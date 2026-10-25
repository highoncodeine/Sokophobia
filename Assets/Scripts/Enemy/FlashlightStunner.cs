using UnityEngine;
using UnityEngine.InputSystem;
using Sokophobia.Interaction;

namespace Sokophobia.Enemy
{
    public class FlashlightStunner : MonoBehaviour
    {
        [Header("Beam")]
        [SerializeField] private float range = 10f;
        [SerializeField] private float angle = 30f;
        [SerializeField] private float stunDuration = 3f;
        [Tooltip("High-beam discharge time after a successful stun.")]
        [SerializeField] private float cooldown = 2.5f;
        [SerializeField] private LayerMask blockers = ~0;
        
        [Header("Charge (counterplay)")]
        [Tooltip("How long the high beam must stay on Sokobot before the stun fires.")]
        [SerializeField] private float chargeTime = 0.6f;

        [Header("Battery")]
        [Tooltip("How much battery is consumed per successful high-beam stun.")]
        [SerializeField] private float batteryCost = 20f;

        private Transform _cam;
        private SokobotController _sokobot;
        private Collider _sokobotCollider;
        private FlashlightBattery _battery;
        
        private float _cd;
        private float _charge;
        private bool _needsRelease;
        private bool _wasHighBeamActive;

        public bool IsHighBeamActive { get; private set; }

        public bool IsDischarging => _cd > 0f;

        public float Cooldown01 => Mathf.Clamp01(_cd / Mathf.Max(0.05f, cooldown));

        public float Charge01 => Mathf.Clamp01(_charge / Mathf.Max(0.05f, chargeTime));

        [SerializeField] private AudioSource flashlightSfx;

        private void Awake()
        {
            var cam = Camera.main;
            _cam = cam != null ? cam.transform : transform;
            
            _battery = GetComponent<FlashlightBattery>();
        }

        private void Start()
        {
            _sokobot = FindAnyObjectByType<SokobotController>(FindObjectsInactive.Include);
            _sokobotCollider = _sokobot != null ? _sokobot.GetComponentInChildren<Collider>(true) : null;
        }

        private void Update()
        {
            _cd = Mathf.Max(0f, _cd - Time.deltaTime);

            bool held = HighBeamHeld();

            if (!held && _cd <= 0f)
                _needsRelease = false;

            IsHighBeamActive = held
                && !_needsRelease
                && _cd <= 0f
                && CanAffordHighBeam();
            
            if (IsHighBeamActive && !_wasHighBeamActive)
            {
                if (flashlightSfx != null) 
                {
                    flashlightSfx.Play();
                }
            }
            
            _wasHighBeamActive = IsHighBeamActive; 

            if (!IsHighBeamActive)
            {
                _charge = 0f;
                return;
            }

            if (!IsHighBeamActive)
            {
                _charge = 0f;
                return;
            }

            if (_sokobot == null || !_sokobot.isActiveAndEnabled || _cam == null) return;

            if (!BeamOnTarget())
            {
                _charge = 0f;
                return;
            }

            _charge += Time.deltaTime;

            if (_charge >= chargeTime)
            {
                if (_battery != null && !_battery.TryConsume(batteryCost))
                {
                    _charge = 0f;
                    IsHighBeamActive = false;
                    return;
                }

                _charge = 0f;
                _cd = cooldown;
                _needsRelease = true;
                IsHighBeamActive = false;
                _sokobot.Stun(stunDuration);
            }
        }

        private void OnDisable()
        {
            IsHighBeamActive = false;
            _charge = 0f;
        }

        private bool HighBeamHeld()
        {
            if (Time.timeScale <= 0f) return false;
            if (Cursor.lockState != CursorLockMode.Locked) return false;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        private bool CanAffordHighBeam()
        {
            return _battery == null || _battery.CurrentCharge >= batteryCost;
        }

        private bool BeamOnTarget()
        {
            Vector3 aim = GetAimPoint();
            Vector3 toBot = aim - _cam.position;
            float dist = toBot.magnitude;
            if (dist > range) return false;
            if (dist < 0.05f) return true;
            if (Vector3.Angle(_cam.forward, toBot.normalized) > angle * 0.5f) return false;

            if (Physics.Raycast(_cam.position, toBot.normalized, out var hit, dist + 0.01f, blockers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != _sokobot.transform && !hit.transform.IsChildOf(_sokobot.transform))
                    return false;
            }
            return true;
        }

        private Vector3 GetAimPoint()
        {
            if (_sokobotCollider != null && _sokobotCollider.enabled && _sokobotCollider.gameObject.activeInHierarchy)
                return _sokobotCollider.ClosestPoint(_cam.position);
            return _sokobot.transform.position + Vector3.up * 1f;
        }
    }
}