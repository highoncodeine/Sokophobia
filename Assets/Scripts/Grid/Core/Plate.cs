using UnityEngine;
using Sokophobia.Grid;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Pressure-plate marker. Registers its cell with PuzzleManager; never an occupant,
    /// so boxes can still be pushed onto this cell (one occupant per cell rule).
    /// Enforces trigger collider at runtime so the prefab stays untouched.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Plate : MonoBehaviour
    {
        public GridCoord Coord { get; private set; }

        private PuzzleManager _puzzle;
        private GridManager _grid;
        private bool _registered;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void Start()
        {
            _grid = FindAnyObjectByType<GridManager>();
            _puzzle = FindAnyObjectByType<PuzzleManager>();

            if (_grid == null)
            {
                Debug.LogError("[Plate] No GridManager found.");
                return;
            }
            if (_puzzle == null)
            {
                Debug.LogError("[Plate] No PuzzleManager found. Add one to the scene.");
                return;
            }

            Coord = _grid.WorldToGrid(transform.position);
            _puzzle.RegisterPlate(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered && _puzzle != null)
                _puzzle.UnregisterPlate(this);
        }
    }
}
