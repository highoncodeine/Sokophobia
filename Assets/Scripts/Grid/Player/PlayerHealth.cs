using System;
using UnityEngine;

namespace Sokophobia.Grid
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Player Health")]
    [DefaultExecutionOrder(-80)]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 10;
        [SerializeField, Min(1)] private int damagePerEnemyHit = 1;
        [SerializeField] private AudioSource damageSfx;

        public event Action<int, int> OnHealthChanged;

        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerDamaged += HandleEnemyHit;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerDamaged -= HandleEnemyHit;
        }

        private void HandleEnemyHit()
        {
            TakeDamage(Mathf.Max(1, damagePerEnemyHit));
        }

        public void TakeDamage(int amount)
        {
            damageSfx.Play();
            if (!isActiveAndEnabled || IsDead || amount <= 0 || Time.timeScale <= 0f)
                return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (IsDead)
            {
                // Stop an in-progress step too; disabling a component alone
                // would leave its movement coroutine running.
                if (TryGetComponent<GridPlayerController>(out var movement))
                {
                    movement.StopAllCoroutines();
                    movement.enabled = false;
                }

                GameEvents.RaisePlayerDied();
            }
        }

        [ContextMenu("Testing/Take One Enemy Hit")]
        private void TakeTestHit()
        {
            if (Application.isPlaying && (Application.isEditor || Debug.isDebugBuild))
                GameEvents.RaisePlayerDamaged();
        }

        [ContextMenu("Testing/Lose All Health")]
        private void LoseAllHealthForTesting()
        {
            if (Application.isPlaying && (Application.isEditor || Debug.isDebugBuild))
                TakeDamage(CurrentHealth);
        }
    }
}
