using System.Collections.Generic;
using ProjectHive.Core.Contracts;
using ProjectHive.Core.Events;
using UnityEngine;

namespace ProjectHive.Combat
{
    public enum MeleeWeaponKind
    {
        Knife = 0,
        Warhammer = 1
    }

    [System.Serializable]
    public struct MeleeAttackProfile
    {
        public MeleeWeaponKind Kind;
        public float Damage;
        public float Range;
        public float Radius;
        public float Cooldown;
        public float AttackDuration;
        public float DamageWindowStart;
        public float DamageWindowEnd;
        public float ImpactNoiseRadius;
        public DamageHitZone HitZone;
        public DamageFlags DamageFlags;

        public static MeleeAttackProfile Knife()
        {
            return new MeleeAttackProfile
            {
                Kind = MeleeWeaponKind.Knife,
                Damage = 45f,
                Range = 1.65f,
                Radius = 0.28f,
                Cooldown = 0.45f,
                AttackDuration = 0.42f,
                DamageWindowStart = 0.12f,
                DamageWindowEnd = 0.2f,
                ImpactNoiseRadius = 7f,
                HitZone = DamageHitZone.Body,
                DamageFlags = DamageFlags.CanCauseBleeding
            };
        }

        public static MeleeAttackProfile Warhammer()
        {
            return new MeleeAttackProfile
            {
                Kind = MeleeWeaponKind.Warhammer,
                Damage = 85f,
                Range = 1.9f,
                Radius = 0.55f,
                Cooldown = 0.95f,
                AttackDuration = 0.82f,
                DamageWindowStart = 0.38f,
                DamageWindowEnd = 0.5f,
                ImpactNoiseRadius = 15f,
                HitZone = DamageHitZone.Head,
                DamageFlags = DamageFlags.Critical
            };
        }

        public void Validate()
        {
            Damage = Mathf.Max(0f, Damage);
            Range = Mathf.Max(0.1f, Range);
            Radius = Mathf.Max(0.01f, Radius);
            Cooldown = Mathf.Max(0.01f, Cooldown);
            AttackDuration = Mathf.Max(0.01f, AttackDuration);
            DamageWindowStart = Mathf.Clamp(DamageWindowStart, 0f, AttackDuration);
            DamageWindowEnd = Mathf.Clamp(DamageWindowEnd, DamageWindowStart, AttackDuration);
            ImpactNoiseRadius = Mathf.Max(0f, ImpactNoiseRadius);
        }
    }

    [DisallowMultipleComponent]
    public sealed class MeleeWeapon : MonoBehaviour
    {
        [SerializeField] private MeleeAttackProfile knife = MeleeAttackProfile.Knife();
        [SerializeField] private MeleeAttackProfile warhammer = MeleeAttackProfile.Warhammer();
        [SerializeField] private MeleeWeaponKind equippedKind = MeleeWeaponKind.Knife;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private LayerMask hitMask = ~0;

        private readonly HashSet<GameObject> hitTargetsThisAttack = new HashSet<GameObject>();
        private readonly RaycastHit[] attackHits = new RaycastHit[12];
        private float nextAttackTime;
        private float activeAttackStartedAt;
        private bool hasActiveAttack;
        private bool damageWindowResolved;
        private bool impactNoiseEmitted;
        private GameObject activeOwner;
        private Vector3 activeOrigin;
        private Vector3 activeForward;
        private RaycastHit lastHit;

        public MeleeWeaponKind EquippedKind => equippedKind;
        public bool IsAttacking => hasActiveAttack;
        public bool CanAttack => !hasActiveAttack && Time.time >= nextAttackTime;
        public MeleeAttackProfile CurrentProfile => GetProfile(equippedKind);
        public RaycastHit LastHit => lastHit;

        private void Update()
        {
            if (!hasActiveAttack)
                return;

            UpdateActiveAttack();
        }

        public void Equip(MeleeWeaponKind kind)
        {
            equippedKind = kind;
        }

        public void SetAttackOrigin(Transform origin)
        {
            attackOrigin = origin;
        }

        public bool TryAttack(GameObject owner, Vector3 origin, Vector3 forward, out RaycastHit hit)
        {
            hit = lastHit;
            if (!CanAttack)
                return false;

            MeleeAttackProfile profile = CurrentProfile;
            nextAttackTime = Time.time + profile.Cooldown;
            activeAttackStartedAt = Time.time;
            activeOwner = owner;
            activeOrigin = origin;
            activeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            hasActiveAttack = true;
            damageWindowResolved = false;
            impactNoiseEmitted = false;
            lastHit = default;
            hitTargetsThisAttack.Clear();
            return true;
        }

        private void UpdateActiveAttack()
        {
            MeleeAttackProfile profile = CurrentProfile;
            float elapsed = Time.time - activeAttackStartedAt;

            if (!damageWindowResolved && elapsed >= profile.DamageWindowStart && elapsed <= profile.DamageWindowEnd)
            {
                CastDamageWindow(profile);
                damageWindowResolved = true;
            }
            else if (!damageWindowResolved && elapsed > profile.DamageWindowEnd)
            {
                damageWindowResolved = true;
            }

            if (elapsed < profile.AttackDuration)
                return;

            hasActiveAttack = false;
            activeOwner = null;
            hitTargetsThisAttack.Clear();
        }

        private void CastDamageWindow(MeleeAttackProfile profile)
        {
            Vector3 origin = attackOrigin != null ? attackOrigin.position : activeOrigin;
            Vector3 forward = attackOrigin != null ? attackOrigin.forward : activeForward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = transform.forward;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                profile.Radius,
                forward.normalized,
                attackHits,
                profile.Range,
                hitMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return;

            System.Array.Sort(attackHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = attackHits[i];
                attackHits[i] = default;
                lastHit = hit;

                if (!CombatHitUtility.TryGetDamageable(hit.collider, out IDamageable damageable, out GameObject target) ||
                    CombatHitUtility.IsSelfHit(activeOwner, target) ||
                    damageable.IsDead ||
                    !hitTargetsThisAttack.Add(target))
                {
                    continue;
                }

                DamageData damageData = new DamageData(
                    profile.Damage,
                    DamageKind.Melee,
                    profile.HitZone,
                    profile.DamageFlags,
                    hit.point,
                    forward,
                    activeOwner);
                damageable.ApplyDamage(in damageData);

                if (!impactNoiseEmitted)
                {
                    EmitImpact(activeOwner, hit.point, profile.ImpactNoiseRadius);
                    impactNoiseEmitted = true;
                }
            }
        }

        private MeleeAttackProfile GetProfile(MeleeWeaponKind kind)
        {
            return kind == MeleeWeaponKind.Warhammer ? warhammer : knife;
        }

        private void EmitImpact(GameObject owner, Vector3 position, float noiseRadius)
        {
            GameEventBus bus = GameEventBus.Instance;
            if (bus == null)
                return;

            NoiseEvent noise = new NoiseEvent(
                position,
                0.45f,
                noiseRadius,
                NoiseCategory.MeleeImpact,
                NoiseAffiliation.Player,
                owner != null ? owner.GetInstanceID() : gameObject.GetInstanceID(),
                Time.time);
            bus.PublishNoise(in noise);
        }

        private void OnValidate()
        {
            knife.Validate();
            knife.Kind = MeleeWeaponKind.Knife;
            warhammer.Validate();
            warhammer.Kind = MeleeWeaponKind.Warhammer;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}
