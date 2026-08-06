using ProjectHive.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float range = 2.5f;
        [SerializeField] private float radius = 0.2f;
        [SerializeField] private LayerMask interactMask = ~0;

        private IInteractable current;
        private InteractionContext currentContext;

        public string CurrentPrompt => current != null ? current.InteractionPrompt : string.Empty;
        public bool HasTarget => current != null;

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        private void Update()
        {
            UpdateTarget();

            if (current != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                current.Interact(in currentContext);
        }

        private void UpdateTarget()
        {
            current = null;

            Vector3 origin = viewCamera != null ? viewCamera.transform.position : transform.position + Vector3.up * 1.6f;
            Vector3 forward = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            currentContext = new InteractionContext(gameObject, origin, forward);

            if (!Physics.SphereCast(origin, radius, forward, out RaycastHit hit, range, interactMask, QueryTriggerInteraction.Collide))
                return;

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(in currentContext))
                return;

            current = interactable;
        }

        private void OnValidate()
        {
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.01f, radius);
        }
    }
}
