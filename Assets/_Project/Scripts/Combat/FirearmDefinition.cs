using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    public enum FirearmVisualProfile
    {
        Pistol = 0,
        Revolver = 1
    }

    [CreateAssetMenu(fileName = "Firearm_", menuName = "Project Hive/Combat/Firearm Definition")]
    public sealed class FirearmDefinition : ScriptableObject
    {
        [SerializeField] private string weaponItemId = "weapon.firearm.unassigned";
        [SerializeField] private string displayName = "Unnamed Firearm";
        [SerializeField] private FirearmFeedType feedType = FirearmFeedType.DetachableMagazine;
        [SerializeField] private FirearmVisualProfile visualProfile = FirearmVisualProfile.Pistol;
        [SerializeField] private Color displayColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0.01f)] private float fireRate = 4f;
        [SerializeField, Min(1f)] private float maxDistance = 120f;
        [SerializeField, Min(0f)] private float fullDamageDistance = 25f;
        [SerializeField, Range(0f, 1f)] private float minimumDamageMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float spreadDegrees = 0.75f;
        [SerializeField, Min(0)] private int magazineCapacity = 10;
        [SerializeField, Min(1)] private int cylinderCapacity = 5;
        [SerializeField, Min(0f)] private float gunshotLoudness = 1f;
        [SerializeField, Min(0f)] private float gunshotRadius = 55f;

        public string WeaponItemId => weaponItemId;
        public string DisplayName => displayName;
        public FirearmFeedType FeedType => feedType;
        public FirearmVisualProfile VisualProfile => visualProfile;
        public Color DisplayColor => displayColor;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float MaxDistance => maxDistance;
        public float FullDamageDistance => fullDamageDistance;
        public float MinimumDamageMultiplier => minimumDamageMultiplier;
        public float SpreadDegrees => spreadDegrees;
        public int MagazineCapacity => magazineCapacity;
        public int CylinderCapacity => cylinderCapacity;
        public float GunshotLoudness => gunshotLoudness;
        public float GunshotRadius => gunshotRadius;

        private void OnValidate()
        {
            weaponItemId = string.IsNullOrWhiteSpace(weaponItemId)
                ? "weapon.firearm.unassigned"
                : weaponItemId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName.Trim();
            damage = Mathf.Max(0f, damage);
            fireRate = Mathf.Max(0.01f, fireRate);
            maxDistance = Mathf.Max(1f, maxDistance);
            fullDamageDistance = Mathf.Clamp(fullDamageDistance, 0f, maxDistance);
            minimumDamageMultiplier = Mathf.Clamp01(minimumDamageMultiplier);
            spreadDegrees = Mathf.Max(0f, spreadDegrees);
            magazineCapacity = Mathf.Max(0, magazineCapacity);
            cylinderCapacity = Mathf.Max(1, cylinderCapacity);
        }
    }
}
