using UnityEngine;
using ProjectHive.Combat;

namespace ProjectHive.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerBodyLocomotionAnimator : MonoBehaviour
    {
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
        private static readonly int IsAirborneHash = Animator.StringToHash("IsAirborne");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
        private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        private static readonly int IsVaultingHash = Animator.StringToHash("IsVaulting");
        private static readonly int IsMantlingHash = Animator.StringToHash("IsMantling");
        private static readonly int IsArmedHash = Animator.StringToHash("IsArmed");
        private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
        private static readonly int SlideHash = Animator.StringToHash("Slide");
        private static readonly int VaultHash = Animator.StringToHash("Vault");
        private static readonly int MantleHash = Animator.StringToHash("Mantle");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private FirearmWeapon firearm;
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float dampTime = 0.12f;
        [SerializeField] private bool firearmPoseIsArmed = true;

        private PlayerMoveState previousMoveState;
        private bool subscribedToFirearm;

        private void Awake()
        {
            ResolveReferences();
            previousMoveState = motor != null ? motor.MoveState : PlayerMoveState.Grounded;
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeFirearm();
            previousMoveState = motor != null ? motor.MoveState : PlayerMoveState.Grounded;
            UpdateAnimator(0f);
        }

        private void OnDisable()
        {
            UnsubscribeFirearm();
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeFirearm();
            UpdateAnimator(dampTime);
        }

        private void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponent<FirstPersonMotor>();

            if (bodyAnimator == null)
            {
                Transform visual = transform.Find("PH_PlayerBody_Visual");
                if (visual != null)
                    bodyAnimator = visual.GetComponentInChildren<Animator>(true);
            }

            if (firearm == null)
                firearm = GetComponentInChildren<FirearmWeapon>(true);
        }

        private void UpdateAnimator(float damping)
        {
            if (motor == null || bodyAnimator == null || bodyAnimator.runtimeAnimatorController == null)
                return;

            PlayerMoveState moveState = motor.MoveState;
            bool sliding = moveState == PlayerMoveState.Slide;
            bool vaulting = moveState == PlayerMoveState.Vault;
            bool mantling = moveState == PlayerMoveState.Mantle;
            bool airborne = moveState == PlayerMoveState.Airborne || vaulting || mantling;

            float speed01 = 0f;
            if (!airborne)
            {
                float referenceSpeed = motor.IsSprinting ? sprintSpeed : walkSpeed;
                speed01 = referenceSpeed > 0f
                    ? Mathf.Clamp01(new Vector3(motor.Velocity.x, 0f, motor.Velocity.z).magnitude / sprintSpeed)
                    : 0f;
            }

            if (moveState != previousMoveState)
                TriggerMoveState(previousMoveState, moveState);

            SetBool(IsAirborneHash, airborne);
            SetBool(GroundedHash, !airborne);
            SetBool(FreeFallHash, moveState == PlayerMoveState.Airborne);
            SetBool(IsSlidingHash, sliding);
            SetBool(IsVaultingHash, vaulting);
            SetBool(IsMantlingHash, mantling);
            SetBool(IsArmedHash, firearmPoseIsArmed && firearm != null && firearm.gameObject.activeInHierarchy);
            SetBool(IsReloadingHash, firearm != null && firearm.IsReloading);
            SetFloat(MoveSpeedHash, speed01, damping);
            SetFloat(SpeedHash, speed01, damping);
            SetFloat(MotionSpeedHash, speed01 > 0.01f ? 1f : 0f, damping);

            previousMoveState = moveState;
        }

        private void TriggerMoveState(PlayerMoveState previous, PlayerMoveState current)
        {
            if (previous == current || bodyAnimator == null)
                return;

            if (current == PlayerMoveState.Slide)
                SetTrigger(SlideHash);
            else if (current == PlayerMoveState.Vault)
                SetTrigger(VaultHash);
            else if (current == PlayerMoveState.Mantle)
                SetTrigger(MantleHash);
        }

        private void SubscribeFirearm()
        {
            if (firearm == null || subscribedToFirearm)
                return;

            firearm.FirearmFired += HandleFirearmFired;
            firearm.FirearmReloadStarted += HandleFirearmReloadStarted;
            subscribedToFirearm = true;
        }

        private void UnsubscribeFirearm()
        {
            if (firearm == null || !subscribedToFirearm)
                return;

            firearm.FirearmFired -= HandleFirearmFired;
            firearm.FirearmReloadStarted -= HandleFirearmReloadStarted;
            subscribedToFirearm = false;
        }

        private void HandleFirearmFired()
        {
            SetTrigger(FireHash);
        }

        private void HandleFirearmReloadStarted()
        {
            SetTrigger(ReloadHash);
        }

        private void SetBool(int hash, bool value)
        {
            if (bodyAnimator != null)
                bodyAnimator.SetBool(hash, value);
        }

        private void SetFloat(int hash, float value, float damping)
        {
            if (bodyAnimator == null)
                return;

            if (damping > 0f && Application.isPlaying)
                bodyAnimator.SetFloat(hash, value, damping, Time.deltaTime);
            else
                bodyAnimator.SetFloat(hash, value);
        }

        private void SetTrigger(int hash)
        {
            if (bodyAnimator != null && bodyAnimator.runtimeAnimatorController != null)
                bodyAnimator.SetTrigger(hash);
        }
    }
}
