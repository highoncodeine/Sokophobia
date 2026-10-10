using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Attached to the Player. Validates and executes box pushes.
    /// The player calls TryPush(targetCell, direction) when trying to step into a box.
    /// </summary>
    public class BoxPushController : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
        }

        /// <summary>
        /// Attempt to push the box at <paramref name="boxCell"/> in <paramref name="dir"/>.
        /// Returns true if the push succeeded (box moved); the caller should then move the player.
        /// </summary>
        public bool TryPush(GridCoord boxCell, GridCoord dir)
        {
            if (gridManager == null) return false;

            GridCoord destination = boxCell + dir;

            // 1. Destination must be a valid floor cell
            if (!gridManager.IsValidCell(destination)) return false;

            // 2. Destination must be unoccupied
            if (gridManager.IsOccupied(destination)) return false;

            // 3. Grab the box
            var occ = gridManager.GetOccupant(boxCell);
            if (occ == null || occ.Type != OccupantType.Box) return false;
            var box = occ as Box;
            if (box == null) return false;

            // 4. Move the box in the grid (logical)
            if (!gridManager.TryMove(boxCell, destination)) return false;

            // 5. Visual + noise handled inside Box.MoveTo
            box.MoveTo(destination);

            // 6. Notify external systems (Person 2's AI, Person 3's puzzle)
            GameEvents.RaiseBoxPushed(box, boxCell);

            return true;
        }
    }
}