using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamageHitZoneMarker : MonoBehaviour
    {
        [SerializeField] private DamageHitZone hitZone = DamageHitZone.Body;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        public DamageHitZone HitZone => hitZone;
        public float DamageMultiplier => damageMultiplier;

        public void Configure(DamageHitZone zone, float multiplier)
        {
            hitZone = zone;
            damageMultiplier = Mathf.Max(0f, multiplier);
        }

        private void OnValidate()
        {
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
        }
    }
}
