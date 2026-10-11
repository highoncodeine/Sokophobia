using UnityEngine;
using UnityEngine.AI;
using Sokophobia.Grid;

namespace Sokophobia.Enemy
{
    /// <summary>
    /// Sokobot encounter AI. FSM: Inactive -> Patrol/Investigate -> Chase -> Stunned.
    /// Decide (this FSM) outputs intent; NavMeshAgent executes motion.
    /// Noise (BoxPush/BoxPull) drives Investigate; vision + proximity drive Chase.
    /// Speed scales with PuzzleManager progress. Stun via ISokobotStunnable.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class SokobotController : MonoBehaviour, ISokobotStunnable
    {
        public enum StateId { Inactive, Patrol, Investigate, Chase, Stunned }

        [Header("Patrol")]
        [SerializeField] private Vector3[] waypoints = new Vector3[0];
        [SerializeField] private float patrolRadiusFallback = 10f;
        [SerializeField] private float waypointTolerance = 0.5f;

        [Header("Timing")]
        [SerializeField] private float inactiveDelay = 4f;
        [SerializeField] private float investigateTimeout = 6f;
        [SerializeField] private float loseSightTimeout = 4f;
        [SerializeField] private float repathInterval = 0.2f;
        [SerializeField] private float noiseDedupWindow = 0.25f;
        [SerializeField] private float tagCooldown = 2f;

        [Header("Speeds (base, before plate multiplier)")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float investigateSpeed = 2.5f;
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float maxPlateBonus = 0.8f;

        [Header("Senses")]
        [SerializeField] private float visionRange = 8f;
        [SerializeField] private float visionAngle = 60f;
        [SerializeField] private float headHeight = 1.6f;
        [SerializeField] private float proximityRadius = 2f;
        [SerializeField] private LayerMask visionBlockers = ~0;

        [Header("Catch")]
        [SerializeField] private float catchDistance = 1.2f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private NavMeshAgent _agent;
        private Transform _player;
        private PuzzleManager _puzzle;

        private StateId _state = StateId.Inactive;
        private float _stateTime;
        private int _wpIndex;
        private Vector3 _spawnPos;

        private Vector3 _lastKnown;
        private bool _hasLastKnown;
        private float _repathTimer;
        private float _loseSightTimer;
        private float _tagCooldownTimer;

        // Noise dedup: keep loudest within window, not first.
        private bool _noisePending;
        private float _noiseTimer;
        private NoiseEvent _loudestNoise;
        private float _loudestReach;

        public StateId CurrentState => _state;

        // ---------- Lifecycle (unity-csharp-scripting: Awake cache, OnEnable subscribe) ----------
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _spawnPos = transform.position;
        }

        private void Start()
        {
            var playerObj = FindAnyObjectByType<GridPlayerController>();
            if (playerObj != null) _player = playerObj.transform;
            else Debug.LogError("[Sokobot] No GridPlayerController found.");

            _puzzle = FindAnyObjectByType<PuzzleManager>();

            // Soft check: warn (not error) so Pause-on-Error never freezes Play entry.
            // The agent registers with the mesh on enable; SokobotController.Start can
            // run before that, so a hard error here is a false negative.
            if (!NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
                Debug.LogWarning("[Sokobot] Spawn off baked NavMesh (will self-correct if agent registers).");
            else
                transform.position = hit.position;

            Enter(StateId.Inactive);
        }

        private void OnEnable()
        {
            GameEvents.OnNoise += HandleNoise;
            GameEvents.OnBoxPlaced += HandlePlateProgress;
            GameEvents.OnBoxRemovedFromPlate += HandlePlateProgress;
        }

        private void OnDisable()
        {
            GameEvents.OnNoise -= HandleNoise;
            GameEvents.OnBoxPlaced -= HandlePlateProgress;
            GameEvents.OnBoxRemovedFromPlate -= HandlePlateProgress;
        }

        // ---------- Per-frame driver ----------
        private void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            _tagCooldownTimer = Mathf.Max(0f, _tagCooldownTimer - dt);

            PumpNoiseDedup(dt);

            switch (_state)
            {
                case StateId.Inactive: TickInactive(); break;
                case StateId.Patrol: TickPatrol(dt); break;
                case StateId.Investigate: TickInvestigate(dt); break;
                case StateId.Chase: TickChase(dt); break;
                case StateId.Stunned: TickStunned(); break;
            }
        }

        // ---------- States ----------
        private void Enter(StateId next)
        {
            _state = next;
            _stateTime = 0f;

            switch (next)
            {
                case StateId.Inactive:
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    break;
                case StateId.Patrol:
                    _agent.isStopped = false;
                    ApplySpeed(patrolSpeed);
                    GoToNextWaypoint();
                    break;
                case StateId.Investigate:
                    _agent.isStopped = false;
                    ApplySpeed(investigateSpeed);
                    if (_hasLastKnown) SetDestinationSafe(_lastKnown);
                    break;
                case StateId.Chase:
                    _agent.isStopped = false;
                    _loseSightTimer = 0f;
                    _repathTimer = 0f;
                    ApplySpeed(chaseSpeed);
                    break;
                case StateId.Stunned:
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    break;
            }
        }

        private void TickInactive()
        {
            if (_stateTime >= inactiveDelay)
                Enter(StateId.Patrol);
        }

        private void TickPatrol(float dt)
        {
            if (SensePlayer()) { Enter(StateId.Chase); return; }

            if (HasArrived())
                GoToNextWaypoint();
            // Noise interrupts arrive via PumpNoiseDedup, which enters Investigate directly.
        }

        private void TickInvestigate(float dt)
        {
            if (SensePlayer()) { Enter(StateId.Chase); return; }

            if (_stateTime >= investigateTimeout)
            {
                _hasLastKnown = false;
                Enter(StateId.Patrol);
                return;
            }

            if (HasArrived())
            {
                // Reached the noise point, nothing visible: wait out the timeout.
                _agent.ResetPath();
            }
        }

        private void TickChase(float dt)
        {
            if (_player == null) { Enter(StateId.Patrol); return; }

            bool seen = SensePlayer();
            _loseSightTimer = seen ? 0f : _loseSightTimer + dt;

            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                if (seen) _lastKnown = _player.position;
                SetDestinationSafe(seen ? _player.position : _lastKnown);
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= catchDistance && _tagCooldownTimer <= 0f)
            {
                _tagCooldownTimer = tagCooldown;
                GameEvents.RaisePlayerDamaged();
                _hasLastKnown = false;
                Enter(StateId.Patrol);
                return;
            }

            if (_loseSightTimer >= loseSightTimeout)
            {
                _hasLastKnown = true;
                Enter(StateId.Investigate);
            }
        }

