using System;
using ProjectHive.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Player
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonMotor : MonoBehaviour, ILocomotionStateProvider
    {
        [Header("View")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool lockCursor = true;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float crouchSpeed = 2.4f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float airAcceleration = 7f;
        [SerializeField] private float deceleration = 28f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundStickVelocity = -2f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.1f;
        [SerializeField] private float crouchLerpSpeed = 12f;
        [SerializeField] private LayerMask standBlockMask = ~0;

        [Header("Slide")]
        [SerializeField] private float slideStartSpeed = 8.5f;
        [SerializeField] private float slideDuration = 0.7f;
        [SerializeField] private float slideFriction = 10f;
        [SerializeField] private float slideMinSprintSpeed = 5.5f;
        [SerializeField] private float slideSteerStrength = 1.8f;

        [Header("Parkour")]
        [SerializeField] private LayerMask parkourMask = ~0;
        [SerializeField] private float parkourCheckDistance = 1.35f;
        [SerializeField] private float parkourCheckRadius = 0.18f;
        [SerializeField] private float vaultMaxHeight = 1.1f;
        [SerializeField] private float mantleMinHeight = 1.05f;
        [SerializeField] private float mantleMaxHeight = 2.05f;
        [SerializeField] private float vaultLandingForwardOffset = 1.2f;
        [SerializeField] private float mantleLandingForwardOffset = 0.45f;
        [SerializeField] private float vaultDuration = 0.32f;
        [SerializeField] private float mantleDuration = 0.55f;
        [SerializeField] private float sprintParkourDurationMultiplier = 0.6f;
        [SerializeField] private float walkParkourDurationMultiplier = 1.35f;
        [SerializeField] private float vaultArcHeight = 0.55f;
        [SerializeField] private float windowSillMaxHeight = 1.45f;
        [SerializeField] private float windowPassThroughForwardOffset = 1.45f;
        [SerializeField] private float windowPassThroughDuration = 0.42f;
        [SerializeField] private float landingClearance = 0.04f;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;
        private float lastGroundedTime;
        private float lastJumpPressedTime;
        private float slideTimeRemaining;
        private float parkourElapsed;
        private float parkourDuration;
        private float parkourArcHeight;
        private bool parkourIgnoresControllerCollision;
        private bool crouchHeld;
        private Vector3 horizontalVelocity;
        private Vector3 slideVelocity;
        private Vector3 parkourStartPosition;
        private Vector3 parkourTargetPosition;
        private readonly Collider[] standCheckHits = new Collider[8];
        private readonly Collider[] parkourClearanceHits = new Collider[12];

        public PlayerMoveState MoveState { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsSliding => MoveState == PlayerMoveState.Slide;
        public bool IsParkouring => MoveState == PlayerMoveState.Vault || MoveState == PlayerMoveState.Mantle;
        public Vector3 Velocity => controller != null ? controller.velocity : Vector3.zero;
        public PlayerLocomotionState LocomotionState => ToLocomotionState(MoveState);
        public event Action<LocomotionStateChange> LocomotionStateChanged;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraRoot == null && Camera.main != null)
                cameraRoot = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (!lockCursor)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (controller != null && !controller.enabled)
                controller.enabled = true;

            if (!lockCursor)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            UpdateLook();
            UpdateMovement();
            UpdateCrouchHeight();
        }

        private void UpdateLook()
        {
            if (Mouse.current == null)
                return;

            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(Vector3.up, delta.x, Space.World);

            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            if (cameraRoot != null)
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            if (IsParkouring)
            {
                UpdateParkourTraversal();
                return;
            }

            Vector2 input = ReadMoveInput();
            bool grounded = controller.isGrounded;
            if (grounded)
                lastGroundedTime = Time.time;

            if (grounded && verticalVelocity < 0f)
                verticalVelocity = groundStickVelocity;

            bool crouchPressed = WasCrouchPressedThisFrame();
            bool sprintHeld = IsSprintHeld();
            bool jumpPressed = WasJumpPressedThisFrame();
            crouchHeld = IsCrouchHeld();
            if (jumpPressed)
                lastJumpPressedTime = Time.time;

            bool wantsSprint = CanSprint(input, sprintHeld);
            if (jumpPressed && grounded && TryStartParkour(wantsSprint))
                return;

            if (CanStartSlide(grounded, crouchPressed, wantsSprint))
                StartSlide(input);

            Vector3 move = transform.right * input.x + transform.forward * input.y;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            IsSprinting = MoveState != PlayerMoveState.Slide && wantsSprint && !crouchHeld;
            IsCrouching = MoveState == PlayerMoveState.Slide || crouchHeld || !CanStand();

            if (MoveState == PlayerMoveState.Slide)
                UpdateSlide(move);
            else
                UpdateHorizontalVelocity(move, grounded);

            if (CanConsumeJump())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastJumpPressedTime = -999f;
                slideTimeRemaining = 0f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            UpdateMoveState(input, controller.isGrounded);
        }

        private Vector2 ReadMoveInput()
        {
            if (Keyboard.current == null)
                return Vector2.zero;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed)
                input.y += 1f;
            if (Keyboard.current.sKey.isPressed)
                input.y -= 1f;
            if (Keyboard.current.dKey.isPressed)
                input.x += 1f;
            if (Keyboard.current.aKey.isPressed)
                input.x -= 1f;

            return Vector2.ClampMagnitude(input, 1f);
        }

        private bool CanSprint(Vector2 input, bool sprintHeld)
        {
            if (MoveState == PlayerMoveState.Slide || Keyboard.current == null)
                return false;

            return sprintHeld && input.y > 0.1f;
        }

        private bool IsSprintHeld()
        {
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        }

        private bool IsCrouchHeld()
        {
            if (Keyboard.current == null)
                return false;

            return Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
        }

        private bool WasCrouchPressedThisFrame()
        {
            if (Keyboard.current == null)
                return false;

            return Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame;
        }

        private bool WasJumpPressedThisFrame()
        {
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        private bool CanConsumeJump()
        {
            return Time.time - lastGroundedTime <= coyoteTime &&
                   Time.time - lastJumpPressedTime <= jumpBufferTime &&
                   MoveState != PlayerMoveState.Slide;
        }

        private bool CanStartSlide(bool grounded, bool crouchPressed, bool wantsSprint)
        {
            return grounded &&
                   crouchPressed &&
                   wantsSprint &&
                   horizontalVelocity.magnitude >= slideMinSprintSpeed;
        }

        private void StartSlide(Vector2 input)
        {
            Vector3 forward = input.sqrMagnitude > 0.01f
                ? (transform.right * input.x + transform.forward * input.y).normalized
                : transform.forward;

            SetMoveState(PlayerMoveState.Slide);
            slideTimeRemaining = slideDuration;
            slideVelocity = forward * Mathf.Max(slideStartSpeed, horizontalVelocity.magnitude);
            horizontalVelocity = slideVelocity;
        }

        private bool TryStartParkour(bool sprintingIntoParkour)
        {
            if (IsCrouching || controller == null)
                return false;

            Vector3 forward = transform.forward;
            if (!TryFindFrontParkourObstacle(forward, out RaycastHit frontHit))
                return false;

            if (frontHit.collider.transform == transform || frontHit.collider.transform.IsChildOf(transform))
                return false;

            if (TryStartWindowPassage(frontHit, forward, sprintingIntoParkour))
                return true;

            if (!TryFindObstacleTop(frontHit, forward, out RaycastHit topHit))
                return false;

            float obstacleHeight = topHit.point.y - transform.position.y;
            if (obstacleHeight <= 0.15f || obstacleHeight > mantleMaxHeight)
                return false;

            if (obstacleHeight <= vaultMaxHeight &&
                TryFindVaultLanding(frontHit, forward, out Vector3 vaultLanding) &&
                HasStandingClearance(vaultLanding, frontHit.collider))
            {
                float arcHeight = Mathf.Max(vaultArcHeight, obstacleHeight + 0.25f);
                StartParkour(PlayerMoveState.Vault, vaultLanding, GetParkourDuration(vaultDuration, sprintingIntoParkour), arcHeight, false);
                return true;
            }

            if (obstacleHeight >= mantleMinHeight)
            {
                Vector3 mantleLanding = topHit.point + forward * mantleLandingForwardOffset;
                mantleLanding.y += landingClearance;
                if (HasStandingClearance(mantleLanding, frontHit.collider))
                {
                    StartParkour(PlayerMoveState.Mantle, mantleLanding, GetParkourDuration(mantleDuration, sprintingIntoParkour), 0f, false);
                    return true;
                }
            }

            return false;
        }

        private bool TryStartWindowPassage(RaycastHit frontHit, Vector3 forward, bool sprintingIntoParkour)
        {
            ParkourPrototypeObstacle window = frontHit.collider.GetComponentInParent<ParkourPrototypeObstacle>();
            if (window == null || window.Kind != ParkourPrototypeObstacleKind.WindowPassage)
                return false;

            Collider sill = FindWindowSill(window.transform);
            if (sill == null)
                sill = frontHit.collider;

            float sillHeight = sill.bounds.max.y - transform.position.y;
            if (sillHeight <= 0.15f || sillHeight > windowSillMaxHeight)
                return false;

            Vector3 landing = transform.position + forward * (frontHit.distance + windowPassThroughForwardOffset);
            landing.y += landingClearance;

            if (!HasCrouchingClearance(landing, sill))
                return false;

            float arcHeight = Mathf.Clamp(sillHeight + 0.05f, 0.35f, vaultArcHeight);
            StartParkour(PlayerMoveState.Vault, landing, GetParkourDuration(windowPassThroughDuration, sprintingIntoParkour), arcHeight, true);
            return true;
        }

        private float GetParkourDuration(float baseDuration, bool sprintingIntoParkour)
        {
            float multiplier = sprintingIntoParkour ? sprintParkourDurationMultiplier : walkParkourDurationMultiplier;
            return baseDuration * multiplier;
        }

        private static Collider FindWindowSill(Transform root)
        {
            Transform sillTransform = root.Find("Sill");
            return sillTransform != null ? sillTransform.GetComponent<Collider>() : null;
        }

        private bool TryFindFrontParkourObstacle(Vector3 forward, out RaycastHit frontHit)
        {
            Vector3 highOrigin = transform.position + Vector3.up * Mathf.Min(standingHeight * 0.55f, 1.05f);
            if (Physics.SphereCast(
                    highOrigin,
                    parkourCheckRadius,
                    forward,
                    out frontHit,
                    parkourCheckDistance,
                    parkourMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            Vector3 lowOrigin = transform.position + Vector3.up * Mathf.Min(crouchingHeight * 0.5f, 0.65f);
            return Physics.SphereCast(
                lowOrigin,
                parkourCheckRadius,
                forward,
                out frontHit,
                parkourCheckDistance,
                parkourMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool TryFindObstacleTop(RaycastHit frontHit, Vector3 forward, out RaycastHit topHit)
        {
            Bounds frontBounds = frontHit.collider.bounds;
            float directTopHeight = frontBounds.max.y - transform.position.y;
            if (directTopHeight > 0.15f && directTopHeight <= mantleMaxHeight)
            {
                topHit = frontHit;
                topHit.point = new Vector3(frontHit.point.x, frontBounds.max.y, frontHit.point.z);
                return true;
            }

            Vector3 topProbeOrigin = frontHit.point + forward * 0.15f + Vector3.up * (mantleMaxHeight + 0.25f);
            float topProbeDistance = mantleMaxHeight + 0.55f;

            RaycastHit[] hits = Physics.RaycastAll(
                topProbeOrigin,
                Vector3.down,
                topProbeDistance,
                parkourMask,
                QueryTriggerInteraction.Ignore);

            float bestHeight = float.MaxValue;
            topHit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                float height = hit.point.y - transform.position.y;
                if (height <= 0.15f || height > mantleMaxHeight || height >= bestHeight)
                    continue;

                topHit = hit;
                bestHeight = height;
                found = true;
            }

            return found;
        }

        private bool TryFindVaultLanding(RaycastHit frontHit, Vector3 forward, out Vector3 landing)
        {
            Vector3 landingProbeOrigin = transform.position +
                                         forward * (frontHit.distance + vaultLandingForwardOffset) +
                                         Vector3.up * (vaultMaxHeight + 0.8f);

            if (Physics.Raycast(
                    landingProbeOrigin,
                    Vector3.down,
                    out RaycastHit groundHit,
                    vaultMaxHeight + 1.4f,
                    parkourMask,
                    QueryTriggerInteraction.Ignore))
            {
                landing = groundHit.point + Vector3.up * landingClearance;
                return true;
            }

            landing = transform.position + forward * (frontHit.distance + vaultLandingForwardOffset);
            landing.y += landingClearance;
            return true;
        }

        private bool HasStandingClearance(Vector3 footPosition, Collider obstacle)
        {
            return HasCapsuleClearance(footPosition, standingHeight, obstacle);
        }

        private bool HasCrouchingClearance(Vector3 footPosition, Collider obstacle)
        {
            return HasCapsuleClearance(footPosition, crouchingHeight, obstacle);
        }

        private bool HasCapsuleClearance(Vector3 footPosition, float height, Collider obstacle)
        {
            Vector3 bottom = footPosition + Vector3.up * controller.radius;
            Vector3 top = footPosition + Vector3.up * (height - controller.radius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                controller.radius * 0.95f,
                parkourClearanceHits,
                parkourMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = parkourClearanceHits[i];
                parkourClearanceHits[i] = null;

                if (hit == null ||
                    hit == obstacle ||
                    hit.transform == transform ||
                    hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void StartParkour(PlayerMoveState state, Vector3 targetPosition, float duration, float arcHeight, bool ignoreControllerCollision)
        {
            SetMoveState(state);
            parkourElapsed = 0f;
            parkourDuration = Mathf.Max(0.01f, duration);
            parkourArcHeight = Mathf.Max(0f, arcHeight);
            parkourIgnoresControllerCollision = ignoreControllerCollision;
            parkourStartPosition = transform.position;
            parkourTargetPosition = targetPosition;
            verticalVelocity = 0f;
            horizontalVelocity = Vector3.zero;
            slideVelocity = Vector3.zero;
            slideTimeRemaining = 0f;
            IsCrouching = false;
            IsSprinting = false;
            lastJumpPressedTime = -999f;

            if (state == PlayerMoveState.Vault)
                SetControllerHeight(crouchingHeight, true);

            if (parkourIgnoresControllerCollision)
                controller.enabled = false;
        }

        private void UpdateParkourTraversal()
        {
            parkourElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(parkourElapsed / parkourDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 nextPosition = Vector3.Lerp(parkourStartPosition, parkourTargetPosition, eased);
            nextPosition.y += Mathf.Sin(t * Mathf.PI) * parkourArcHeight;

            if (parkourIgnoresControllerCollision)
                transform.position = nextPosition;
            else
                controller.Move(nextPosition - transform.position);

            if (t < 1f)
                return;

            if (parkourIgnoresControllerCollision)
                controller.enabled = true;

            SetMoveState(controller.isGrounded ? PlayerMoveState.Grounded : PlayerMoveState.Airborne);
            parkourElapsed = 0f;
            parkourDuration = 0f;
            parkourArcHeight = 0f;
            parkourIgnoresControllerCollision = false;
            parkourStartPosition = Vector3.zero;
            parkourTargetPosition = Vector3.zero;
            verticalVelocity = groundStickVelocity;
        }

        private void UpdateSlide(Vector3 move)
        {
            slideTimeRemaining -= Time.deltaTime;
            if (move.sqrMagnitude > 0.01f)
                slideVelocity = Vector3.RotateTowards(slideVelocity, move * slideVelocity.magnitude, slideSteerStrength * Time.deltaTime, 0f);

            slideVelocity = Vector3.MoveTowards(slideVelocity, Vector3.zero, slideFriction * Time.deltaTime);
            horizontalVelocity = slideVelocity;

            if (slideTimeRemaining <= 0f || horizontalVelocity.magnitude <= crouchSpeed)
                FinishSlide();
        }

        private void FinishSlide()
        {
            slideTimeRemaining = 0f;
            slideVelocity = Vector3.zero;

            bool blockedAbove = !CanStand();
            IsCrouching = crouchHeld || blockedAbove;
            IsSprinting = false;
            SetMoveState(IsCrouching ? PlayerMoveState.Crouch : PlayerMoveState.Grounded);
        }

        private void UpdateHorizontalVelocity(Vector3 move, bool grounded)
        {
            float targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = move * targetSpeed;
            float rate = grounded
                ? targetVelocity.sqrMagnitude > 0.01f ? acceleration : deceleration
                : airAcceleration;

            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);
        }

        private void UpdateMoveState(Vector2 input, bool grounded)
        {
            if (MoveState == PlayerMoveState.Slide)
            {
                IsCrouching = true;
                IsSprinting = false;
                return;
            }

            IsCrouching = crouchHeld || !CanStand();

            if (!grounded)
            {
                SetMoveState(PlayerMoveState.Airborne);
                return;
            }

            if (IsCrouching)
                SetMoveState(PlayerMoveState.Crouch);
            else if (IsSprinting && input.sqrMagnitude > 0.01f)
                SetMoveState(PlayerMoveState.Sprint);
            else
                SetMoveState(PlayerMoveState.Grounded);
        }

        private void SetMoveState(PlayerMoveState moveState)
        {
            if (MoveState == moveState)
                return;

            PlayerLocomotionState previous = LocomotionState;
            MoveState = moveState;
            PlayerLocomotionState current = LocomotionState;

            if (previous != current)
                LocomotionStateChanged?.Invoke(new LocomotionStateChange(previous, current));
        }

        private static PlayerLocomotionState ToLocomotionState(PlayerMoveState moveState)
        {
            return moveState switch
            {
                PlayerMoveState.Sprint => PlayerLocomotionState.Sprint,
                PlayerMoveState.Crouch => PlayerLocomotionState.Crouch,
                PlayerMoveState.Slide => PlayerLocomotionState.Slide,
                PlayerMoveState.Airborne => PlayerLocomotionState.Airborne,
                PlayerMoveState.Vault => PlayerLocomotionState.Vault,
                PlayerMoveState.Mantle => PlayerLocomotionState.Mantle,
                _ => PlayerLocomotionState.Grounded
            };
        }

        private void UpdateCrouchHeight()
        {
            bool forceCrouch = MoveState == PlayerMoveState.Slide || MoveState == PlayerMoveState.Vault || !CanStand();
            float targetHeight = IsCrouching || forceCrouch ? crouchingHeight : standingHeight;
            SetControllerHeight(targetHeight, false);
        }

        private void SetControllerHeight(float targetHeight, bool immediate)
        {
            controller.height = immediate
                ? targetHeight
                : Mathf.Lerp(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            controller.center = Vector3.up * (controller.height * 0.5f);

            if (cameraRoot != null)
            {
                Vector3 cameraLocalPosition = cameraRoot.localPosition;
                cameraLocalPosition.y = immediate
                    ? targetHeight - 0.12f
                    : Mathf.Lerp(cameraLocalPosition.y, targetHeight - 0.12f, crouchLerpSpeed * Time.deltaTime);
                cameraRoot.localPosition = cameraLocalPosition;
            }
        }

        private bool CanStand()
        {
            if (controller == null || controller.height >= standingHeight - 0.02f)
                return true;

            Vector3 bottom = transform.position + Vector3.up * controller.radius;
            Vector3 top = transform.position + Vector3.up * (standingHeight - controller.radius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                controller.radius * 0.95f,
                standCheckHits,
                standBlockMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = standCheckHits[i];
                standCheckHits[i] = null;

                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                return false;
            }

            return true;
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            crouchSpeed = Mathf.Max(0f, crouchSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            airAcceleration = Mathf.Max(0f, airAcceleration);
            deceleration = Mathf.Max(0f, deceleration);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            standingHeight = Mathf.Max(0.5f, standingHeight);
            crouchingHeight = Mathf.Clamp(crouchingHeight, 0.5f, standingHeight);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            slideStartSpeed = Mathf.Max(0f, slideStartSpeed);
            slideDuration = Mathf.Max(0f, slideDuration);
            slideFriction = Mathf.Max(0f, slideFriction);
            slideMinSprintSpeed = Mathf.Max(0f, slideMinSprintSpeed);
            slideSteerStrength = Mathf.Max(0f, slideSteerStrength);
            parkourCheckDistance = Mathf.Max(0.1f, parkourCheckDistance);
            parkourCheckRadius = Mathf.Max(0.01f, parkourCheckRadius);
            vaultMaxHeight = Mathf.Max(0.15f, vaultMaxHeight);
            mantleMinHeight = Mathf.Max(vaultMaxHeight, mantleMinHeight);
            mantleMaxHeight = Mathf.Max(mantleMinHeight, mantleMaxHeight);
            vaultLandingForwardOffset = Mathf.Max(0.1f, vaultLandingForwardOffset);
            mantleLandingForwardOffset = Mathf.Max(0.05f, mantleLandingForwardOffset);
            vaultDuration = Mathf.Max(0.01f, vaultDuration);
            mantleDuration = Mathf.Max(0.01f, mantleDuration);
            sprintParkourDurationMultiplier = Mathf.Max(0.01f, sprintParkourDurationMultiplier);
            walkParkourDurationMultiplier = Mathf.Max(0.01f, walkParkourDurationMultiplier);
            vaultArcHeight = Mathf.Max(0f, vaultArcHeight);
            windowSillMaxHeight = Mathf.Max(vaultMaxHeight, windowSillMaxHeight);
            windowPassThroughForwardOffset = Mathf.Max(0.1f, windowPassThroughForwardOffset);
            windowPassThroughDuration = Mathf.Max(0.01f, windowPassThroughDuration);
            landingClearance = Mathf.Max(0f, landingClearance);
        }
    }

    public enum PlayerMoveState
    {
        Grounded,
        Sprint,
        Crouch,
        Slide,
        Airborne,
        Vault,
        Mantle
    }
}
