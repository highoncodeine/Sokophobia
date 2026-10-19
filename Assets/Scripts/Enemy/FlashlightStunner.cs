using UnityEngine;

namespace Sokophobia.Enemy
{
    /// <summary>
    /// First stun trigger: flashlight beam. Each cooldown tick checks cone + LOS
    /// from the player camera to Sokobot and calls ISokobotStunnable.Stun.
    /// Attach near the player (uses Camera.main by default).
    /// </summary>
    public class FlashlightStunner : MonoBehaviour
    {
        [Header("Beam")]
        [SerializeField] private float range = 10f;
        [SerializeField] private float angle = 30f;
        [SerializeField] private float stunDuration = 3f;
        [SerializeField] private float cooldown = 1f;
        [SerializeField] private LayerMask blockers = ~0;
        [Header("Charge (counterplay)")]
        [Tooltip("How long the beam must stay on Sokobot before the stun fires.")]
        [SerializeField] private float chargeTime = 0.6f;

        private Transform _cam;
        private SokobotController _sokobot;
        private float _cd;
        private float _charge;

        /// <summary>0..1 beam charge, for HUD/telegraphs.</summary>
        public float Charge01 => Mathf.Clamp01(_charge / Mathf.Max(0.05f, chargeTime));

        private void Awake()
        {
            var cam = Camera.main;
            _cam = cam != null ? cam.transform : transform;
        }

        private void Start()
        {
            // Cache the enemy even while dormant; EnemySpawner activates it later.
            _sokobot = FindAnyObjectByType<SokobotController>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            _cd -= Time.deltaTime;
            if (_sokobot == null || !_sokobot.isActiveAndEnabled || _cam == null) return;
            if (_cd > 0f) { _charge = 0f; return; }

            if (!BeamOnTarget())
            {
                _charge = 0f;
                return;
            }

            _charge += Time.deltaTime;
            if (_charge >= chargeTime)
            {
                _charge = 0f;
                _cd = cooldown;
                _sokobot.Stun(stunDuration);
            }
        }

        private bool BeamOnTarget()
        {
            Vector3 toBot = _sokobot.transform.position - _cam.position;
            float dist = toBot.magnitude;
            if (dist > range || dist < 0.001f) return false;
            if (Vector3.Angle(_cam.forward, toBot.normalized) > angle * 0.5f) return false;

            if (Physics.Raycast(_cam.position, toBot.normalized, out var hit, dist, blockers))
            {
                if (hit.transform != _sokobot.transform && !hit.transform.IsChildOf(_sokobot.transform))
                    return false;
            }
            return true;
        }
    }
}