        private void TickStunned()
        {
            // Timer is driven by _stunRemaining (see Stun). Exit handled there via state time.
            if (_stateTime >= _stunRemaining)
            {
                // One-step de-escalation: never resume full Chase unless visible right now.
                if (SensePlayer()) { Enter(StateId.Chase); }
                else if (_hasLastKnown) { Enter(StateId.Investigate); }
                else { Enter(StateId.Patrol); }
            }
        }

        // ---------- Stun (callable from any state) ----------
        private float _stunRemaining;

        public void Stun(float durationSeconds)
        {
            if (durationSeconds <= 0f) return;
            if (_state != StateId.Stunned)
            {
                // Keep lastKnown if we had eyes on the player.
                if (_state == StateId.Chase && _player != null)
                {
                    _lastKnown = _player.position;
                    _hasLastKnown = true;
                }
                _stunRemaining = durationSeconds;
                Enter(StateId.Stunned);
            }
            else
            {
                // Extend, don't stack.
                _stunRemaining = Mathf.Max(_stunRemaining, _stateTime + durationSeconds);
            }
        }

        // ---------- Noise (dedup: keep loudest in window) ----------
        private void HandleNoise(NoiseEvent e)
        {
            if (e.Source != NoiseSource.BoxPush && e.Source != NoiseSource.BoxPull)
                return;

            float reach = e.Radius * e.Intensity; // unclamped: pull 15*1.2=18m > push 12m
            if (!_noisePending || reach > _loudestReach)
            {
                _loudestNoise = e;
                _loudestReach = reach;
            }
            _noisePending = true;
            _noiseTimer = noiseDedupWindow;
        }

