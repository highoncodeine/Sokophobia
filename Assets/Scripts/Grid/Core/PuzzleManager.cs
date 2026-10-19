using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Owns plate totals + box-on-plate progress. Plates stay non-occupants.
    /// Bridges GridManager occupancy events to GameEvents plate events,
    /// reviving OnBoxPlaced / OnBoxRemovedFromPlate for AI (Sokobot) and HUD.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class PuzzleManager : MonoBehaviour
    {
        public event Action<int, int> OnProgressChanged;
        public event Action OnPuzzleSolved;

        private readonly Dictionary<GridCoord, Plate> _plates = new();
        private readonly HashSet<GridCoord> _satisfied = new();

        private GridManager _grid;

        public int TotalPlates => _plates.Count;
        public int PlacedCount
        {
            get
            {
#if UNITY_EDITOR || DEBUG
                if (_completedForTesting)
                    return TotalPlates;
#endif
                return _satisfied.Count;
            }
        }
        public int RemainingPlates => Mathf.Max(0, TotalPlates - PlacedCount);
        public bool IsSolved { get; private set; }

#if UNITY_EDITOR || DEBUG
        private bool _completedForTesting;

        public void CompleteForTesting()
        {
            if (!Application.isPlaying || (!Application.isEditor && !Debug.isDebugBuild) || IsSolved)
                return;

            if (TotalPlates == 0)
            {
                Debug.LogWarning("[PuzzleManager] Cannot complete the puzzle: no plates are registered.", this);
                return;
            }

            _completedForTesting = true;
            CompletePuzzle();
            Debug.Log("[PuzzleManager] Puzzle completed using the testing shortcut.", this);
        }
#endif

        private void LateUpdate()
        {
            if (IsSolved || TotalPlates == 0 || PlacedCount != TotalPlates)
                return;

            CompletePuzzle();
        }

        private void CompletePuzzle()
        {
            if (IsSolved)
                return;

            IsSolved = true;
            Notify();
            OnPuzzleSolved?.Invoke();
        }

        private void Awake()
        {
            _grid = FindAnyObjectByType<GridManager>();
            if (_grid == null)
                Debug.LogError("[PuzzleManager] No GridManager found.");
        }

        private void OnEnable()
        {
            if (_grid != null)
            {
                _grid.OnOccupantPlaced += HandlePlaced;
                _grid.OnOccupantRemoved += HandleRemoved;
            }
        }

        private void OnDisable()
        {
            if (_grid != null)
            {
                _grid.OnOccupantPlaced -= HandlePlaced;
                _grid.OnOccupantRemoved -= HandleRemoved;
            }
        }

        public void RegisterPlate(Plate plate)
        {
            _plates[plate.Coord] = plate;
            // Initial scan: a box may already occupy this cell (registration order).
            if (_grid != null && _grid.GetOccupant(plate.Coord) is Box box && _satisfied.Add(plate.Coord))
            {
                GameEvents.RaiseBoxPlaced(box, plate.Coord);
            }
            Notify();
        }

        public void UnregisterPlate(Plate plate)
        {
            _plates.Remove(plate.Coord);
            _satisfied.Remove(plate.Coord);
            Notify();
        }

        private void HandlePlaced(GridCoord c, IGridOccupant o)
        {
            if (!_plates.ContainsKey(c) || _grid == null) return;

            // Post-move state: whoever now occupies the plate cell.
            var box = _grid.GetOccupant(c) as Box;
            if (box != null && _satisfied.Add(c))
            {
                GameEvents.RaiseBoxPlaced(box, c);
                Notify();
            }
        }

        private void HandleRemoved(GridCoord c, IGridOccupant o)
        {
            if (!_plates.ContainsKey(c)) return;

            // The removed occupant is passed through so subscribers get the real box.
            if (_satisfied.Remove(c))
            {
                GameEvents.RaiseBoxRemovedFromPlate(o as Box, c);
                Notify();
            }
        }

        private void Notify() => OnProgressChanged?.Invoke(PlacedCount, TotalPlates);
    }
}
