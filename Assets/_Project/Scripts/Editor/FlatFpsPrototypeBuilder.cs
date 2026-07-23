using System;
using System.Collections.Generic;
using System.IO;
using ProjectHive.Core;
using ProjectHive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectHive.EditorTools
{
    [InitializeOnLoad]
    public static class FlatFpsPrototypeBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype_FlatFPS.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Prototype/Materials";
        private const string PreviewFolderName = "FlatFPS";

        static FlatFpsPrototypeBuilder()
        {
            EditorApplication.delayCall += BuildWhenReady;
        }

        private static void BuildWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += BuildWhenReady;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            BuildPrototype();
        }

        [MenuItem("Project Hive/Prototype/Build Flat FPS Blockout")]
        public static void BuildPrototype()
        {
            EditorSceneManager.SaveOpenScenes();
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder("Assets/_Project/Art/Prototype");
            EnsureFolder(MaterialFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject sceneRoot = new GameObject("Prototype_FlatFPS");

            BuildRuntime(sceneRoot.transform);
            BuildLighting(sceneRoot.transform);
            BuildEnvironment(sceneRoot.transform);
            Camera playerCamera = BuildPlayer(sceneRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            CapturePreview(playerCamera);
            RemoveLegacyPrototypeAssets();

            Selection.activeGameObject = sceneRoot;
            Debug.Log("[FlatFpsPrototypeBuilder] Built and opened " + ScenePath);
        }

        private static void BuildRuntime(Transform parent)
        {
            GameObject runtime = new GameObject("GameRuntime", typeof(GameRuntime));
            runtime.transform.SetParent(parent, false);
        }

        private static void BuildLighting(Transform parent)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.32f);
            RenderSettings.fog = false;

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.86f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
        }

        private static void BuildEnvironment(Transform parent)
        {
            Transform environment = NewGroup("Blockout_Environment", parent);

            Material ground = CreateMaterial("M_Ground", new Color(0.16f, 0.18f, 0.20f), 0f, 0.18f);
            Material building = CreateMaterial("M_Building", new Color(0.31f, 0.34f, 0.38f), 0f, 0.12f);
            Material buildingDark = CreateMaterial("M_BuildingDark", new Color(0.19f, 0.22f, 0.26f), 0f, 0.10f);
            Material obstacle = CreateMaterial("M_Obstacle", new Color(0.58f, 0.24f, 0.08f), 0f, 0.20f);

            CreateBlock("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), ground, environment);

            BlockSpec[] buildings =
            {
                new("Building_A01", new Vector3(-22f, 4f, -20f), new Vector3(14f, 8f, 18f)),
                new("Building_A02", new Vector3(-23f, 6f, 3f), new Vector3(12f, 12f, 15f)),
                new("Building_A03", new Vector3(-20f, 3f, 24f), new Vector3(17f, 6f, 14f)),
                new("Building_B01", new Vector3(21f, 5f, -23f), new Vector3(16f, 10f, 13f)),
                new("Building_B02", new Vector3(24f, 3.5f, -3f), new Vector3(13f, 7f, 14f)),
                new("Building_B03", new Vector3(21f, 7f, 22f), new Vector3(18f, 14f, 16f)),
                new("Building_C01", new Vector3(-43f, 4.5f, 7f), new Vector3(15f, 9f, 28f)),
                new("Building_C02", new Vector3(43f, 5.5f, 9f), new Vector3(17f, 11f, 25f)),
                new("Building_D01", new Vector3(-7f, 3f, 43f), new Vector3(22f, 6f, 13f)),
                new("Building_D02", new Vector3(18f, 4f, 43f), new Vector3(18f, 8f, 13f))
            };

            for (int index = 0; index < buildings.Length; index++)
            {
                BlockSpec spec = buildings[index];
                Material material = (index & 1) == 0 ? building : buildingDark;
                CreateBlock(spec.Name, spec.Position, spec.Scale, material, environment);
            }

            CreateBlock("Low_Wall_Left", new Vector3(-7f, 1f, 8f), new Vector3(9f, 2f, 1f), buildingDark, environment);
            CreateBlock("Low_Wall_Right", new Vector3(8f, 1f, 14f), new Vector3(10f, 2f, 1f), buildingDark, environment);

            Vector3[] obstaclePositions =
            {
                new(-4f, 0.75f, -4f),
                new(4f, 0.75f, 1f),
                new(-2f, 0.75f, 20f),
                new(7f, 0.75f, 29f),
                new(-10f, 0.75f, 32f)
            };

            for (int index = 0; index < obstaclePositions.Length; index++)
                CreateBlock("Obstacle_" + (index + 1).ToString("00"), obstaclePositions[index], new Vector3(2f, 1.5f, 2f), obstacle, environment);
        }

        private static Camera BuildPlayer(Transform parent)
        {
            GameObject player = new GameObject(
                "Player",
                typeof(CharacterController),
                typeof(SimpleFirstPersonController));

            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0f, 0.05f, -34f);

            CharacterController controller = player.GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;

            GameObject cameraObject = new GameObject(
                "PlayerCamera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(UniversalAdditionalCameraData));

            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 220f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.38f, 0.47f);
            camera.allowHDR = true;

            UniversalAdditionalCameraData cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            player.GetComponent<SimpleFirstPersonController>().Configure(camera);
            return camera;
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
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

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RemoveLegacyPrototypeAssets()
        {
            string[] legacyAssets =
            {
                "Assets/_Project/Cinematic",
                "Assets/_Project/Maps",
                "Assets/_Project/Scripts/Editor/OrganicCityMapBuilder.cs"
            };

            foreach (string assetPath in legacyAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || AssetDatabase.IsValidFolder(assetPath))
                {
                    bool deleted = AssetDatabase.DeleteAsset(assetPath);
                    Debug.Log("[FlatFpsPrototypeBuilder] Removed legacy asset: " + assetPath + " result=" + deleted);
                }
            }
        }

        private static void CapturePreview(Camera camera)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string folder = Path.Combine(projectRoot, "Captures", PreviewFolderName);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "Prototype_FlatFPS.png");

            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static Transform NewGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string child = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, child);
        }

        private readonly struct BlockSpec
        {
            public BlockSpec(string name, Vector3 position, Vector3 scale)
            {
                Name = name;
                Position = position;
                Scale = scale;
            }

            public string Name { get; }
            public Vector3 Position { get; }
            public Vector3 Scale { get; }
        }
    }
}
