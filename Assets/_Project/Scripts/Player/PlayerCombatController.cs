using ProjectHive.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private FirearmWeapon firearm;
        [SerializeField] private MeleeWeapon meleeWeapon;
        [SerializeField] private AssassinationAttack assassinationAttack;
        [SerializeField] private bool preferAssassinationWhenCrouched = true;

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
        }

        private void Update()
        {
            if (Mouse.current == null)
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                Fire();

            if (Mouse.current.rightButton.wasPressedThisFrame)
                MeleeOrAssassinate();
        }

        public void Fire()
        {
            if (firearm == null)
                return;

            firearm.TryFire(gameObject, GetOrigin(), GetForward(), out RaycastHit hit);
            LastWeaponHit = hit;
        }

        public void MeleeOrAssassinate()
        {
            Vector3 origin = GetOrigin();
            Vector3 forward = GetForward();

            if (ShouldTryAssassination() &&
                assassinationAttack != null &&
                assassinationAttack.TryAssassinate(gameObject, origin, forward, out GameObject target))
            {
                LastAssassinationTarget = target;
                return;
            }

            if (meleeWeapon == null)
                return;

            meleeWeapon.TryAttack(gameObject, origin, forward, out RaycastHit hit);
            LastWeaponHit = hit;
        }

        private bool ShouldTryAssassination()
        {
            if (!preferAssassinationWhenCrouched)
                return true;

            FirstPersonMotor motor = GetComponent<FirstPersonMotor>();
            return motor == null || motor.IsCrouching;
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
