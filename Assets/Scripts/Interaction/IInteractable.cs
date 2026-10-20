using UnityEngine;

namespace Sokophobia.Interaction
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        string Prompt { get; }
        void Interact(GameObject interactor);
    }
}