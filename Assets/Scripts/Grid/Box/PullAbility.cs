using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Attached to the Player. Handles pull ability.
    /// Locked by default; call UnlockPull() from a puzzle condition to enable.
    /// Pull semantics: player is at P, box is in front at B = P + dir.
    /// After pull: box moves to P (player's old cell), player moves to P - dir (one cell back).
    /// </summary>
    public class PullAbility : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private float pullNoiseRadius = 15f;   // pull is louder than push
        [SerializeField] private float pullNoiseIntensity = 1.2f;

        public bool IsUnlocked { get; private set; }

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
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

        /// <summary>
        /// Attempt to pull the box at <paramref name="boxCell"/>.
        /// Returns the cell the player should move to if successful, otherwise null.
        /// </summary>
        public GridCoord? TryPull(GridCoord playerCell, GridCoord boxCell, GridCoord dir)
        {
            if (!IsUnlocked) return null;
            if (gridManager == null) return null;

            // Verify the box is in front
            if (playerCell + dir != boxCell) return null;

            // Player's destination after the pull is one cell back from their current position
            GridCoord playerDestination = playerCell - dir;

            // 1. Player's destination must be valid & unoccupied
            if (!gridManager.IsValidCell(playerDestination)) return null;
            if (gridManager.IsOccupied(playerDestination)) return null;

            // 2. Grab the box
            var occ = gridManager.GetOccupant(boxCell);
            if (occ == null || occ.Type != OccupantType.Box) return null;
            var box = occ as Box;
            if (box == null) return null;

            // 3. Move box into player's old cell (logical)
            if (!gridManager.TryMove(boxCell, playerCell)) return null;

            // 4. Visual + noise for box
            box.MoveTo(playerCell);

            // 5. Pull is louder than push — override noise payload
            GameEvents.RaiseNoise(new NoiseEvent(
                transform.position,
                pullNoiseRadius,
                pullNoiseIntensity,
                NoiseSource.BoxPull
            ));

            // 6. Notify
            GameEvents.RaiseBoxPushed(box, boxCell);

            // 7. Caller moves player to playerDestination
            return playerDestination;
        }
    }
}