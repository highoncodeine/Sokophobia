using System.Collections;
using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// A pushable/pullable Sokoban box. Implements IGridOccupant.
    /// Logical position updates instantly; visual position lerps.
    /// </summary>
    public class Box : MonoBehaviour, IGridOccupant
    {
        [Header("Visual")]
        [SerializeField] private float moveDuration = 0.15f;

        [Header("Audio (optional)")]
        [SerializeField] private float pushNoiseRadius = 12f;
        [SerializeField] private float pushNoiseIntensity = 1f;

        public GridCoord Coord { get; private set; }
        public OccupantType Type => OccupantType.Box;
        public Transform Transform => transform;

        private GridManager gridManager;
        private Coroutine moveRoutine;

        /// <summary>
        /// Called by GridManager (or the pusher) right after TryPlace on this box.
        /// </summary>
        public void Init(GridManager manager, GridCoord startCoord)
        {
            gridManager = manager;
            Coord = startCoord;
            transform.position = gridManager.GridToWorld(startCoord);
        }

        /// <summary>
        /// Called by the push/pull controller after the grid has already updated.
        /// Updates logical Coord and lerps the visual.
        /// </summary>
        public void MoveTo(GridCoord target)
        {
            Coord = target;

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(LerpTo(gridManager.GridToWorld(target)));

            // Emit noise at the new cell
            var noise = new NoiseEvent(
                transform.position,
                pushNoiseRadius,
                pushNoiseIntensity,
                NoiseSource.BoxPush
            );
            GameEvents.RaiseNoise(noise);
        }

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