using UnityEngine;

namespace Sokophobia.Interaction
{
    public class BatteryPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private float chargeAmount = 40f;
        private bool _collected = false;

        public bool CanInteract => !_collected;
        public string Prompt => "Pick up battery";

        public void Interact(GameObject interactor)
        {
            if (_collected) return;

            if (interactor.TryGetComponent<FlashlightBattery>(out var battery))
            {
                battery.AddCharge(chargeAmount);
                _collected = true;
                sfxSource.Play();
                gameObject.SetActive(false);
                Debug.Log($"Battery picked up! +{chargeAmount} charge.");
            }
        }
    }
}