        private void PumpNoiseDedup(float dt)
        {
            if (!_noisePending) return;
            _noiseTimer -= dt;
            if (_noiseTimer > 0f) return;
            _noisePending = false;

            if (_state == StateId.Stunned || _state == StateId.Chase || _state == StateId.Inactive)
                return;

            float dist = Vector3.Distance(transform.position, _loudestNoise.Position);
            if (dist <= _loudestReach)
            {
                _lastKnown = _loudestNoise.Position;
                _hasLastKnown = true;
                if (_state == StateId.Patrol || _state == StateId.Investigate)
                    Enter(StateId.Investigate);
            }
        }

        // ---------- Senses ----------
        private bool SensePlayer()
        {
            if (_player == null) return false;

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Proximity: hears breathing, no LOS needed.
            if (dist <= proximityRadius) return true;

            if (dist > visionRange) return false;

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            if (Vector3.Angle(fwd, toPlayer.normalized) > visionAngle * 0.5f) return false;

            Vector3 head = transform.position + Vector3.up * headHeight;
            Vector3 target = _player.position + Vector3.up * 1f;
            Vector3 dir = target - head;
            float range = dir.magnitude;
            if (Physics.Raycast(head, dir.normalized, out var hit, range, visionBlockers))
            {
                // Blocked unless the first hit is the player itself.
                if (hit.transform != _player && !hit.transform.IsChildOf(_player))
                    return false;
            }
            return true;
        }

        // ---------- Motion helpers (unity-navmesh patterns) ----------
        private void SetDestinationSafe(Vector3 dest)
        {
            if (!NavMesh.SamplePosition(dest, out var hit, 2f, NavMesh.AllAreas))
                return;
            // Don't commit to unreachable targets.
            var path = new NavMeshPath();
            if (_agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                _agent.SetDestination(hit.position);
        }

        private bool HasArrived()
        {
            if (_agent.pathPending) return false;
            return _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, waypointTolerance);
        }

        private void GoToNextWaypoint()
        {
            Vector3 dest;
            if (waypoints != null && waypoints.Length > 0)
            {
                dest = waypoints[_wpIndex % waypoints.Length];
                _wpIndex++;
            }
            else
            {
                // Random fallback: reachable point within radius, never broken.
                dest = _spawnPos;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 cand = _spawnPos + new Vector3(
                        Random.Range(-patrolRadiusFallback, patrolRadiusFallback), 0f,
                        Random.Range(-patrolRadiusFallback, patrolRadiusFallback));
                    if (NavMesh.SamplePosition(cand, out var hit, 2f, NavMesh.AllAreas))
                    {
                        dest = hit.position;
                        break;
                    }
                }
            }
            SetDestinationSafe(dest);
        }

        // ---------- Difficulty ----------
        private void HandlePlateProgress(Box box, GridCoord cell) => RefreshSpeedForCurrentState();

        private void RefreshSpeedForCurrentState()
        {
            float b = _state switch
            {
                StateId.Chase => chaseSpeed,
                StateId.Investigate => investigateSpeed,
                _ => patrolSpeed,
            };
            ApplySpeed(b);
        }

        private void ApplySpeed(float baseSpeed)
        {
            float mult = 1f;
            if (_puzzle != null && _puzzle.TotalPlates > 0)
                mult = 1f + ((float)_puzzle.PlacedCount / _puzzle.TotalPlates) * maxPlateBonus;
            _agent.speed = baseSpeed * mult;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = Color.yellow;
            if (waypoints != null)
                foreach (var w in waypoints)
                    Gizmos.DrawWireSphere(w, 0.3f);
        }
    }
}
