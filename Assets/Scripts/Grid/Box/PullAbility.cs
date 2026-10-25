using UnityEngine;

namespace Sokophobia.Grid
{
    public class PullAbility : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private float pullNoiseRadius = 15f;
        [SerializeField] private float pullNoiseIntensity = 1.2f;

        public bool IsUnlocked { get; private set; }

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindAnyObjectByType<GridManager>();
        }

        public void UnlockPull()
        {
            IsUnlocked = true;
            Debug.Log("[PullAbility] Pull unlocked.");
        }

        public void LockPull()
        {
            IsUnlocked = false;
        }

        public GridCoord? TryPull(GridCoord playerCell, GridCoord boxCell, GridCoord dir)
        {
            if (!IsUnlocked) return null;
            if (gridManager == null) return null;

            if (playerCell + dir != boxCell) return null;

            GridCoord playerDestination = playerCell - dir;

            if (!gridManager.IsValidCell(playerDestination)) return null;
            if (gridManager.IsOccupied(playerDestination)) return null;

            var occ = gridManager.GetOccupant(boxCell);
            if (occ == null || occ.Type != OccupantType.Box) return null;
            var box = occ as Box;
            if (box == null) return null;

            if (!gridManager.TryMove(boxCell, playerCell)) return null;

            box.MoveTo(playerCell);

            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                pullNoiseRadius,
                pullNoiseIntensity,
                NoiseSource.BoxPull
            ));

            GameEvents.RaiseBoxPushed(box, boxCell);

            return playerDestination;
        }
    }
}