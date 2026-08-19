using System.Collections;
using ProjectHive.Combat;
using ProjectHive.Core.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Player
{
    public enum PlayerEquippedWeaponCategory
    {
        Firearm = 0,
        Melee = 1
    }

    [DisallowMultipleComponent]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private FirearmWeapon firearm;
        [SerializeField] private MeleeWeapon meleeWeapon;
        [SerializeField] private AssassinationAttack assassinationAttack;
        [SerializeField] private PlayerMeleeViewModel meleeViewModel;
        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private float knifeAssassinationAimLockSeconds = 1.75f;
        [SerializeField] private float warhammerAssassinationAimLockSeconds = 1.85f;

        private PlayerEquippedWeaponCategory equippedCategory = PlayerEquippedWeaponCategory.Firearm;
        private Coroutine assassinationAimLockRoutine;
        private bool assassinationInputLocked;

        public RaycastHit LastWeaponHit { get; private set; }
        public GameObject LastAssassinationTarget { get; private set; }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (firearm == null)
                firearm = GetComponentInChildren<FirearmWeapon>();
            if (meleeWeapon == null)
                meleeWeapon = GetComponentInChildren<MeleeWeapon>();
            if (assassinationAttack == null)
                assassinationAttack = GetComponentInChildren<AssassinationAttack>();
            if (meleeViewModel == null)
                meleeViewModel = GetComponentInChildren<PlayerMeleeViewModel>();
            if (motor == null)
                motor = GetComponent<FirstPersonMotor>();

            if (meleeWeapon == null)
                meleeWeapon = gameObject.AddComponent<MeleeWeapon>();
            if (assassinationAttack == null)
                assassinationAttack = gameObject.AddComponent<AssassinationAttack>();
            if (meleeViewModel == null)
                meleeViewModel = gameObject.AddComponent<PlayerMeleeViewModel>();

            if (viewCamera != null)
                meleeWeapon.SetAttackOrigin(viewCamera.transform);

            EquipFirearmSlot(1);
        }

        private void OnDisable()
        {
            EndAssassinationAimLock();
        }

        private void Update()
        {
            if (assassinationInputLocked)
                return;

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                    PrimaryAttack();

                if (Mouse.current.rightButton.wasPressedThisFrame)
                    TryAssassinateOnly();
            }

            if (Keyboard.current == null)
                return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                EquipFirearmSlot(1);

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                EquipFirearmSlot(2);

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                EquipMelee(MeleeWeaponKind.Knife);

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
                EquipMelee(MeleeWeaponKind.Warhammer);

            if (Keyboard.current.rKey.wasPressedThisFrame)
                ReloadFirearm();
        }

        public void Fire()
        {
            if (equippedCategory != PlayerEquippedWeaponCategory.Firearm || firearm == null)
                return;

            firearm.TryFire(gameObject, GetOrigin(), GetForward(), out RaycastHit hit);
            LastWeaponHit = hit;
        }

        public void PrimaryAttack()
        {
            if (equippedCategory == PlayerEquippedWeaponCategory.Firearm)
            {
                Fire();
                return;
            }

            MeleeAttack();
        }

        public void MeleeAttack()
        {
            if (equippedCategory != PlayerEquippedWeaponCategory.Melee || meleeWeapon == null)
                return;

            Vector3 origin = GetOrigin();
            Vector3 forward = GetForward();

            if (meleeWeapon.TryAttack(gameObject, origin, forward, out RaycastHit hit))
                meleeViewModel?.PlayAttack(meleeWeapon.EquippedKind);

            LastWeaponHit = hit;
        }

        public void TryAssassinateOnly()
        {
            if (equippedCategory != PlayerEquippedWeaponCategory.Melee || assassinationAttack == null)
                return;

            if (!assassinationAttack.TryAssassinate(gameObject, GetOrigin(), GetForward(), out GameObject target))
                return;

            LastAssassinationTarget = target;
        }

        public void ReloadFirearm()
        {
            if (equippedCategory != PlayerEquippedWeaponCategory.Firearm)
                return;

            firearm?.TryReload();
        }

        public void EquipFirearmSlot(int slotNumber)
        {
            equippedCategory = PlayerEquippedWeaponCategory.Firearm;
            firearm?.TryEquipLoadoutSlot(slotNumber);
            firearm?.SetViewModelVisible(true);
            meleeViewModel?.SetVisible(false);
        }

        public void EquipMelee(MeleeWeaponKind kind)
        {
            equippedCategory = PlayerEquippedWeaponCategory.Melee;
            meleeWeapon?.Equip(kind);
            meleeViewModel?.Equip(kind);
            meleeViewModel?.SetVisible(true);
            firearm?.SetViewModelVisible(false);
        }

        public void PlayAssassinationMotion(GameObject target = null)
        {
            MeleeWeaponKind kind = meleeWeapon != null ? meleeWeapon.EquippedKind : MeleeWeaponKind.Knife;
            if (target != null)
                motor?.ForceLookAt(GetAssassinationFocusPoint(target));

            BeginAssassinationAimLock(kind);
            meleeViewModel?.PlayAssassination(kind, target);

            if (target == null)
                return;

            AssassinationExecutionMotion executionMotion = target.GetComponent<AssassinationExecutionMotion>();
            if (executionMotion == null)
                executionMotion = target.AddComponent<AssassinationExecutionMotion>();

            executionMotion.Play(kind, transform);
        }

        private void BeginAssassinationAimLock(MeleeWeaponKind kind)
        {
            if (assassinationAimLockRoutine != null)
                StopCoroutine(assassinationAimLockRoutine);

            assassinationInputLocked = true;
            motor?.SetLookInputLocked(true);
            float maxDuration = kind == MeleeWeaponKind.Warhammer
                ? warhammerAssassinationAimLockSeconds
                : knifeAssassinationAimLockSeconds;
            assassinationAimLockRoutine = StartCoroutine(ReleaseAssassinationAimLockWhenMotionEnds(maxDuration));
        }

        private IEnumerator ReleaseAssassinationAimLockWhenMotionEnds(float maxDuration)
        {
            yield return null;

            float elapsed = 0f;
            while (elapsed < maxDuration && meleeViewModel != null && meleeViewModel.IsMotionPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            assassinationAimLockRoutine = null;
            assassinationInputLocked = false;
            motor?.SetLookInputLocked(false);
        }

        private void EndAssassinationAimLock()
        {
            if (assassinationAimLockRoutine != null)
            {
                StopCoroutine(assassinationAimLockRoutine);
                assassinationAimLockRoutine = null;
            }

            assassinationInputLocked = false;
            motor?.SetLookInputLocked(false);
        }

        private Vector3 GetOrigin()
        {
            return viewCamera != null ? viewCamera.transform.position : transform.position + Vector3.up * 1.6f;
        }

        private Vector3 GetForward()
        {
            return viewCamera != null ? viewCamera.transform.forward : transform.forward;
        }

        private static Vector3 GetAssassinationFocusPoint(GameObject target)
        {
            Transform generated = target != null ? target.transform.Find("Generated Humanoid Target") : null;
            Transform head = generated != null ? generated.Find("Head Hit Zone") : null;
            if (head == null && target != null)
                head = FindChildByName(target.transform, "Head Hit Zone");

            if (head != null)
                return head.position + Vector3.down * 0.08f;

            return target != null ? target.transform.position + Vector3.up * 1.35f : Vector3.zero;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform match = FindChildByName(child, childName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private void OnValidate()
        {
            knifeAssassinationAimLockSeconds = Mathf.Max(0.1f, knifeAssassinationAimLockSeconds);
            warhammerAssassinationAimLockSeconds = Mathf.Max(0.1f, warhammerAssassinationAimLockSeconds);
        }
    }
}
