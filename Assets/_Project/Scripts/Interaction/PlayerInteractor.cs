using ProjectHive.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3.2f;

        private IInteractable focusedInteractable;
        private InteractionContext currentContext;

        public string CurrentPrompt =>
            focusedInteractable != null && focusedInteractable.CanInteract(in currentContext)
                ? focusedInteractable.InteractionPrompt
                : string.Empty;

        public void Configure(Camera camera)
        {
            viewCamera = camera;
        }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            RefreshFocus();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.eKey.wasPressedThisFrame &&
                focusedInteractable != null &&
                focusedInteractable.CanInteract(in currentContext))
            {
                focusedInteractable.Interact(in currentContext);
            }
        }

        private void RefreshFocus()
        {
            focusedInteractable = null;
            if (viewCamera == null)
                return;

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            currentContext = new InteractionContext(gameObject, ray.origin, ray.direction);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            focusedInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }
    }
}
