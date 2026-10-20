using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokophobia.Grid
{
    /// <summary>
    /// First-person grid-aligned player controller.
    /// Free mouse look (FPS style), but moves exactly one cell at a time.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GridPlayerController : MonoBehaviour, IGridOccupant
    {
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Transform cameraPivot;

        [Header("Movement Timing")]
        [SerializeField] private float moveDuration = 0.18f;

        [Header("Mouse Look")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float pitchMin = -80f;
        [SerializeField] private float pitchMax = 80f;
        [SerializeField] private bool invertY = false;

        [Header("Debug")]
        [SerializeField] private bool logSteps = false;

        // ---------- IGridOccupant ----------
        public GridCoord Coord { get; private set; }
        public OccupantType Type => OccupantType.Player;
        public Transform Transform => transform;

        // ---------- State ----------
        private float yaw = 0f;          // Free-look horizontal angle
        private float pitch = 0f;        // Free-look vertical angle
        private int facingIndex = 0;     // Derived nearest cardinal (0=+Z, 1=+X, 2=-Z, 3=-X)
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
            
            yaw = transform.eulerAngles.y;
            ApplyLook();
            
            _prevPos = transform.position;

            // Lock cursor for FPS mouse look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            if (Time.timeScale <= 0f) return;

            HandleMouseLook();

            if (isBusy) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            bool left = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            bool up = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool down = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            bool shiftHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            // ---- Pull (hold Shift + S/Down): box in front moves into our old cell ----
            if (shiftHeld && down)
            {
                if (puller != null)
                {
                    facingIndex = GetNearestCardinalIndex();
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

            // ---- Movement & Strafing ----
            float forwardInput = (up ? 1f : 0f) + (down ? -1f : 0f);
            float strafeInput = (right ? 1f : 0f) + (left ? -1f : 0f);

            GridCoord dir = default;
            bool hasInput = false;

            facingIndex = GetNearestCardinalIndex(); // Update facing based on mouse aim

            if (Mathf.Abs(forwardInput) > 0.5f)
            {
                GridCoord forward = Directions[facingIndex];
                dir = forwardInput > 0f ? forward : -forward;
                hasInput = true;
            }
            else if (Mathf.Abs(strafeInput) > 0.5f)
            {
                int rightIdx = (facingIndex + 1) % 4;
                int leftIdx = (facingIndex + 3) % 4;
                dir = strafeInput > 0f ? Directions[rightIdx] : Directions[leftIdx];
                hasInput = true;
            }

            if (hasInput)
            {
                TryStep(dir);
            }
        }

        // ---------- Mouse Look ----------
        private void HandleMouseLook()
        {
            if (Mouse.current == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            
            yaw += delta.x * mouseSensitivity;
            
            // Invert Y logic based on FPS standards
            pitch += (invertY ? delta.y : -delta.y) * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            ApplyLook();
        }

        private void ApplyLook()
        {
            // Yaw rotates the whole player body
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Pitch only rotates the camera pivot
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// Convert free yaw into nearest 90° cardinal (0=+Z, 1=+X, 2=-Z, 3=-X).
        /// </summary>
        private int GetNearestCardinalIndex()
        {
            float normalizedYaw = yaw % 360f;
            if (normalizedYaw < 0f) normalizedYaw += 360f;

            return Mathf.RoundToInt(normalizedYaw / 90f) % 4;
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
    }
}