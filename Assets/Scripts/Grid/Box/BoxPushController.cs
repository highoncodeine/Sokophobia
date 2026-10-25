using UnityEngine;

namespace Sokophobia.Grid
{
    public class BoxPushController : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = FindAnyObjectByType<GridManager>();
        }

        public bool TryPush(GridCoord boxCell, GridCoord dir)
        {
            if (gridManager == null) return false;

            GridCoord destination = boxCell + dir;

            if (!gridManager.IsValidCell(destination)) return false;

            if (gridManager.IsOccupied(destination)) return false;

            var occ = gridManager.GetOccupant(boxCell);
            if (occ == null || occ.Type != OccupantType.Box) return false;
            var box = occ as Box;
            if (box == null) return false;

            if (!gridManager.TryMove(boxCell, destination)) return false;

            box.MoveTo(destination);

            GameEvents.RaiseBoxPushed(box, boxCell);

            return true;
        }
    }
}