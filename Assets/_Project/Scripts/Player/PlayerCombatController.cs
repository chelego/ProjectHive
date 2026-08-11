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

        private PlayerEquippedWeaponCategory equippedCategory = PlayerEquippedWeaponCategory.Firearm;

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

        private void Update()
        {
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
            PlayAssassinationMotion(target);
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
            meleeViewModel?.PlayAssassination(kind);

            if (target == null)
                return;

            AssassinationExecutionMotion executionMotion = target.GetComponent<AssassinationExecutionMotion>();
            if (executionMotion == null)
                executionMotion = target.AddComponent<AssassinationExecutionMotion>();

            executionMotion.Play(kind, transform);
        }

        private Vector3 GetOrigin()
        {
            return viewCamera != null ? viewCamera.transform.position : transform.position + Vector3.up * 1.6f;
        }

        private Vector3 GetForward()
        {
            return viewCamera != null ? viewCamera.transform.forward : transform.forward;
        }
    }
}
