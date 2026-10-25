using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokophobia.Grid
{
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

        [SerializeField] private AudioSource runSfx;

        public GridCoord Coord { get; private set; }
        public OccupantType Type => OccupantType.Player;
        public Transform Transform => transform;

        private float yaw = 0f;
        private float pitch = 0f;
        private int facingIndex = 0;
        private bool isBusy = false;

        public Vector3 Velocity { get; private set; }
        private Vector3 _prevPos;

        private BoxPushController pusher;
        private PullAbility puller;

        private static readonly GridCoord[] Directions =
        {
            new GridCoord(0, 1),
            new GridCoord(1, 0),
            new GridCoord(0, -1),
            new GridCoord(-1, 0)
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

            float forwardInput = (up ? 1f : 0f) + (down ? -1f : 0f);
            float strafeInput = (right ? 1f : 0f) + (left ? -1f : 0f);

            GridCoord dir = default;
            bool hasInput = false;

            facingIndex = GetNearestCardinalIndex();

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

        private void HandleMouseLook()
        {
            if (Mouse.current == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            
            yaw += delta.x * mouseSensitivity;
            
            pitch += (invertY ? delta.y : -delta.y) * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            ApplyLook();
        }

        private void ApplyLook()
        {
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private int GetNearestCardinalIndex()
        {
            float normalizedYaw = yaw % 360f;
            if (normalizedYaw < 0f) normalizedYaw += 360f;

            return Mathf.RoundToInt(normalizedYaw / 90f) % 4;
        }

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

            if (runSfx != null && !runSfx.isPlaying)
            {
                runSfx.Play();
            }

            if (!gridManager.TryMove(Coord, target))
            {
                Debug.LogWarning($"[GridPlayerController] TryMove failed: {Coord} -> {target}");
                isBusy = false;
                
                if (runSfx != null && runSfx.isPlaying)
                    runSfx.Pause();
                    
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

            yield return null;

            if (!isBusy && runSfx != null && runSfx.isPlaying)
            {
                runSfx.Pause(); 
            }
        }
    }
}