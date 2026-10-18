using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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

        /// <summary>Smoothed world-space velocity for enemy intercept prediction.</summary>
        public Vector3 Velocity { get; private set; }

        private Vector3 _prevPos;

        private BoxPushController pusher;
        private PullAbility puller;

        private static readonly GridCoord[] Directions =
        {
            new GridCoord(0, 1),   // +Z
            new GridCoord(1, 0),   // +X
            new GridCoord(0, -1),  // -Z
            new GridCoord(-1, 0)   // -X
        };

        private void Awake()
        {
            TryGetComponent(out pusher);
            TryGetComponent(out puller);

            if (gridManager == null)
                gridManager = FindAnyObjectByType<GridManager>();
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
            _prevPos = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 inst = (transform.position - _prevPos) / dt;
                Velocity = Vector3.Lerp(Velocity, inst, 1f - Mathf.Exp(-10f * dt));
                if (Velocity.sqrMagnitude < 0.0004f) Velocity = Vector3.zero;
            }
            _prevPos = transform.position;
        }

        private void Update()
        {
            if (isBusy) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            bool left = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            bool up = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool down = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            bool shiftHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            // ---- Pull (hold Shift + vertical): box in front moves into our old cell,
            // ---- we step back. Takes priority over normal push/walk.
            if (shiftHeld)
            {
                if (up || down)
                {
                    if (puller != null)
                    {
                        GridCoord forward = Directions[facingIndex];
                        GridCoord boxCell = Coord + forward;
                        GridCoord? dest = puller.TryPull(Coord, boxCell, forward);

                        if (dest.HasValue)
                        {
                            StartCoroutine(StepRoutine(dest.Value));
                        }
                    }
                    return;
                }
                // Shift held without vertical: allow turning below, don't walk.
            }

            // ---- Turning ----
            float turnInput = (right ? 1f : 0f) + (left ? -1f : 0f);
            if (Mathf.Abs(turnInput) > 0.5f)
            {
                int delta = turnInput > 0f ? 1 : -1;
                StartCoroutine(TurnRoutine(delta));
                return;
            }

            // ---- Movement ----
            float moveInput = (up ? 1f : 0f) + (down ? -1f : 0f);
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

                // --- Push attempt ---
                if (occ != null && occ.Type == OccupantType.Box)
                {
                    if (pusher != null && pusher.TryPush(target, dir))
                    {
                        StartCoroutine(StepRoutine(target));
                    }
                    else if (logSteps)
                    {
                        Debug.Log($"[GridPlayerController] Push failed at {target}");
                    }
                    return;
                }

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