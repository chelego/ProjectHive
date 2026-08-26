using ProjectHive.Combat;
using UnityEngine;

namespace ProjectHive.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerBodyActionPoseDriver : MonoBehaviour
    {
        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private FirearmWeapon firearm;
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private Transform aimReference;
        [SerializeField, Range(0f, 1f)] private float armedPoseWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float vaultPoseWeight = 1f;
        [SerializeField] private float blendSpeed = 12f;
        [SerializeField] private float fireRecoilSeconds = 0.18f;
        [SerializeField] private float fireRecoilWeight = 1f;
        [SerializeField] private Vector3 rightWristLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 leftSupportWristLocalOffset = Vector3.zero;
        [SerializeField] private bool applyProceduralArmPose;
        [SerializeField] private bool useWeaponHandTargets = true;
        [SerializeField] private bool applyProceduralFingerPose;
        [SerializeField] private Vector3 rightTriggerIndexOffset = Vector3.zero;
        [SerializeField] private Vector3 rightGripFingerCurlOffset = Vector3.zero;
        [SerializeField] private Vector3 rightThumbGripOffset = Vector3.zero;
        [SerializeField] private Vector3 leftFingerSupportOffset = Vector3.zero;
        [SerializeField] private Vector3 leftThumbSupportOffset = Vector3.zero;

        private Transform chest;
        private Transform upperChest;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;
        private Transform[] rightTriggerIndex;
        private Transform[] rightGripFingers;
        private Transform[] rightGripThumb;
        private Transform[] leftSupportFingers;
        private Transform[] leftSupportThumb;
        private float armedBlend;
        private float vaultElapsed;
        private float lastFireTime = -999f;
        private PlayerMoveState previousMoveState;
        private bool subscribedToFirearm;

        private void Awake()
        {
            ResolveReferences();
            CacheBones();
            previousMoveState = motor != null ? motor.MoveState : PlayerMoveState.Grounded;
            armedBlend = firearm != null && firearm.gameObject.activeInHierarchy ? armedPoseWeight : 0f;
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheBones();
            SubscribeFirearm();
            previousMoveState = motor != null ? motor.MoveState : PlayerMoveState.Grounded;
            armedBlend = firearm != null && firearm.gameObject.activeInHierarchy ? armedPoseWeight : 0f;
        }

        private void OnDisable()
        {
            UnsubscribeFirearm();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            CacheBones();
            SubscribeFirearm();

            bool firearmVisible = firearm != null && firearm.gameObject.activeInHierarchy;
            float targetArmed = firearmVisible ? armedPoseWeight : 0f;
            armedBlend = Mathf.MoveTowards(armedBlend, targetArmed, blendSpeed * Time.deltaTime);

            ApplyFirearmPose(armedBlend);
            ApplyVaultStepDownPose();
        }

        private void ResolveReferences()
        {
            if (motor == null)
                motor = GetComponent<FirstPersonMotor>();

            if (firearm == null)
                firearm = GetComponentInChildren<FirearmWeapon>(true);

            if (bodyAnimator == null)
            {
                Transform visual = transform.Find("PH_PlayerBody_Visual");
                bodyAnimator = visual != null
                    ? visual.GetComponentInChildren<Animator>(true)
                    : GetComponentInChildren<Animator>(true);
            }

            if (aimReference == null)
            {
                Camera mainCamera = Camera.main;
                aimReference = mainCamera != null ? mainCamera.transform : transform;
            }
        }

        private void CacheBones()
        {
            if (bodyAnimator == null || !bodyAnimator.isHuman)
                return;

            chest ??= bodyAnimator.GetBoneTransform(HumanBodyBones.Chest);
            upperChest ??= bodyAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            leftUpperArm ??= bodyAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm ??= bodyAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand ??= bodyAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm ??= bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm ??= bodyAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand ??= bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpperLeg ??= bodyAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg ??= bodyAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot ??= bodyAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpperLeg ??= bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg ??= bodyAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot ??= bodyAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

            rightTriggerIndex ??= CreateFingerChain(
                HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal);
            rightGripFingers ??= CreateFingerChain(
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal);
            rightGripThumb ??= CreateFingerChain(
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal);
            leftSupportFingers ??= CreateFingerChain(
                HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal);
            leftSupportThumb ??= CreateFingerChain(
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal);
        }

        private void ApplyFirearmPose(float weight)
        {
            if (weight <= 0.001f)
                return;

            float recoil = 0f;
            if (Time.time - lastFireTime <= fireRecoilSeconds)
            {
                float normalized = 1f - Mathf.Clamp01((Time.time - lastFireTime) / fireRecoilSeconds);
                recoil = normalized * normalized * fireRecoilWeight;
            }

            ApplyLocalOffset(chest, new Vector3(-2f - 7f * recoil, 0f, 1.5f * recoil), weight);
            ApplyLocalOffset(upperChest, new Vector3(-8f - 14f * recoil, 0f, 2f * recoil), weight);
            if (!applyProceduralArmPose)
                return;

            Vector3 forward = aimReference != null ? aimReference.forward : transform.forward;
            Vector3 right = aimReference != null ? aimReference.right : transform.right;
            Vector3 up = Vector3.up;
            Vector3 chestPosition = upperChest != null
                ? upperChest.position
                : transform.position + up * 1.35f;
            Vector3 recoilOffset = (-forward * 0.08f + up * 0.04f) * recoil;
            Vector3 rightHandTarget = chestPosition + forward * 0.56f + right * 0.145f - up * 0.13f;
            Vector3 leftHandTarget = rightHandTarget - up * 0.20f - right * 0.055f - forward * 0.015f;
            bool hasWeaponTargets = false;
            if (firearm != null &&
                firearm.TryGetPistolHandTargets(out Vector3 weaponRightGrip, out Vector3 weaponLeftSupport))
            {
                rightHandTarget = weaponRightGrip;
                leftHandTarget = weaponLeftSupport;
                hasWeaponTargets = true;
            }

            rightHandTarget += recoilOffset;
            leftHandTarget += recoilOffset * 0.25f;

            AimArmAtTarget(rightUpperArm, rightLowerArm, rightHand, rightHandTarget, weight);
            AimArmAtTarget(leftUpperArm, leftLowerArm, leftHand, leftHandTarget, weight);

            ApplyLocalOffset(rightHand, rightWristLocalOffset, weight);
            ApplyLocalOffset(leftHand, leftSupportWristLocalOffset, weight);
            if (applyProceduralFingerPose)
            {
                ApplyFingerPose(rightTriggerIndex, rightTriggerIndexOffset, weight);
                ApplyFingerPose(rightGripFingers, rightGripFingerCurlOffset, weight);
                ApplyFingerPose(rightGripThumb, rightThumbGripOffset, weight);
                ApplyFingerPose(leftSupportFingers, leftFingerSupportOffset, weight);
                ApplyFingerPose(leftSupportThumb, leftThumbSupportOffset, weight);
            }
        }

        private void ApplyVaultStepDownPose()
        {
            if (motor == null)
                return;

            PlayerMoveState moveState = motor.MoveState;
            if (moveState != previousMoveState)
            {
                vaultElapsed = 0f;
                previousMoveState = moveState;
            }

            if (moveState != PlayerMoveState.Vault)
                return;

            vaultElapsed += Time.deltaTime;
            float phase = Mathf.Clamp01(vaultElapsed / 0.6f);
            float plant = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.46f, phase));
            float follow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.72f, phase));
            float handPress = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.92f, phase));
            float weight = vaultPoseWeight;

            ApplyLocalOffset(chest, new Vector3(26f * handPress, 0f, 0f), weight);
            ApplyLocalOffset(upperChest, new Vector3(12f * handPress, 0f, 0f), weight);
            ApplyLocalOffset(leftUpperArm, new Vector3(72f * handPress, 0f, -8f), weight);
            ApplyLocalOffset(rightUpperArm, new Vector3(68f * handPress, 0f, 8f), weight);
            ApplyLocalOffset(leftLowerArm, new Vector3(-36f * handPress, 0f, 0f), weight);
            ApplyLocalOffset(rightLowerArm, new Vector3(-34f * handPress, 0f, 0f), weight);

            ApplyLocalOffset(leftUpperLeg, new Vector3(Mathf.Lerp(-72f, -16f, plant), 0f, -18f), weight);
            ApplyLocalOffset(leftLowerLeg, new Vector3(Mathf.Lerp(96f, 24f, plant), 0f, 0f), weight);
            ApplyLocalOffset(leftFoot, new Vector3(Mathf.Lerp(-20f, 42f, plant), 0f, 0f), weight);
            ApplyLocalOffset(rightUpperLeg, new Vector3(Mathf.Lerp(-24f, -68f, follow), 0f, 20f * follow), weight);
            ApplyLocalOffset(rightLowerLeg, new Vector3(Mathf.Lerp(28f, 94f, follow), 0f, 0f), weight);
            ApplyLocalOffset(rightFoot, new Vector3(Mathf.Lerp(-10f, 40f, follow), 0f, 0f), weight);
        }

        private void SubscribeFirearm()
        {
            if (firearm == null || subscribedToFirearm)
                return;

            firearm.FirearmFired += HandleFirearmFired;
            subscribedToFirearm = true;
        }

        private void UnsubscribeFirearm()
        {
            if (firearm == null || !subscribedToFirearm)
                return;

            firearm.FirearmFired -= HandleFirearmFired;
            subscribedToFirearm = false;
        }

        private void HandleFirearmFired()
        {
            lastFireTime = Time.time;
        }

        private static void ApplyLocalOffset(Transform bone, Vector3 eulerOffset, float weight)
        {
            if (bone == null || weight <= 0f)
                return;

            Quaternion offset = Quaternion.Euler(eulerOffset * weight);
            bone.localRotation *= offset;
        }

        private Transform[] CreateFingerChain(params HumanBodyBones[] bones)
        {
            Transform[] chain = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                chain[i] = bodyAnimator.GetBoneTransform(bones[i]);

            return chain;
        }

        private static void ApplyFingerPose(Transform[] bones, Vector3 eulerOffset, float weight)
        {
            if (bones == null || weight <= 0f)
                return;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                    continue;

                float segmentWeight = i % 2 == 0 ? 1f : 0.72f;
                ApplyLocalOffset(bones[i], eulerOffset * segmentWeight, weight);
            }
        }

        private static void AimArmAtTarget(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 handTarget,
            float weight)
        {
            if (weight <= 0f)
                return;

            for (int i = 0; i < 4; i++)
            {
                AimBoneAtTarget(lowerArm, hand, handTarget, weight);
                AimBoneAtTarget(upperArm, hand, handTarget, weight);
            }
        }

        private static void AimBoneAtTarget(Transform bone, Transform currentEnd, Vector3 target, float weight)
        {
            if (bone == null || currentEnd == null)
                return;

            Vector3 currentDirection = currentEnd.position - bone.position;
            Vector3 targetDirection = target - bone.position;
            if (currentDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion correction = Quaternion.FromToRotation(currentDirection.normalized, targetDirection.normalized);
            bone.rotation = Quaternion.Slerp(bone.rotation, correction * bone.rotation, weight);
        }

        private void OnValidate()
        {
            armedPoseWeight = Mathf.Clamp01(armedPoseWeight);
            vaultPoseWeight = Mathf.Clamp01(vaultPoseWeight);
            blendSpeed = Mathf.Max(0f, blendSpeed);
            fireRecoilSeconds = Mathf.Max(0.01f, fireRecoilSeconds);
            fireRecoilWeight = Mathf.Max(0f, fireRecoilWeight);
        }
    }
}
