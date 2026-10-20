using System;
using UnityEngine;

namespace Sokophobia.Interaction
{
    public class FlashlightBattery : MonoBehaviour
    {
        [SerializeField] private float maxCharge = 100f;
        [SerializeField] private float startCharge = 100f;

        public float CurrentCharge { get; private set; }
        public float Charge01 => CurrentCharge / maxCharge;

        public event Action OnChargeChanged;

        private void Awake()
        {
            CurrentCharge = startCharge;
        }

        public void AddCharge(float amount)
        {
            CurrentCharge = Mathf.Clamp(CurrentCharge + amount, 0f, maxCharge);
            OnChargeChanged?.Invoke();
        }

        public bool TryConsume(float amount)
        {
            if (CurrentCharge < amount) return false;
            CurrentCharge -= amount;
            OnChargeChanged?.Invoke();
            return true;
        }
    }
}