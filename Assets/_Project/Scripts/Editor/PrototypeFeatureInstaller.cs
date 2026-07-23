using System.Collections.Generic;
using ProjectHive.Player;
using ProjectHive.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectHive.EditorTools
{
    [InitializeOnLoad]
    public static class PrototypeFeatureInstaller
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype_FlatFPS.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Prototype/Materials";
        private const string MarkerName = "Prototype_Features_v1";

        static PrototypeFeatureInstaller()
        {
            EditorApplication.delayCall += InstallWhenReady;
        }

        private static void InstallWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += InstallWhenReady;
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath || GameObject.Find(MarkerName) != null)
                return;

            InstallFeatures();
        }

        [MenuItem("Project Hive/Prototype/Install Interaction and Combat")]
        public static void InstallFeatures()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("[PrototypeFeatureInstaller] Flat FPS scene does not exist.");
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existingMarker = GameObject.Find(MarkerName);
            if (existingMarker != null)
            {
                Debug.Log("[PrototypeFeatureInstaller] Features are already installed.");
                return;
            }

            GameObject sceneRoot = GameObject.Find("Prototype_FlatFPS");
            GameObject player = GameObject.Find("Prototype_FlatFPS/Player");
            Camera camera = Camera.main;
            if (sceneRoot == null || player == null || camera == null)
            {
                Debug.LogError("[PrototypeFeatureInstaller] Required Flat FPS objects were not found.");
                return;
            }

            Material doorMaterial = CreateMaterial("M_Door", new Color(0.055f, 0.065f, 0.075f), 0.72f, 0.32f);
            Material frameMaterial = CreateMaterial("M_DoorFrame", new Color(0.13f, 0.15f, 0.17f), 0.45f, 0.2f);
            Material panelMaterial = CreateEmissiveMaterial("M_DoorPanel", new Color(1f, 0.12f, 0.05f), 1.2f);
            Material enemyMaterial = CreateMaterial("M_EnemyPrototype", new Color(0.34f, 0.08f, 0.07f), 0.05f, 0.24f);
            Material eyeMaterial = CreateEmissiveMaterial("M_EnemyEye", new Color(1f, 0.16f, 0.04f), 2.4f);
            Material meleeMaterial = CreateMaterial("M_MeleePrototype", new Color(0.17f, 0.19f, 0.22f), 0.78f, 0.3f);
            Material gunMaterial = CreateMaterial("M_GunPrototype", new Color(0.075f, 0.085f, 0.095f), 0.82f, 0.36f);
            Material projectileMaterial = CreateEmissiveMaterial("M_ProjectilePrototype", new Color(1f, 0.55f, 0.08f), 3f);

            GameObject featureRoot = new GameObject(MarkerName);
            featureRoot.transform.SetParent(sceneRoot.transform, false);

            PrototypeProjectilePool projectilePool = BuildProjectilePool(featureRoot.transform, projectileMaterial);
            BuildInteractionTest(featureRoot.transform, doorMaterial, frameMaterial, panelMaterial);
            BuildEnemyTest(featureRoot.transform, enemyMaterial, eyeMaterial);

            BuildPlayerFeatures(
                player,
                camera,
                projectilePool,
                meleeMaterial,
                gunMaterial,
                projectileMaterial);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = featureRoot;
            Debug.Log("[PrototypeFeatureInstaller] Installed head bob, interaction, melee, enemies, and pooled physical projectiles.");
        }

        private static void BuildPlayerFeatures(
            GameObject player,
            Camera camera,
            PrototypeProjectilePool pool,
            Material meleeMaterial,
            Material gunMaterial,
            Material projectileMaterial)
        {
            FirstPersonViewEffects viewEffects = camera.GetComponent<FirstPersonViewEffects>();
            if (viewEffects == null)
                viewEffects = camera.gameObject.AddComponent<FirstPersonViewEffects>();

            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null)
                interactor = player.AddComponent<PlayerInteractor>();
            interactor.Configure(camera);

            PrototypeWeaponController weapons = player.GetComponent<PrototypeWeaponController>();
            if (weapons == null)
                weapons = player.AddComponent<PrototypeWeaponController>();

            Transform weaponRoot = NewGroup("Prototype_ViewWeapons", camera.transform);
            weaponRoot.localPosition = Vector3.zero;
            weaponRoot.localRotation = Quaternion.identity;

            GameObject meleePivotObject = new GameObject("Melee_Pivot");
            meleePivotObject.transform.SetParent(weaponRoot, false);
            meleePivotObject.transform.localPosition = new Vector3(0.38f, -0.39f, 0.72f);
            meleePivotObject.transform.localRotation = Quaternion.Euler(18f, 0f, -22f);

            GameObject meleeVisual = CreatePrimitive(
                "Improvised_Baton",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(0.055f, 0.42f, 0.055f),
                meleeMaterial,
                meleePivotObject.transform,
                false);
            meleeVisual.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            meleeVisual.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);

            GameObject meleeHead = CreatePrimitive(
                "Baton_Head",
                PrimitiveType.Cube,
                new Vector3(0f, 0.46f, 0f),
                new Vector3(0.14f, 0.16f, 0.14f),
                meleeMaterial,
                meleeVisual.transform,
                false);
            meleeHead.transform.localRotation = Quaternion.identity;

            GameObject gunPivotObject = new GameObject("Gun_Pivot");
            gunPivotObject.transform.SetParent(weaponRoot, false);
            gunPivotObject.transform.localPosition = new Vector3(0.29f, -0.28f, 0.64f);

            GameObject gunVisual = new GameObject("Prototype_Gun");
            gunVisual.transform.SetParent(gunPivotObject.transform, false);

            CreatePrimitive(
                "Gun_Body",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(0.18f, 0.16f, 0.48f),
                gunMaterial,
                gunVisual.transform,
                false);
            CreatePrimitive(
                "Gun_Barrel",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.025f, 0.35f),
                new Vector3(0.045f, 0.26f, 0.045f),
                gunMaterial,
                gunVisual.transform,
                false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreatePrimitive(
                "Gun_Grip",
                PrimitiveType.Cube,
                new Vector3(0f, -0.19f, -0.06f),
                new Vector3(0.12f, 0.27f, 0.13f),
                gunMaterial,
                gunVisual.transform,
                false).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            GameObject muzzleObject = new GameObject("Muzzle");
            muzzleObject.transform.SetParent(gunVisual.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 0.025f, 0.64f);

            GameObject muzzleMarker = CreatePrimitive(
                "Muzzle_Marker",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one * 0.05f,
                projectileMaterial,
                muzzleObject.transform,
                false);
            muzzleMarker.SetActive(true);

            weapons.Configure(
                camera,
                meleePivotObject.transform,
                meleePivotObject,
                gunPivotObject.transform,
                gunPivotObject,
                muzzleObject.transform,
                pool);

            PrototypeHud hud = player.GetComponent<PrototypeHud>();
            if (hud == null)
                hud = player.AddComponent<PrototypeHud>();
            hud.Configure(interactor, weapons);
        }

        private static PrototypeProjectilePool BuildProjectilePool(Transform parent, Material projectileMaterial)
        {
            GameObject poolObject = new GameObject("Prototype_ProjectilePool");
            poolObject.transform.SetParent(parent, false);
            PrototypeProjectilePool pool = poolObject.AddComponent<PrototypeProjectilePool>();
            pool.Configure(projectileMaterial, 24);
            return pool;
        }

        private static void BuildInteractionTest(
            Transform parent,
            Material doorMaterial,
            Material frameMaterial,
            Material panelMaterial)
        {
            Transform group = NewGroup("Interaction_Test", parent);

            CreatePrimitive("Wall_Left", PrimitiveType.Cube, new Vector3(5.15f, 2f, -17f), new Vector3(2.3f, 4f, 0.65f), frameMaterial, group);
            CreatePrimitive("Wall_Right", PrimitiveType.Cube, new Vector3(9.85f, 2f, -17f), new Vector3(2.3f, 4f, 0.65f), frameMaterial, group);
            CreatePrimitive("Wall_Top", PrimitiveType.Cube, new Vector3(7.5f, 3.65f, -17f), new Vector3(2.4f, 0.7f, 0.65f), frameMaterial, group);

            GameObject door = CreatePrimitive(
                "Prototype_Door",
                PrimitiveType.Cube,
                new Vector3(7.5f, 1.5f, -17.37f),
                new Vector3(2.1f, 3f, 0.18f),
                doorMaterial,
                group);
            door.transform.localPosition = new Vector3(7.5f, 1.5f, -17.37f);

            GameObject panel = CreatePrimitive(
                "Door_Control_Panel",
                PrimitiveType.Cube,
                new Vector3(8.9f, 1.35f, -17.52f),
                new Vector3(0.36f, 0.58f, 0.14f),
                frameMaterial,
                group);

            GameObject indicator = CreatePrimitive(
                "Panel_Indicator",
                PrimitiveType.Cube,
                new Vector3(0f, 0.08f, -0.09f),
                new Vector3(0.16f, 0.16f, 0.04f),
                panelMaterial,
                panel.transform,
                false);

            GameObject lightObject = new GameObject("Panel_Light", typeof(Light));
            lightObject.transform.SetParent(panel.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.08f, -0.24f);
            Light indicatorLight = lightObject.GetComponent<Light>();
            indicatorLight.type = LightType.Point;
            indicatorLight.range = 1.5f;
            indicatorLight.intensity = 0.8f;
            indicatorLight.color = new Color(1f, 0.12f, 0.05f);

            PrototypeDoorPanel doorPanel = panel.AddComponent<PrototypeDoorPanel>();
            doorPanel.Configure(door.transform, indicator.GetComponent<Renderer>(), indicatorLight);
        }

        private static void BuildEnemyTest(Transform parent, Material enemyMaterial, Material eyeMaterial)
        {
            Transform group = NewGroup("Combat_Test_Enemies", parent);
            CreateEnemy("Enemy_Test_01", new Vector3(0f, 1.15f, -8f), enemyMaterial, eyeMaterial, group);
            CreateEnemy("Enemy_Test_02", new Vector3(-4.5f, 1.15f, 2f), enemyMaterial, eyeMaterial, group);
            CreateEnemy("Enemy_Test_03", new Vector3(5.5f, 1.15f, 10f), enemyMaterial, eyeMaterial, group);
        }

        private static void CreateEnemy(
            string name,
            Vector3 position,
            Material enemyMaterial,
            Material eyeMaterial,
            Transform parent)
        {
            GameObject body = CreatePrimitive(
                name,
                PrimitiveType.Capsule,
                position,
                new Vector3(0.82f, 1.15f, 0.82f),
                enemyMaterial,
                parent);

            GameObject head = CreatePrimitive(
                "Head",
                PrimitiveType.Sphere,
                new Vector3(0f, 1.13f, 0f),
                new Vector3(0.72f, 0.68f, 0.72f),
                enemyMaterial,
                body.transform,
                false);
            Object.DestroyImmediate(head.GetComponent<Collider>());

            GameObject eye = CreatePrimitive(
                "Eye",
                PrimitiveType.Sphere,
                new Vector3(0f, 0.05f, -0.34f),
                new Vector3(0.18f, 0.12f, 0.08f),
                eyeMaterial,
                head.transform,
                false);
            Object.DestroyImmediate(eye.GetComponent<Collider>());

            PrototypeHealth health = body.AddComponent<PrototypeHealth>();
            health.Configure(100f);

            PrototypeEnemy enemy = body.AddComponent<PrototypeEnemy>();
            enemy.Configure(new[]
            {
                body.GetComponent<Renderer>(),
                head.GetComponent<Renderer>()
            });
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent,
            bool worldSpace = true)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);

            if (worldSpace)
                gameObject.transform.position = position;
            else
                gameObject.transform.localPosition = position;

            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;

            if (!worldSpace)
            {
                Collider collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }

            return gameObject;
        }

        private static Transform NewGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateEmissiveMaterial(string name, Color color, float strength)
        {
            Material material = CreateMaterial(name, color, 0.1f, 0.4f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * strength);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
