using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
        [Tooltip("Drop a real clip here to replace the procedural thud.")]
        [SerializeField] private AudioClip pushClip;
        [SerializeField, Range(0f, 1f)] private float pushVolume = 0.8f;

        public GridCoord Coord { get; private set; }
        public OccupantType Type => OccupantType.Box;
        public Transform Transform => transform;

        private GridManager gridManager;
        private Coroutine moveRoutine;
        private NavMeshObstacle obstacle;
        private AudioSource audio;
        private static AudioClip cachedThud;

        private void Start()
        {
            if (gridManager == null)
                gridManager = FindAnyObjectByType<GridManager>();

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
                // Plate accounting belongs to PuzzleManager (grid-event bridge),
                // not here: every box would otherwise report "placed" at spawn.
            }

            SetupObstacle();
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

            // Enable-on-settle: don't carve a sweeping hole mid-slide.
            if (obstacle != null) obstacle.enabled = false;

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(LerpTo(WorldPosOf(target)));

            var noise = new NoiseEvent(
                transform.position,
                pushNoiseRadius,
                pushNoiseIntensity,
                NoiseSource.BoxPush
            );
            GameEvents.RaiseNoise(noise);

            PlayThud();
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

            if (obstacle != null) obstacle.enabled = true;
        }

        /// <summary>
        /// Audible thud for the push/pull the AI just heard about.
        /// Uses the assigned clip, else a generated 0.15s low thud (no assets needed).
        /// </summary>
        private void PlayThud()
        {
            if (audio == null)
            {
                if (!TryGetComponent(out audio))
                    audio = gameObject.AddComponent<AudioSource>();
                audio.spatialBlend = 1f;
                audio.playOnAwake = false;
            }

            AudioClip clip = pushClip != null ? pushClip : GetThud();
            if (clip != null)
                audio.PlayOneShot(clip, pushVolume);
        }

        private static AudioClip GetThud()
        {
            if (cachedThud != null) return cachedThud;

            const int rate = 22050;
            const float dur = 0.15f;
            int len = Mathf.RoundToInt(rate * dur);
            float[] data = new float[len];
            var rng = new System.Random(7);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float env = (1f - t) * (1f - t);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = Mathf.Sin(2f * Mathf.PI * 90f * t * dur);
                data[i] = (noise * 0.5f + tone * 0.5f) * env * 0.9f;
            }

            cachedThud = AudioClip.Create("PushThud", len, 1, rate, false);
            cachedThud.SetData(data, 0);
            return cachedThud;
        }
        /// <summary>
        /// NavMeshObstacle with carving, enabled only at rest (enable-on-settle).
        /// Added at runtime so the Crate prefab needs no YAML surgery.
        /// </summary>
        private void SetupObstacle()
        {
            if (!TryGetComponent(out obstacle))
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false; // we toggle enabled manually
                if (TryGetComponent<BoxCollider>(out var col))
                {
                    obstacle.center = col.center;
                    obstacle.size = col.size;
                }
            }
            else
            {
                obstacle.carving = true;
            }
            obstacle.enabled = true;
        }
    }
}