using System.Collections;
using ProjectHive.Core.Contracts;
using ProjectHive.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeWeaponController : MonoBehaviour
    {
        public enum WeaponMode
        {
            Melee,
            ProjectileGun
        }

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform meleePivot;
        [SerializeField] private GameObject meleeVisual;
        [SerializeField] private Transform gunPivot;
        [SerializeField] private GameObject gunVisual;
        [SerializeField] private Transform muzzle;
        [SerializeField] private PrototypeProjectilePool projectilePool;
        [SerializeField] private GameEventBus eventBus;

        [Header("Melee")]
        [SerializeField, Min(0.1f)] private float meleeRange = 2.1f;
        [SerializeField, Min(0.01f)] private float meleeRadius = 0.38f;
        [SerializeField, Min(1f)] private float meleeDamage = 100f;
        [SerializeField, Min(0.05f)] private float meleeCooldown = 0.55f;

        [Header("Projectile Gun")]
        [SerializeField, Min(1f)] private float projectileSpeed = 38f;
        [SerializeField, Min(1f)] private float projectileDamage = 38f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0.02f)] private float fireCooldown = 0.18f;

        private readonly RaycastHit[] meleeHits = new RaycastHit[12];
        private WeaponMode currentMode = WeaponMode.Melee;
        private float nextActionTime;
        private Coroutine visualRoutine;
        private Quaternion meleeRestRotation;
        private Vector3 gunRestPosition;

        public WeaponMode CurrentMode => currentMode;

        public void Configure(
            Camera camera,
            Transform meleeWeaponPivot,
            GameObject meleeWeaponVisual,
            Transform gunWeaponPivot,
            GameObject gunWeaponVisual,
            Transform gunMuzzle,
            PrototypeProjectilePool pool)
        {
            viewCamera = camera;
            meleePivot = meleeWeaponPivot;
            meleeVisual = meleeWeaponVisual;
            gunPivot = gunWeaponPivot;
            gunVisual = gunWeaponVisual;
            muzzle = gunMuzzle;
            projectilePool = pool;
            CacheRestPose();
            SelectWeapon(WeaponMode.Melee);
        }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();

            CacheRestPose();
            SelectWeapon(currentMode);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                    SelectWeapon(WeaponMode.Melee);
                else if (keyboard.digit2Key.wasPressedThisFrame)
                    SelectWeapon(WeaponMode.ProjectileGun);
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                TryPrimaryAction();
        }

        public void SelectWeapon(WeaponMode mode)
        {
            currentMode = mode;
            if (meleeVisual != null)
                meleeVisual.SetActive(mode == WeaponMode.Melee);
            if (gunVisual != null)
                gunVisual.SetActive(mode == WeaponMode.ProjectileGun);
        }

        public bool TryPrimaryAction()
        {
            if (Time.time < nextActionTime || viewCamera == null)
                return false;

            if (currentMode == WeaponMode.Melee)
                PerformMeleeAttack();
            else
                FireProjectile();

            return true;
        }

        public void FireProjectile()
        {
            if (projectilePool == null || viewCamera == null)
                return;

            nextActionTime = Time.time + fireCooldown;
            Vector3 origin = muzzle != null
                ? muzzle.position
                : viewCamera.transform.position + viewCamera.transform.forward * 0.65f;

            projectilePool.Spawn(
                origin,
                viewCamera.transform.forward,
                projectileSpeed,
                projectileDamage,
                projectileLifetime);

            PublishWeaponNoise(1f, 42f, NoiseCategory.Gunshot);
            StartVisualRoutine(PlayGunRecoil());
        }

        private void PerformMeleeAttack()
        {
            nextActionTime = Time.time + meleeCooldown;
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            int count = Physics.SphereCastNonAlloc(
                ray,
                meleeRadius,
                meleeHits,
                meleeRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            IDamageable closestTarget = null;
            RaycastHit closestHit = default;
            float closestDistance = float.PositiveInfinity;
            float closestBlockingDistance = float.PositiveInfinity;

            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = meleeHits[index];
                if (hit.collider.transform.root == transform.root)
                    continue;

                IDamageable health = hit.collider.GetComponentInParent<IDamageable>();
                if (hit.distance < closestBlockingDistance)
                    closestBlockingDistance = hit.distance;

                if (health == null || health.IsDead || hit.distance >= closestDistance)
                    continue;

                closestTarget = health;
                closestHit = hit;
                closestDistance = hit.distance;
            }

            if (closestTarget != null && closestDistance <= closestBlockingDistance + 0.01f)
            {
                DamageData damage = new DamageData(
                    meleeDamage,
                    DamageKind.Melee,
                    closestHit.point,
                    viewCamera.transform.forward,
                    gameObject);
                closestTarget.ApplyDamage(in damage);
                PublishWeaponNoise(0.35f, 10f, NoiseCategory.MeleeImpact);
            }

            StartVisualRoutine(PlayMeleeSwing());
        }

        private void PublishWeaponNoise(float loudness, float radius, NoiseCategory category)
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            if (eventBus == null)
                return;

            NoiseEvent noiseEvent = new NoiseEvent(
                transform.position,
                loudness,
                radius,
                category,
                NoiseAffiliation.Player,
                gameObject.GetInstanceID(),
                Time.time);
            eventBus.PublishNoise(in noiseEvent);
        }

        private void CacheRestPose()
        {
            if (meleePivot != null)
                meleeRestRotation = meleePivot.localRotation;
            if (gunPivot != null)
                gunRestPosition = gunPivot.localPosition;
        }

        private void StartVisualRoutine(IEnumerator routine)
        {
            if (visualRoutine != null)
                StopCoroutine(visualRoutine);

            visualRoutine = StartCoroutine(routine);
        }

        private IEnumerator PlayMeleeSwing()
        {
            if (meleePivot == null)
                yield break;

            const float duration = 0.28f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float arc = Mathf.Sin(normalized * Mathf.PI);
                meleePivot.localRotation = meleeRestRotation * Quaternion.Euler(-arc * 65f, arc * 36f, arc * 28f);
                yield return null;
            }

            meleePivot.localRotation = meleeRestRotation;
            visualRoutine = null;
        }

        private IEnumerator PlayGunRecoil()
        {
            if (gunPivot == null)
                yield break;

            const float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float recoil = Mathf.Sin(normalized * Mathf.PI);
                gunPivot.localPosition = gunRestPosition + Vector3.back * recoil * 0.09f;
                yield return null;
            }

            gunPivot.localPosition = gunRestPosition;
            visualRoutine = null;
        }
    }
}
