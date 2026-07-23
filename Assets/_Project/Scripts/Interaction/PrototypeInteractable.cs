using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Prototype
{
    public abstract class PrototypeInteractable : MonoBehaviour, IInteractable
    {
        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact();

        string IInteractable.InteractionPrompt => Prompt;
        bool IInteractable.CanInteract(in InteractionContext context) => CanInteract;
        void IInteractable.Interact(in InteractionContext context) => Interact();
    }
}
