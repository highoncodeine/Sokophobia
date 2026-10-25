using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Sokophobia.UI;

namespace Sokophobia.Grid
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Quick Restart")]
    public class QuickRestart : MonoBehaviour
    {
        [Tooltip("Reloads the current level, including while paused or on an end screen. Set to None to disable.")]
        [SerializeField] private Key restartKey = Key.F5;

        private bool _isRestarting;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && restartKey != Key.None && keyboard[restartKey].wasPressedThisFrame)
                RestartLevel();
        }

        [ContextMenu("Restart Level (Play Mode)")]
        public void RestartLevel()
        {
            if (!Application.isPlaying || _isRestarting)
                return;

            Scene level = gameObject.scene;
            if (!level.IsValid() || level.buildIndex < 0)
            {
                Debug.LogWarning("[QuickRestart] Add this level to the Build Profiles scene list before restarting.", this);
                return;
            }

            _isRestarting = true;

            if (TryGetComponent<DoorOpeningPopup>(out var popup))
                popup.Dismiss();

            if (TryGetComponent<VictoryScreenController>(out var victory) && victory.IsVisible)
            {
                victory.PlayAgain();
                return;
            }

            if (TryGetComponent<GameOverController>(out var gameOver) && gameOver.IsVisible)
            {
                gameOver.PlayAgain();
                return;
            }

            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(level.buildIndex);
        }
    }
}