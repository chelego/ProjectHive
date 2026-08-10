using System.IO;
using ProjectHive.Combat;
using ProjectHive.Core.Contracts;
using UnityEditor;
using UnityEngine;

namespace ProjectHive.Editor
{
    public static class FirearmPresetBuilder
    {
        private const string WeaponDataFolder = "Assets/_Project/Data/Items/Weapons";

        [MenuItem("Project Hive/Combat/Create MVP Firearms")]
        public static void CreateMvpFirearms()
        {
            EnsureFolder("Assets/_Project/Data/Items", "Weapons");

            CreateOrUpdate(
                "Firearm_ServicePistol.asset",
                "weapon.firearm.service_pistol",
                "Service Pistol",
                FirearmFeedType.DetachableMagazine,
                FirearmVisualProfile.Pistol,
                new Color(0.08f, 0.1f, 0.12f, 1f),
                damage: 32f,
                fireRate: 4.5f,
                maxDistance: 95f,
                fullDamageDistance: 22f,
                minimumDamageMultiplier: 0.35f,
                spreadDegrees: 0.05f,
                magazineCapacity: 10,
                cylinderCapacity: 5,
                loudness: 1f,
                radius: 55f);

            CreateOrUpdate(
                "Firearm_FiveShotRevolver.asset",
                "weapon.firearm.five_shot_revolver",
                "Five-Shot Revolver",
                FirearmFeedType.Revolver,
                FirearmVisualProfile.Revolver,
                new Color(0.36f, 0.29f, 0.18f, 1f),
                damage: 48f,
                fireRate: 2.2f,
                maxDistance: 110f,
                fullDamageDistance: 28f,
                minimumDamageMultiplier: 0.4f,
                spreadDegrees: 0.03f,
                magazineCapacity: 0,
                cylinderCapacity: 5,
                loudness: 1.15f,
                radius: 65f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateOrUpdate(
            string fileName,
            string weaponItemId,
            string displayName,
            FirearmFeedType feedType,
            FirearmVisualProfile visualProfile,
            Color displayColor,
            float damage,
            float fireRate,
            float maxDistance,
            float fullDamageDistance,
            float minimumDamageMultiplier,
            float spreadDegrees,
            int magazineCapacity,
            int cylinderCapacity,
            float loudness,
            float radius)
        {
            string path = $"{WeaponDataFolder}/{fileName}";
            FirearmDefinition definition =
                AssetDatabase.LoadAssetAtPath<FirearmDefinition>(path);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FirearmDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("weaponItemId").stringValue = weaponItemId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("feedType").enumValueIndex = (int)feedType;
            serialized.FindProperty("visualProfile").enumValueIndex = (int)visualProfile;
            serialized.FindProperty("displayColor").colorValue = displayColor;
            serialized.FindProperty("damage").floatValue = damage;
            serialized.FindProperty("fireRate").floatValue = fireRate;
            serialized.FindProperty("maxDistance").floatValue = maxDistance;
            serialized.FindProperty("fullDamageDistance").floatValue = fullDamageDistance;
            serialized.FindProperty("minimumDamageMultiplier").floatValue = minimumDamageMultiplier;
            serialized.FindProperty("spreadDegrees").floatValue = spreadDegrees;
            serialized.FindProperty("magazineCapacity").intValue = magazineCapacity;
            serialized.FindProperty("cylinderCapacity").intValue = cylinderCapacity;
            serialized.FindProperty("gunshotLoudness").floatValue = loudness;
            serialized.FindProperty("gunshotRadius").floatValue = radius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string fullPath = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(fullPath))
                return;

            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }
    }
}
