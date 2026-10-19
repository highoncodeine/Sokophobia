using UnityEngine;
using Sokophobia.UI;

namespace Sokophobia.Grid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Sokophobia/Victory Trigger")]
    public class VictoryTrigger : MonoBehaviour
    {
        private BoxCollider _trigger;
        private readonly Collider[] _overlaps = new Collider[32];
        private bool _activated;
        private bool _reportedMissingController;

        private void Awake()
        {
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryActivate(other);
        }

        private void LateUpdate()
        {
            if (_activated || !_trigger.enabled || Time.timeScale <= 0f)
                return;

            // GridPlayerController moves its Transform directly rather than calling
            // CharacterController.Move, so also query overlaps instead of relying
            // solely on physics trigger callbacks. This also handles spawn-inside.
            Vector3 scale = transform.lossyScale;
            Vector3 halfSize = Vector3.Scale(_trigger.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 center = transform.TransformPoint(_trigger.center);
            int count = Physics.OverlapBoxNonAlloc(center, halfSize, _overlaps,
                transform.rotation, ~0, QueryTriggerInteraction.Collide);

            // Avoid missing the player in an unusually crowded trigger volume.
            Collider[] overlaps = count == _overlaps.Length
                ? Physics.OverlapBox(center, halfSize, transform.rotation, ~0, QueryTriggerInteraction.Collide)
                : _overlaps;
            if (overlaps != _overlaps)
                count = overlaps.Length;

            for (int i = 0; i < count && !_activated; i++)
                TryActivate(overlaps[i]);
        }

        private void TryActivate(Collider other)
        {
            if (!isActiveAndEnabled || _activated || !_trigger.enabled || Time.timeScale <= 0f)
                return;

            GridPlayerController player = other.GetComponentInParent<GridPlayerController>();
            if (player == null || !player.isActiveAndEnabled)
                return;

            if (!player.TryGetComponent<VictoryScreenController>(out var victory))
            {
                if (!_reportedMissingController)
                {
                    Debug.LogError("[VictoryTrigger] Add VictoryScreenController to the Player and assign VictoryScreen.png.", this);
                    _reportedMissingController = true;
                }
                return;
            }

            _activated = victory.ShowVictory();
        }
    }
}