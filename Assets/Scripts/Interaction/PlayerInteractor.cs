using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokophobia.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactRange = 2.5f;
        [SerializeField] private LayerMask interactMask;

        public bool HasTarget { get; private set; }
        public string CurrentPrompt { get; private set; }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Update()
        {
            HasTarget = false;
            CurrentPrompt = "";

            if (Time.timeScale <= 0f || playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.SphereCast(ray, 0.15f, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null && interactable.CanInteract)
                {
                    HasTarget = true;
                    CurrentPrompt = interactable.Prompt;

                    if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        interactable.Interact(gameObject);
                    }
                }
            }
        }
    }
}