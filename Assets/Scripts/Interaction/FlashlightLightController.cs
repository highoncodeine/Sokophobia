using UnityEngine;
using Sokophobia.Enemy;

namespace Sokophobia.Interaction
{
    [RequireComponent(typeof(FlashlightBattery))]
    public class FlashlightLightController : MonoBehaviour
    {
        [Header("Lights")]
        [Tooltip("Spot light projected in front of the player. Found by name if left empty.")]
        [SerializeField] private Light beam;

        [Tooltip("Point light that keeps the held flashlight model visible. Found by name if left empty.")]
        [SerializeField] private Light fill;

        [Header("Low Battery")]
        [Tooltip("Intensity multiplier at the lowest non-zero charge.")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumIntensityScale = 0.25f;

        [Header("High Beam")]
        [Tooltip("Intensity multiplier while the high beam is held. High values sell the stun punch.")]
        [SerializeField] private float highBeamIntensityMultiplier = 12f;

        [Tooltip("Fill-light intensity multiplier while the high beam is held.")]
        [SerializeField] private float highBeamFillMultiplier = 6f;

        [Tooltip("Spot angle while the high beam is held. Keep near the stunner's cone angle.")]
        [SerializeField] private float highBeamSpotAngle = 30f;

        [Header("Stun Flash")]
        [Tooltip("Extra brightness multiplier at the instant a stun lands, on top of the high beam.")]
        [SerializeField] private float stunFlashMultiplier = 4f;

        [Tooltip("Seconds for the stun flash to fade back into the discharge flicker.")]
        [SerializeField] private float stunFlashDuration = 0.18f;

        [Header("Discharge")]
        [Tooltip("Beam brightness multiplier while the high beam is recharging.")]
        [Range(0f, 1f)]
        [SerializeField] private float dischargeIntensityMultiplier = 0.55f;

        [Tooltip("How fast the beam flickers during discharge.")]
        [SerializeField] private float dischargeFlickerSpeed = 18f;

        private const string BeamName = "FlashlightBeam";
        private const string FillName = "FlashlightFill";

        private FlashlightBattery _battery;
        private FlashlightStunner _stunner;
        private float _beamIntensity;
        private float _fillIntensity;
        private float _baseSpotAngle;
        private float _currentSpotAngle;
        private float _chargeScale = 1f;
        private bool _wasDischarging;
        private float _stunFlashTimer;

        private void Awake()
        {
            _battery = GetComponent<FlashlightBattery>();
            _stunner = GetComponent<FlashlightStunner>();

            if (beam == null) beam = FindLight(BeamName);
            if (fill == null) fill = FindLight(FillName);

            if (beam == null)
                Debug.LogWarning($"[FlashlightLightController] Light '{BeamName}' not found.", this);
            if (fill == null)
                Debug.LogWarning($"[FlashlightLightController] Light '{FillName}' not found.", this);

            if (beam != null)
            {
                _beamIntensity = beam.intensity;
                _baseSpotAngle = beam.spotAngle;
                _currentSpotAngle = _baseSpotAngle;
            }

            if (fill != null) _fillIntensity = fill.intensity;
        }

        private void OnEnable()
        {
            if (_battery != null) _battery.OnChargeChanged += Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            bool discharging = _stunner != null && _stunner.IsDischarging;
            if (discharging && !_wasDischarging)
                _stunFlashTimer = Mathf.Max(0.01f, stunFlashDuration);
            _wasDischarging = discharging;

            ApplyLighting();

            if (_stunFlashTimer > 0f)
                _stunFlashTimer = Mathf.Max(0f, _stunFlashTimer - Time.deltaTime);
        }

        private void OnDisable()
        {
            if (_battery != null) _battery.OnChargeChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_battery == null) return;

            float charge = Mathf.Clamp01(_battery.Charge01);
            _chargeScale = charge > 0f ? Mathf.Lerp(minimumIntensityScale, 1f, charge) : 0f;
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            float beamScale = _chargeScale;
            float fillScale = _chargeScale;
            float targetAngle = _baseSpotAngle;

            if (_stunner != null && _stunner.IsHighBeamActive)
            {
                beamScale *= highBeamIntensityMultiplier;
                fillScale *= highBeamFillMultiplier;
                targetAngle = highBeamSpotAngle;
            }
            else if (_stunner != null && _stunner.IsDischarging)
            {
                float flicker = Mathf.Lerp(0.35f, 1f,
                    Mathf.PerlinNoise(Time.time * dischargeFlickerSpeed, 0.37f));
                beamScale *= dischargeIntensityMultiplier * flicker;
                fillScale *= dischargeIntensityMultiplier * flicker;
            }

            if (_stunFlashTimer > 0f && stunFlashDuration > 0.0001f)
            {
                float flash01 = Mathf.Clamp01(_stunFlashTimer / stunFlashDuration);
                float beamFlash = _chargeScale * highBeamIntensityMultiplier * stunFlashMultiplier;
                float fillFlash = _chargeScale * highBeamFillMultiplier * stunFlashMultiplier;
                beamScale = Mathf.Lerp(beamScale, beamFlash, flash01);
                fillScale = Mathf.Lerp(fillScale, fillFlash, flash01);
            }

            _currentSpotAngle = Mathf.Lerp(_currentSpotAngle, targetAngle,
                1f - Mathf.Exp(-18f * Time.deltaTime));

            Apply(beam, _beamIntensity, beamScale, _currentSpotAngle);
            Apply(fill, _fillIntensity, fillScale);
        }

        private static void Apply(Light light, float baseIntensity, float scale, float? spotAngle = null)
        {
            if (light == null) return;

            light.intensity = baseIntensity * scale;
            light.enabled = scale > 0f;

            if (spotAngle.HasValue && light.type == LightType.Spot)
                light.spotAngle = spotAngle.Value;
        }

        private Light FindLight(string lightName)
        {
            Light[] lights = GetComponentsInChildren<Light>(true);
            foreach (Light light in lights)
            {
                if (light.name == lightName) return light;
            }

            return null;
        }
    }
}
