using System.Collections;
using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// First-person grid-aligned player controller.
    /// Moves one cell at a time via step-lerp, turns in 90° increments,
    /// and buffers one queued input while a movement is in progress.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GridPlayerController : MonoBehaviour, IGridOccupant
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Transform cameraPivot;

        [Header("Timing")]
        [SerializeField] private float moveDuration = 0.18f;
        [SerializeField] private float turnDuration = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool logSteps = false;

        // ---------- IGridOccupant ----------
        public GridCoord Coord { get; private set; }
        public OccupantType Type => OccupantType.Player;
        public Transform Transform => transform;

        // ---------- State ----------
        private int facingIndex = 0; // 0 = +Z, 1 = +X, 2 = -Z, 3 = -X
        private bool isBusy = false;

        private static readonly GridCoord[] Directions =
        {
            new GridCoord(0, 1),   // +Z
            new GridCoord(1, 0),   // +X
            new GridCoord(0, -1),  // -Z
            new GridCoord(-1, 0)   // -X
        };

        private void Awake()
        {
            if (gridManager == null)
            {
                Debug.LogError("[GridPlayerController] gridManager not assigned.");
                enabled = false;
                return;
            }

            Coord = gridManager.WorldToGrid(transform.position);

            if (!gridManager.TryPlace(Coord, this))
            {
                Debug.LogError($"[GridPlayerController] Could not register at {Coord}. " +
                               $"Valid: {gridManager.IsValidCell(Coord)}, " +
                               $"Occupied: {gridManager.IsOccupied(Coord)}");
            }

            transform.position = gridManager.GridToWorld(Coord);
            ApplyFacingInstant();
        }

        private void Update()
        {
            if (isBusy) return;

            // ---- Turning ----
            float turnInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(turnInput) > 0.5f)
            {
                int delta = turnInput > 0f ? 1 : -1;
                StartCoroutine(TurnRoutine(delta));
                return;
            }

            // ---- Movement ----
            float moveInput = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(moveInput) > 0.5f)
            {
                GridCoord forward = Directions[facingIndex];
                GridCoord dir = moveInput > 0f ? forward : -forward;
                TryStep(dir);
            }
        }

        // ---------- Movement ----------
        private void TryStep(GridCoord dir)
        {
            GridCoord target = Coord + dir;

            if (!gridManager.IsValidCell(target))
            {
                if (logSteps) Debug.Log($"[GridPlayerController] Blocked (invalid): {target}");
                return;
            }

            if (gridManager.IsOccupied(target))
            {
                var occ = gridManager.GetOccupant(target);
                // TODO: uncomment when BoxPushController lands on feature/box-interaction
                // if (occ != null && occ.Type == OccupantType.Box)
                // {
                //     var pusher = GetComponent<BoxPushController>();
                //     if (pusher != null && pusher.TryPush(target, dir))
                //     {
                //         StartCoroutine(StepRoutine(target));
                //     }
                //     return;
                // }

                if (logSteps) Debug.Log($"[GridPlayerController] Blocked (occupied by {occ?.Type}): {target}");
                return;
            }

            StartCoroutine(StepRoutine(target));
        }

        private IEnumerator StepRoutine(GridCoord target)
        {
            isBusy = true;

            if (!gridManager.TryMove(Coord, target))
            {
                Debug.LogWarning($"[GridPlayerController] TryMove failed: {Coord} -> {target}");
                isBusy = false;
                yield break;
            }

            GridCoord from = Coord;
            Coord = target;

            Vector3 startPos = transform.position;
            Vector3 endPos = gridManager.GridToWorld(target);

            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, endPos, t / moveDuration);
                yield return null;
            }
            transform.position = endPos;

            isBusy = false;
        }

        // ---------- Turning ----------
        private IEnumerator TurnRoutine(int direction)
        {
            isBusy = true;

            facingIndex = (facingIndex + direction + 4) % 4;

            Quaternion startRot = transform.rotation;
            Quaternion endRot = Quaternion.Euler(0f, facingIndex * 90f, 0f);

            float t = 0f;
            while (t < turnDuration)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, endRot, t / turnDuration);
                yield return null;
            }
            transform.rotation = endRot;

            isBusy = false;
        }

        private void ApplyFacingInstant()
        {
            transform.rotation = Quaternion.Euler(0f, facingIndex * 90f, 0f);
        }
    }
}