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

        private Transform _cam;
        private SokobotController _sokobot;
        private float _cd;

        private void Awake()
        {
            var cam = Camera.main;
            _cam = cam != null ? cam.transform : transform;
        }

        private void Start()
        {
            _sokobot = FindAnyObjectByType<SokobotController>();
        }

        private void Update()
        {
            _cd -= Time.deltaTime;
            if (_cd > 0f || _sokobot == null || _cam == null) return;

            Vector3 toBot = _sokobot.transform.position - _cam.position;
            float dist = toBot.magnitude;
            if (dist > range) return;
            if (Vector3.Angle(_cam.forward, toBot.normalized) > angle * 0.5f) return;

            if (Physics.Raycast(_cam.position, toBot.normalized, out var hit, dist, blockers))
            {
                if (hit.transform != _sokobot.transform && !hit.transform.IsChildOf(_sokobot.transform))
                    return;
            }

            _cd = cooldown;
            _sokobot.Stun(stunDuration);
        }
    }
}
