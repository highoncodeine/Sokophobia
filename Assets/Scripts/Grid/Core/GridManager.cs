using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Authoritative grid system. Owns cell types, occupancy, and coordinate conversion.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Dimensions")]
        [SerializeField] private int width = 20;
        [SerializeField] private int height = 20;

        [Header("Cell Size & Origin")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 origin = Vector3.zero;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private CellType[,] cells;
        private readonly Dictionary<GridCoord, IGridOccupant> occupants = new();

        public event Action<GridCoord, IGridOccupant> OnOccupantPlaced;
        public event Action<GridCoord, IGridOccupant> OnOccupantRemoved;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;

        private bool initialized = false;

        // ---------- Lifecycle ----------
        private void EnsureInitialized()
        {
            if (initialized) return;

            cells = new CellType[width, height];
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    cells[x, z] = CellType.Floor;

            initialized = true;
        }

        private void Awake()
        {
            EnsureInitialized();

            // Bake walls from level geometry before any occupant registers.
            var baker = GetComponent<GridBaker>();
            if (baker != null)
                baker.Bake();
            else
                Debug.LogWarning("[GridManager] No GridBaker on this GameObject — walls will not be auto-baked.");
        }

        // ---------- Coordinate Conversion ----------
        public Vector3 GridToWorld(GridCoord c)
            => origin + new Vector3(c.x * cellSize, 0f, c.z * cellSize);

        public GridCoord WorldToGrid(Vector3 world)
        {
            Vector3 local = world - origin;
            return new GridCoord(
                Mathf.RoundToInt(local.x / cellSize),
                Mathf.RoundToInt(local.z / cellSize));
        }

        // ---------- Cell Queries ----------
        public bool IsInBounds(GridCoord c)
        {
            EnsureInitialized();
            return c.x >= 0 && c.x < width && c.z >= 0 && c.z < height;
        }

        public bool IsValidCell(GridCoord c)
        {
            EnsureInitialized();
            return IsInBounds(c) && cells[c.x, c.z] == CellType.Floor;
        }

        public CellType GetCellType(GridCoord c)
        {
            EnsureInitialized();
            return IsInBounds(c) ? cells[c.x, c.z] : CellType.Void;
        }

        public void SetCellType(GridCoord c, CellType type)
        {
            EnsureInitialized();
            if (!IsInBounds(c)) return;
            cells[c.x, c.z] = type;
        }

        // ---------- Occupancy ----------
        public bool IsOccupied(GridCoord c) => occupants.ContainsKey(c);

        public IGridOccupant GetOccupant(GridCoord c)
            => occupants.TryGetValue(c, out var o) ? o : null;

        public bool TryPlace(GridCoord c, IGridOccupant occupant)
        {
            if (!IsValidCell(c) || IsOccupied(c)) return false;
            occupants[c] = occupant;
            OnOccupantPlaced?.Invoke(c, occupant);
            return true;
        }

        public bool TryRemove(GridCoord c)
        {
            if (!occupants.TryGetValue(c, out var o)) return false;
            occupants.Remove(c);
            OnOccupantRemoved?.Invoke(c, o);
            return true;
        }

        public bool TryMove(GridCoord from, GridCoord to)
        {
            if (!occupants.TryGetValue(from, out var o)) return false;
            if (!IsValidCell(to) || IsOccupied(to)) return false;

            occupants.Remove(from);
            occupants[to] = o;

            OnOccupantRemoved?.Invoke(from, o);
            OnOccupantPlaced?.Invoke(to, o);
            return true;
        }

        // ---------- Debug ----------
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Guard against calling before Awake (e.g., in edit mode)
            if (cells == null) return;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    GridCoord c = new GridCoord(x, z);
                    Vector3 center = GridToWorld(c);

                    switch (cells[x, z])
                    {
                        case CellType.Wall:  Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f); break;
                        case CellType.Floor: Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.15f); break;
                        default:             Gizmos.color = Color.clear; break;
                    }

                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.05f, cellSize));

                    if (occupants.TryGetValue(c, out var o))
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(center + Vector3.up * 0.5f, 0.2f);
                    }
                }
            }
        }

        /// <summary>
        /// Editor-time utility: wipe all cells to Floor before re-baking.
        /// </summary>
        public void ResetCellsToFloor()
        {
            EnsureInitialized();
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    cells[x, z] = CellType.Floor;
        }

        /// <summary>
        /// Editor-time utility: expose a public read-only view for baking.
        /// </summary>
        public bool IsInBounds(int x, int z)
            => x >= 0 && x < width && z >= 0 && z < height;

    }
}