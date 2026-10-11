using System.Collections.Generic;
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
        [Tooltip("How often senses refresh (s), not every frame.")]
        [SerializeField] private float senseInterval = 0.1f;

        [Header("Alert (suspicion 0..1)")]
        [Tooltip("Suspicion gained per second while the player is visible.")]
        [SerializeField] private float suspicionGain = 2f;
        [SerializeField] private float suspicionDecay = 0.5f;
        [SerializeField, Range(0f, 1f)] private float investigateThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float chaseThreshold = 1f;

        [Header("Search")]
        [Tooltip("How many points to check around lastKnown when investigating.")]
        [SerializeField] private int searchPointCount = 4;
        [SerializeField] private float searchRadius = 3f;

        [Header("Intercept")]
        [Tooltip("How much to lead the player's motion when chasing (0 = current pos).")]
        [SerializeField, Range(0f, 1f)] private float interceptLead = 0.7f;

        [Header("Catch")]
        [SerializeField] private float catchDistance = 1.2f;
        [Tooltip("Wind-up before a tag lands: the player can break away.")]
        [SerializeField] private float catchWindup = 0.45f;
        [SerializeField] private float catchLungeMultiplier = 1.6f;

        [Header("Stun resistance")]
        [Tooltip("Repeated stuns within this window lose potency.")]
        [SerializeField] private float stunDiminishWindow = 8f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private NavMeshAgent _agent;
        private Transform _player;
        private GridPlayerController _playerCtrl;
        private PuzzleManager _puzzle;

        private StateId _state = StateId.Inactive;
        private float _stateTime;
        private int _wpIndex;
        private Vector3 _spawnPos;

        // Blackboard (game-ai: decision layer reads this, motion layer acts on it).
        private Vector3 _lastKnown;
        private bool _hasLastKnown;
        private float _suspicion;
        private bool _seen; // cached sense result, refreshed on senseInterval
        private float _senseTimer;
        private readonly List<Vector3> _searchPoints = new();
        private int _searchIndex;
        private float _repathTimer;
        private float _loseSightTimer;
        private float _tagCooldownTimer;
        private float _catchTimer; // wind-up progress
        private Vector3 _prevPlayerPos;
        private bool _havePrevPlayerPos;
        private Vector3 _playerVel;
        private int _stunCount;
        private float _lastStunTime = -999f;

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
            if (playerObj != null)
            {
                _player = playerObj.transform;
                _playerCtrl = playerObj;
                _prevPlayerPos = _player.position;
                _havePrevPlayerPos = true;
            }
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
        // game-ai: senses refresh on a timer into the blackboard; states only read it.
        private void Update()
        {
            float dt = Time.deltaTime;
            _stateTime += dt;
            _tagCooldownTimer = Mathf.Max(0f, _tagCooldownTimer - dt);

            TrackPlayerVelocity(dt);
            TickSenses(dt);
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

        private void TrackPlayerVelocity(float dt)
        {
            if (_player == null || dt <= 0f) return;
            if (!_havePrevPlayerPos)
            {
                _prevPlayerPos = _player.position;
                _havePrevPlayerPos = true;
                return;
            }
            Vector3 inst = (_player.position - _prevPlayerPos) / dt;
            float k = 1f - Mathf.Exp(-8f * dt);
            _playerVel = Vector3.Lerp(_playerVel, inst, k);
            if (_playerCtrl != null)
            {
                Vector3 pv = _playerCtrl.Velocity;
                if (pv.sqrMagnitude > 0.001f) _playerVel = pv;
            }
            _prevPlayerPos = _player.position;
        }

        private void TickSenses(float dt)
        {
            _senseTimer -= dt;
            if (_senseTimer > 0f) return;
            _senseTimer = Mathf.Max(0.02f, senseInterval);

            _seen = SensePlayer();
            if (_seen)
            {
                _suspicion = Mathf.Min(1f, _suspicion + suspicionGain * senseInterval);
                _lastKnown = _player != null ? _player.position : _lastKnown;
                _hasLastKnown = _player != null;
            }
            else
            {
                _suspicion = Mathf.Max(0f, _suspicion - suspicionDecay * senseInterval);
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
                    BuildSearchPattern(_hasLastKnown ? _lastKnown : transform.position);
                    GoToNextSearchPoint();
                    break;
                case StateId.Chase:
                    _agent.isStopped = false;
                    _loseSightTimer = 0f;
                    _repathTimer = 0f;
                    _catchTimer = 0f;
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
            // Proximity always wins; vision needs a full suspicion meter.
            if (_seen && (_suspicion >= chaseThreshold || IsPlayerClose())) { Enter(StateId.Chase); return; }
            if (_suspicion >= investigateThreshold && _hasLastKnown) { Enter(StateId.Investigate); return; }

            if (HasArrived())
                GoToNextWaypoint();
            // Noise interrupts arrive via PumpNoiseDedup, which enters Investigate directly.
        }

        private void TickInvestigate(float dt)
        {
            if (_seen && (_suspicion >= chaseThreshold || IsPlayerClose())) { Enter(StateId.Chase); return; }

            if (_stateTime >= investigateTimeout)
            {
                _hasLastKnown = false;
                _suspicion = 0f;
                Enter(StateId.Patrol);
                return;
            }

            // game-ai: walk the search pattern instead of idling at one point.
            if (HasArrived())
                GoToNextSearchPoint();
        }

        private void TickChase(float dt)
        {
            if (_player == null) { Enter(StateId.Patrol); return; }

            bool seen = _seen;
            _loseSightTimer = seen ? 0f : _loseSightTimer + dt;

            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                SetDestinationSafe(PredictIntercept());
            }

            // Telegraphed catch: must hold range + line-of-sight through the wind-up.
            // Gives the player a dodge window instead of an instant tag through walls.
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= catchDistance + 0.3f && _tagCooldownTimer <= 0f && HasLineOfSight())
            {
                _catchTimer += dt;
                float lunge = 1f + (catchLungeMultiplier - 1f) * Mathf.Clamp01(_catchTimer / Mathf.Max(0.05f, catchWindup));
                _agent.speed = baseSpeedFor(StateId.Chase) * plateMultiplier() * lunge;
                FacePlayer();

                if (_catchTimer >= catchWindup)
                {
                    float d2 = Vector3.Distance(transform.position, _player.position);
                    if (d2 <= catchDistance && HasLineOfSight())
                    {
                        _tagCooldownTimer = tagCooldown;
                        GameEvents.RaisePlayerDamaged();
                    }
                    _catchTimer = 0f;
                    _hasLastKnown = _player != null;
                    if (_player != null) _lastKnown = _player.position;
                    Enter(_tagCooldownTimer > 0f ? StateId.Patrol : StateId.Chase);
                    return;
                }
            }
            else
            {
                _catchTimer = 0f;
                RefreshSpeedForCurrentState();
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
                if (_seen && _suspicion >= chaseThreshold) { Enter(StateId.Chase); }
                else if (_hasLastKnown) { Enter(StateId.Investigate); }
                else { Enter(StateId.Patrol); }
            }
        }

        // ---------- Stun (callable from any state) ----------
        private float _stunRemaining;

        public void Stun(float durationSeconds)
        {
            if (durationSeconds <= 0f) return;
            // Diminishing returns: chained stuns within the window lose potency.
            float sinceLast = Time.time - _lastStunTime;
            if (sinceLast < stunDiminishWindow) _stunCount++;
            else _stunCount = 0;
            _lastStunTime = Time.time;
            float effective = durationSeconds / (1f + _stunCount * 0.5f);

            if (_state != StateId.Stunned)
            {
                // Keep lastKnown if we had eyes on the player.
                if ((_state == StateId.Chase || _suspicion >= investigateThreshold) && _player != null)
                {
                    _lastKnown = _player.position;
                    _hasLastKnown = true;
                }
                _suspicion = Mathf.Min(_suspicion, investigateThreshold);
                _stunRemaining = effective;
                Enter(StateId.Stunned);
            }
            else
            {
                // Extend, don't stack.
                _stunRemaining = Mathf.Max(_stunRemaining, _stateTime + effective);
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
                _suspicion = Mathf.Max(_suspicion, investigateThreshold);
                if (_state == StateId.Patrol || _state == StateId.Investigate)
                    Enter(StateId.Investigate);
            }
        }

        // ---------- Senses ----------
        private bool IsPlayerClose()
        {
            if (_player == null) return false;
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            return d.magnitude <= proximityRadius;
        }

        private bool HasLineOfSight()
        {
            if (_player == null) return false;
            Vector3 head = transform.position + Vector3.up * headHeight;
            Vector3 target = _player.position + Vector3.up * 1f;
            Vector3 dir = target - head;
            float range = dir.magnitude;
            if (range < 0.001f) return true;
            if (Physics.Raycast(head, dir.normalized, out var hit, range, visionBlockers))
            {
                if (hit.transform != _player && !hit.transform.IsChildOf(_player))
                    return false;
            }
            return true;
        }

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

            return HasLineOfSight();
        }

        // ---------- Intent helpers (game-ai: decision outputs a target position) ----------
        private Vector3 PredictIntercept()
        {
            if (_player == null) return _lastKnown;
            Vector3 target = _player.position;
            float dist = Vector3.Distance(transform.position, target);
            float speed = Mathf.Max(0.5f, _agent.speed);
            float eta = dist / speed;
            Vector3 lead = _playerVel * (eta * Mathf.Clamp01(interceptLead));
            lead.y = 0f;
            if (lead.sqrMagnitude > 16f) lead = lead.normalized * 4f;
            return target + lead;
        }

        private void BuildSearchPattern(Vector3 center)
        {
            _searchPoints.Clear();
            _searchIndex = 0;
            _searchPoints.Add(center);
            int n = Mathf.Max(1, searchPointCount);
            for (int i = 0; i < n; i++)
            {
                float a = (Mathf.PI * 2f * i) / n;
                Vector3 cand = center + new Vector3(Mathf.Cos(a) * searchRadius, 0f, Mathf.Sin(a) * searchRadius);
                if (NavMesh.SamplePosition(cand, out var hit, 2f, NavMesh.AllAreas))
                    _searchPoints.Add(hit.position);
            }
        }

        private void GoToNextSearchPoint()
        {
            if (_searchPoints.Count == 0)
            {
                BuildSearchPattern(_hasLastKnown ? _lastKnown : transform.position);
                if (_searchPoints.Count == 0) return;
            }
            Vector3 dest = _searchPoints[_searchIndex % _searchPoints.Count];
            _searchIndex++;
            SetDestinationSafe(dest);
        }

        private void FacePlayer()
        {
            if (_player == null) return;
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(d.normalized), 1f - Mathf.Exp(-10f * Time.deltaTime));
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

        private void RefreshSpeedForCurrentState() => ApplySpeed(baseSpeedFor(_state));

        private float baseSpeedFor(StateId s) => s switch
        {
            StateId.Chase => chaseSpeed,
            StateId.Investigate => investigateSpeed,
            _ => patrolSpeed,
        };

        private float plateMultiplier()
        {
            if (_puzzle != null && _puzzle.TotalPlates > 0)
                return 1f + ((float)_puzzle.PlacedCount / _puzzle.TotalPlates) * maxPlateBonus;
            return 1f;
        }

        private void ApplySpeed(float baseSpeed)
        {
            _agent.speed = baseSpeed * plateMultiplier();
        }

        public StateId DebugState => _state;
        public float DebugSuspicion => _suspicion;
        public float InvestigateThreshold => investigateThreshold;
        public float ChaseThreshold => chaseThreshold;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = Color.yellow;
            if (waypoints != null)
                foreach (var w in waypoints)
                    Gizmos.DrawWireSphere(w, 0.3f);

#if UNITY_EDITOR
            // game-ai verify-by-observation: vision cone, lastKnown, search, live path.
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            float half = visionAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 left = Quaternion.Euler(0f, -half * Mathf.Rad2Deg, 0f) * fwd;
            Vector3 right = Quaternion.Euler(0f, half * Mathf.Rad2Deg, 0f) * fwd;
            Gizmos.color = _seen ? Color.red : Color.cyan;
            Gizmos.DrawRay(transform.position + Vector3.up * headHeight, left * visionRange);
            Gizmos.DrawRay(transform.position + Vector3.up * headHeight, right * visionRange);

            if (_hasLastKnown)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_lastKnown + Vector3.up * 0.5f, 0.4f);
            }
            Gizmos.color = Color.green;
            foreach (var p in _searchPoints)
                Gizmos.DrawWireSphere(p + Vector3.up * 0.3f, 0.25f);

            if (_agent != null && _agent.hasPath)
            {
                Gizmos.color = Color.white;
                Vector3 prev = transform.position;
                foreach (var c in _agent.path.corners)
                {
                    Gizmos.DrawLine(prev + Vector3.up * 0.3f, c + Vector3.up * 0.3f);
                    prev = c;
                }
            }
#endif
        }
    }
}
