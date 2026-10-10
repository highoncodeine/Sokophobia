using System.Collections;
using UnityEngine;

namespace Sokophobia.Grid
{
    public class Box : MonoBehaviour, IGridOccupant
    {
        [Header("Visual")]
        [SerializeField] private float moveDuration = 0.15f;
        [SerializeField] private float gridYOffset = 0.5f;

        [Header("Audio (optional)")]
        [SerializeField] private float pushNoiseRadius = 12f;
        [SerializeField] private float pushNoiseIntensity = 1f;

        public GridCoord Coord { get; private set; }
        public OccupantType Type => OccupantType.Box;
        public Transform Transform => transform;

        private GridManager gridManager;
        private Coroutine moveRoutine;

        private void Start()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();

            if (gridManager == null)
            {
                Debug.LogError("[Box] No GridManager found.");
                return;
            }

            Coord = gridManager.WorldToGrid(transform.position);

            if (!gridManager.TryPlace(Coord, this))
            {
                Debug.LogError($"[Box] Could not register at {Coord}. " +
                               $"Valid: {gridManager.IsValidCell(Coord)}, " +
                               $"Occupied: {gridManager.IsOccupied(Coord)}");
            }
            else
            {
                transform.position = WorldPosOf(Coord);
                GameEvents.RaiseBoxPlaced(this, Coord);
            }
        }

        public void Init(GridManager manager, GridCoord startCoord)
        {
            gridManager = manager;
            Coord = startCoord;
            transform.position = WorldPosOf(startCoord);
        }

        public void MoveTo(GridCoord target)
        {
            Coord = target;

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(LerpTo(WorldPosOf(target)));

            var noise = new NoiseEvent(
                transform.position,
                pushNoiseRadius,
                pushNoiseIntensity,
                NoiseSource.BoxPush
            );
            GameEvents.RaiseNoise(noise);
        }

        private Vector3 WorldPosOf(GridCoord coord)
            => gridManager.GridToWorld(coord) + new Vector3(0f, gridYOffset, 0f);

        private IEnumerator LerpTo(Vector3 targetPos)
        {
            Vector3 startPos = transform.position;
            float t = 0f;

            while (t < moveDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, t / moveDuration);
                yield return null;
            }

            transform.position = targetPos;
            moveRoutine = null;
        }
    }
}