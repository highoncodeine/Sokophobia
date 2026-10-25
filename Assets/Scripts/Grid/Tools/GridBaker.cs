using UnityEngine;

namespace Sokophobia.Grid
{
    [DefaultExecutionOrder(-50)]
    public class GridBaker : MonoBehaviour
    {
        [Header("Bake Settings")]
        [Tooltip("Layers to treat as walls/obstacles. Set to a dedicated Wall layer.")]
        [SerializeField] private LayerMask wallLayers = ~0;

        [Tooltip("How high above the cell to start the raycast.")]
        [SerializeField] private float rayStartHeight = 5f;

        [Tooltip("How far down to cast.")]
        [SerializeField] private float rayLength = 10f;

        [Tooltip("Minimum height above cell center for a hit to count as a wall.")]
        [SerializeField] private float wallMinHeight = 1f;

        [Header("Runtime")]
        [Tooltip("Automatically bake on Awake. Recommended for grid-logic blocks.")]
        [SerializeField] private bool bakeOnAwake = true;

        public bool BakeOnAwake => bakeOnAwake;

        [ContextMenu("Bake Grid From Level")]
        public void Bake()
        {
            var grid = GetComponent<GridManager>();
            if (grid == null)
            {
                Debug.LogError("[GridBaker] No GridManager on this GameObject.");
                return;
            }

            grid.ResetCellsToFloor();

            int walls = 0;
            int floors = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var coord = new GridCoord(x, z);
                    Vector3 cellCenter = grid.GridToWorld(coord);
                    Vector3 rayOrigin = cellCenter + Vector3.up * rayStartHeight;

                    if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayLength, wallLayers))
                    {
                        float hitHeight = hit.point.y - cellCenter.y;
                        if (hitHeight > wallMinHeight)
                        {
                            grid.SetCellType(coord, CellType.Wall);
                            walls++;
                            continue;
                        }
                    }

                    floors++;
                }
            }

            Debug.Log($"[GridBaker] Baked {grid.Width}x{grid.Height} grid: {walls} walls, {floors} floors.");
        }
    }
}