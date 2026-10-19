using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokophobia.Grid
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Debug/Puzzle Cheats")]
    public class PuzzleCheats : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        [Tooltip("Optional. If empty, finds a PuzzleManager when the shortcut is pressed.")]
        [SerializeField] private PuzzleManager puzzleManager;

        [SerializeField] private Key completePuzzleKey = Key.F8;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (Time.timeScale <= 0f || keyboard == null || completePuzzleKey == Key.None)
                return;

            if (keyboard[completePuzzleKey].wasPressedThisFrame)
                CompletePuzzle();
        }

        [ContextMenu("Complete Puzzle (Testing)")]
        public void CompletePuzzle()
        {
            if (!Application.isPlaying || (!Application.isEditor && !Debug.isDebugBuild))
                return;

            if (puzzleManager == null)
                puzzleManager = FindAnyObjectByType<PuzzleManager>();

            if (puzzleManager == null)
            {
                Debug.LogWarning("[PuzzleCheats] No PuzzleManager found in the scene.", this);
                return;
            }

            puzzleManager.CompleteForTesting();
        }
#endif
    }
}