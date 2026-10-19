using System.Collections;
using UnityEngine;
using Sokophobia.Grid;

namespace Sokophobia.Enemy
{
    /// <summary>
    /// Holds Sokobot dormant until the first crate is placed on a pressure plate,
    /// then spawns (activates) it at its authored scene position.
    /// Progress comes from PuzzleManager, so moving a crate off the plate later
    /// does not respawn the enemy: only the first placement matters.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Enemy/Enemy Spawner")]
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy")]
        [Tooltip("The dormant enemy to activate. Falls back to the first SokobotController in the scene.")]
        [SerializeField] private GameObject enemy;

        [Header("Timing")]
        [Tooltip("Small beat between the crate settling on the plate and the enemy appearing.")]
        [SerializeField, Min(0f)] private float spawnDelay = 0.2f;

        private PuzzleManager _puzzle;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Coroutine _spawnRoutine;
        private bool _spawned;

        public bool HasSpawned => _spawned;
        public GameObject Enemy => enemy;

        private void Awake()
        {
            if (enemy == null)
            {
                var bot = FindAnyObjectByType<SokobotController>(FindObjectsInactive.Include);
                if (bot != null)
                    enemy = bot.gameObject;
            }

            if (enemy == null)
            {
                Debug.LogError("[EnemySpawner] No enemy assigned and no SokobotController found.", this);
                enabled = false;
                return;
            }

            _spawnPosition = enemy.transform.position;
            _spawnRotation = enemy.transform.rotation;

            // Ensure the enemy does not run its AI or show its body before the cue.
            enemy.SetActive(false);
        }

        private void OnEnable()
        {
            if (_spawned)
                return;

            _puzzle = FindAnyObjectByType<PuzzleManager>();
            if (_puzzle == null)
            {
                Debug.LogError("[EnemySpawner] No PuzzleManager found.", this);
                enabled = false;
                return;
            }

            _puzzle.OnProgressChanged += HandleProgress;

            // Covers scenes that already start with a crate on a plate.
            if (_puzzle.PlacedCount > 0)
                HandleProgress(_puzzle.PlacedCount, _puzzle.TotalPlates);
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        private void HandleProgress(int placed, int total)
        {
            if (_spawned || placed <= 0)
                return;

            if (spawnDelay <= 0f)
            {
                Spawn();
                return;
            }

            if (_spawnRoutine == null)
                _spawnRoutine = StartCoroutine(SpawnAfterDelay());
        }

        private IEnumerator SpawnAfterDelay()
        {
            yield return new WaitForSeconds(spawnDelay);
            _spawnRoutine = null;
            Spawn();
        }

        private void Spawn()
        {
            if (_spawned || enemy == null)
                return;

            _spawned = true;
            Unsubscribe();

            // Snap to the authored spawn point even if something nudged the
            // dormant transform while it was inactive.
            enemy.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            enemy.SetActive(true);
        }

        private void Unsubscribe()
        {
            if (_puzzle != null)
                _puzzle.OnProgressChanged -= HandleProgress;
        }
    }
}
