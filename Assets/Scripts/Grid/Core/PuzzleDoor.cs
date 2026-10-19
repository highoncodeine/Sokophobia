using UnityEngine;

namespace Sokophobia.Grid
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Puzzle Door")]
    public class PuzzleDoor : MonoBehaviour
    {
        [Tooltip("Puzzle that opens this door. If empty, finds a PuzzleManager in the scene.")]
        [SerializeField] private PuzzleManager puzzleManager;

        private bool isOpen;

        private void OnEnable()
        {
            if (isOpen)
                return;

            if (puzzleManager == null)
                puzzleManager = FindAnyObjectByType<PuzzleManager>();

            if (puzzleManager == null)
            {
                Debug.LogError("[PuzzleDoor] No PuzzleManager found. Add one to the scene or assign it in the Inspector.", this);
                return;
            }

            puzzleManager.OnPuzzleSolved += Open;

            if (puzzleManager.IsSolved)
                Open();
        }

        private void OnDisable()
        {
            if (puzzleManager != null)
                puzzleManager.OnPuzzleSolved -= Open;
        }

        private void Open()
        {
            if (isOpen)
                return;

            isOpen = true;

            GridManager grid = FindAnyObjectByType<GridManager>();
            GridBaker baker = grid != null ? grid.GetComponent<GridBaker>() : null;

            gameObject.SetActive(false);

            if (baker != null)
            {
                Physics.SyncTransforms();
                baker.Bake();
            }

            GameEvents.RaisePuzzleDoorOpened();
        }
    }
}