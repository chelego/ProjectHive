#if UNITY_EDITOR
using ProjectHive.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectHive.EditorTools
{
    internal static class ParkourPrototypePrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Interaction/Parkour";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string VaultPrefabPath = PrefabFolder + "/PH_Parkour_VaultBarrier.prefab";
        private const string MantlePrefabPath = PrefabFolder + "/PH_Parkour_MantleWall.prefab";
        private const string WindowPrefabPath = PrefabFolder + "/PH_Parkour_WindowPassage.prefab";
        private const string VaultMaterialPath = MaterialFolder + "/M_Parkour_VaultBarrier.mat";
        private const string MantleMaterialPath = MaterialFolder + "/M_Parkour_MantleWall.mat";
        private const string WindowMaterialPath = MaterialFolder + "/M_Parkour_WindowPassage.mat";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabsAfterReload()
        {
            EditorApplication.delayCall -= EnsurePrefabs;
            EditorApplication.delayCall += EnsurePrefabs;
        }

        [MenuItem("Tools/ProjectHive/Parkour/Rebuild Prototype Obstacles")]
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
            EnsureFolder("Assets/_Project/Prefabs", "Interaction");
            EnsureFolder("Assets/_Project/Prefabs/Interaction", "Parkour");
            EnsureFolder("Assets/_Project/Art", "Materials");

            Material vaultMaterial = GetOrCreateMaterial(VaultMaterialPath, new Color(0.78f, 0.86f, 0.72f));
            Material mantleMaterial = GetOrCreateMaterial(MantleMaterialPath, new Color(0.65f, 0.72f, 0.78f));
            Material windowMaterial = GetOrCreateMaterial(WindowMaterialPath, new Color(0.72f, 0.68f, 0.58f));

            CreateObstaclePrefab(
                VaultPrefabPath,
                "PH_Parkour_VaultBarrier",
                ParkourPrototypeObstacleKind.VaultBarrier,
                new Vector3(2.2f, 0.85f, 0.45f),
                vaultMaterial,
                "Low barrier for jump-key vault detection and landing clearance checks.",
                overwrite);

            CreateObstaclePrefab(
                MantlePrefabPath,
                "PH_Parkour_MantleWall",
                ParkourPrototypeObstacleKind.MantleWall,
                new Vector3(2.2f, 1.65f, 0.55f),
                mantleMaterial,
                "High wall for jump-key mantle detection, top probe, and climb landing checks.",
                overwrite);

            CreateWindowPassagePrefab(
                WindowPrefabPath,
                windowMaterial,
                overwrite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateObstaclePrefab(
            string prefabPath,
            string objectName,
            ParkourPrototypeObstacleKind kind,
            Vector3 dimensions,
            Material material,
            string purpose,
            bool overwrite)
        {
            if (overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);

            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = objectName;
            obstacle.transform.position = Vector3.up * (dimensions.y * 0.5f);
            obstacle.transform.localScale = dimensions;
            obstacle.layer = 0;

            MeshRenderer renderer = obstacle.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            ParkourPrototypeObstacle marker = obstacle.AddComponent<ParkourPrototypeObstacle>();
            marker.Configure(kind, dimensions, purpose);

            PrefabUtility.SaveAsPrefabAsset(obstacle, prefabPath);
            Object.DestroyImmediate(obstacle);
        }

        private static void CreateWindowPassagePrefab(string prefabPath, Material material, bool overwrite)
        {
            if (overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);

            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                return;

            GameObject root = new GameObject("PH_Parkour_WindowPassage");
            ParkourPrototypeObstacle marker = root.AddComponent<ParkourPrototypeObstacle>();
            marker.Configure(
                ParkourPrototypeObstacleKind.WindowPassage,
                new Vector3(1.8f, 1.98f, 0.28f),
                "Window-like frame for vaulting over a sill while crouching through the opening.");

            CreateFramePart(root.transform, "Sill", new Vector3(1.8f, 0.5f, 0.28f), new Vector3(0f, 0.25f, 0f), material);
            CreateFramePart(root.transform, "Left_Jamb", new Vector3(0.18f, 1.2f, 0.28f), new Vector3(-0.81f, 1.125f, 0f), material);
            CreateFramePart(root.transform, "Right_Jamb", new Vector3(0.18f, 1.2f, 0.28f), new Vector3(0.81f, 1.125f, 0f), material);
            CreateFramePart(root.transform, "Header", new Vector3(1.8f, 0.25f, 0.28f), new Vector3(0f, 1.85f, 0f), material);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateFramePart(Transform parent, string name, Vector3 scale, Vector3 position, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.layer = 0;

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

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
