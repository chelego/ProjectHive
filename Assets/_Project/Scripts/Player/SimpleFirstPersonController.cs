using ProjectHive.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimpleFirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.2f;
        [SerializeField, Min(0f)] private float runSpeed = 7f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -24f;

        [Header("View")]
        [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;
        [SerializeField, Range(45f, 89f)] private float verticalLookLimit = 85f;

        private CharacterController characterController;
        private float cameraPitch;
        private float verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            UpdateView(mouse);
            UpdateMovement(keyboard);
        }

        public void Configure(Camera camera)
        {
            viewCamera = camera;
        }

        private void UpdateView(Mouse mouse)
        {
            bool canLook = GameRuntime.Instance == null
                ? Cursor.lockState == CursorLockMode.Locked
                : GameRuntime.Instance.IsCursorCaptured;

            if (!canLook || viewCamera == null)
                return;

            Vector2 lookDelta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0f, lookDelta.x, 0f, Space.Self);

            cameraPitch = Mathf.Clamp(cameraPitch - lookDelta.y, -verticalLookLimit, verticalLookLimit);
            viewCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        private void UpdateMovement(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 planarVelocity = (transform.forward * input.y + transform.right * input.x) * speed;

            if (characterController.isGrounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = -2f;

                if (keyboard.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
