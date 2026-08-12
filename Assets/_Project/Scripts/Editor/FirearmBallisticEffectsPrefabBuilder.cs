#if UNITY_EDITOR
using ProjectHive.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectHive.EditorTools
{
    internal static class FirearmBallisticEffectsPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Combat/Effects";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string MuzzleFlashPrefabPath = PrefabFolder + "/PH_FX_MuzzleFlash.prefab";
        private const string TracerPrefabPath = PrefabFolder + "/PH_FX_BulletTracer.prefab";
        private const string SurfaceImpactPrefabPath = PrefabFolder + "/PH_FX_SurfaceImpact.prefab";
        private const string DamageableImpactPrefabPath = PrefabFolder + "/PH_FX_DamageableImpact.prefab";
        private const string MuzzleMaterialPath = MaterialFolder + "/M_FX_MuzzleFlash.mat";
        private const string TracerMaterialPath = MaterialFolder + "/M_FX_BulletTracer.mat";
        private const string SurfaceImpactMaterialPath = MaterialFolder + "/M_FX_SurfaceImpact.mat";
        private const string DamageableImpactMaterialPath = MaterialFolder + "/M_FX_DamageableImpact.mat";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabsAfterReload()
        {
            EditorApplication.delayCall -= EnsurePrefabs;
            EditorApplication.delayCall += EnsurePrefabs;
        }

        [MenuItem("Tools/ProjectHive/Combat/Rebuild Firearm Ballistic Effects")]
        private static void RebuildPrefabs()
        {
            CreateOrReplacePrefabs(true);
        }

        private static void EnsurePrefabs()
        {
            CreateOrReplacePrefabs(false);
        }

        private static void CreateOrReplacePrefabs(bool overwrite)
        {
            EnsureFolder("Assets/_Project/Prefabs", "Combat");
            EnsureFolder("Assets/_Project/Prefabs/Combat", "Effects");
            EnsureFolder("Assets/_Project/Art", "Materials");

            Material muzzle = GetOrCreateMaterial(MuzzleMaterialPath, new Color(1f, 0.66f, 0.18f, 1f));
            Material tracer = GetOrCreateMaterial(TracerMaterialPath, new Color(1f, 0.82f, 0.26f, 1f));
            Material surface = GetOrCreateMaterial(SurfaceImpactMaterialPath, new Color(1f, 0.5f, 0.12f, 1f));
            Material damageable = GetOrCreateMaterial(DamageableImpactMaterialPath, new Color(1f, 0.86f, 0.68f, 1f));

            CreateParticlePrefab(
                MuzzleFlashPrefabPath,
                "PH_FX_MuzzleFlash",
                muzzle,
                new Color(1f, 0.66f, 0.18f, 1f),
                0.05f,
                18,
                0.28f,
                overwrite);
            CreateTracerPrefab(TracerPrefabPath, tracer, overwrite);
            CreateParticlePrefab(
                SurfaceImpactPrefabPath,
                "PH_FX_SurfaceImpact",
                surface,
                new Color(1f, 0.5f, 0.12f, 1f),
                0.45f,
                22,
                0.12f,
                overwrite);
            CreateParticlePrefab(
                DamageableImpactPrefabPath,
                "PH_FX_DamageableImpact",
                damageable,
                new Color(1f, 0.86f, 0.68f, 1f),
                0.32f,
                14,
                0.16f,
                overwrite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateParticlePrefab(
            string prefabPath,
            string objectName,
            Material material,
            Color color,
            float lifetime,
            int burstCount,
            float startSize,
            bool overwrite)
        {
            if (overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);

            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            GameObject root = new GameObject(objectName);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ParticleSystem.MainModule main = particles.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = objectName.Contains("Impact") ? 1.8f : 0.2f;
            main.startSize = startSize;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = objectName.Contains("Impact")
                ? ParticleSystemShapeType.Cone
                : ParticleSystemShapeType.Sphere;
            shape.angle = 28f;
            shape.radius = 0.08f;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateTracerPrefab(string prefabPath, Material material, bool overwrite)
        {
            if (overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);

            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            GameObject root = new GameObject("PH_FX_BulletTracer");
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = 0.025f;
            line.endWidth = 0.006f;
            line.startColor = new Color(1f, 0.82f, 0.26f, 0.95f);
            line.endColor = new Color(1f, 0.82f, 0.26f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            material = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                color = color
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
