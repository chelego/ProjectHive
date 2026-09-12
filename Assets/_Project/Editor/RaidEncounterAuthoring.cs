#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectHive.AI.Mob;
using ProjectHive.Gameplay.Raid;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectHive.EditorTools
{
    public static class RaidEncounterAuthoring
    {
        private const string Root = "Assets/ThirdParty/ProjectHiveMonsterBirds";
        private const string ResourcesRoot = Root + "/Resources/HiveEncounters";

        [MenuItem("ProjectHive/Encounters/Prepare Assets From Vertical Slice")]
        public static void Prepare()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/_Project/Integration/VerticalSlice/Scenes/VerticalSlice.unity")
                throw new InvalidOperationException("Open VerticalSlice in edit mode before preparing encounter assets.");
            if (scene.isDirty) throw new InvalidOperationException("Save your current scene edits first.");
            string fbxPath = Root + "/Model/AshRaven.fbx";
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Contains("Flight") ? "Flight" : "Feed";
                clip.loopTime = true; clip.wrapMode = WrapMode.Loop;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Material feather = MakeMaterial("Feathers", new Color(0.7f, 0.78f, 0.84f), 0.22f);
            feather.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/AshRaven_Feathers.png"));
            EditorUtility.SetDirty(feather);
            Material horn = MakeMaterial("Horn", new Color(0.075f, 0.064f, 0.05f), 0.35f);
            Material skin = MakeMaterial("Skin", new Color(0.085f, 0.035f, 0.027f), 0.12f);
            Material eye = MakeMaterial("Eye", new Color(0.2f, 0.16f, 0.08f), 0.62f);
            Material cube = MakeMaterial("SpotterBody", new Color(0.045f, 0.06f, 0.065f), 0.25f);
            GameObject lamp = new GameObject("SpotterSearchlight");
            try
            {
                lamp.AddComponent<Light>().shadowCustomResolution = 256;
                var lightData = lamp.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                var serializedLight = new SerializedObject(lightData);
                serializedLight.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 0;
                serializedLight.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(lamp, ResourcesRoot + "/SpotterSearchlight.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(lamp); }
            MakeDiagnosticMaterial("DiagnosticLine", UnityEngine.Rendering.CompareFunction.Always);
            Material beam = MakeDiagnosticMaterial("SearchlightBeam", UnityEngine.Rendering.CompareFunction.LessEqual);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject bird = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                bird.name = "AshRaven";
                foreach (Renderer renderer in bird.GetComponentsInChildren<Renderer>(true))
                {
                    // FBX slot order is authored in build_monster_bird.py: feather / skin / horn / eye.
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = i == 1 ? skin : i == 2 ? horn : i == 3 ? eye : feather;
                    renderer.sharedMaterials = mats;
                }
                Animation animation = bird.GetComponent<Animation>() ?? bird.AddComponent<Animation>();
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__")) continue;
                    animation.AddClip(clip, clip.name);
                    if (clip.name == "Feed") animation.clip = clip;
                }
                animation.playAutomatically = true;
                animation.wrapMode = WrapMode.Loop;
                animation.cullingType = AnimationCullingType.BasedOnRenderers;
                var lod = bird.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.012f, bird.GetComponentsInChildren<Renderer>()) });
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(bird, ResourcesRoot + "/MonsterBird.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(bird); }

            RaidEncounterLayout layout = AssetDatabase.LoadAssetAtPath<RaidEncounterLayout>(ResourcesRoot + "/RaidEncounterLayout.asset");
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<RaidEncounterLayout>();
                AssetDatabase.CreateAsset(layout, ResourcesRoot + "/RaidEncounterLayout.asset");
            }
            layout.birdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesRoot + "/MonsterBird.prefab");
            layout.beamMaterial = beam; layout.spotterMaterial = cube;
            layout.birdCalls = Enumerable.Range(1, 3).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/AshRaven_Alarm_" + i.ToString("00") + ".wav")).ToArray();
            var points = new List<Vector3>();
            var centers = new List<Vector3>();
            foreach (PrototypeSpawnDistrict district in UnityEngine.Object.FindObjectsByType<PrototypeSpawnDistrict>(FindObjectsSortMode.None).OrderBy(d => d.DistrictName))
            {
                int count = 0;
                for (int i = 0; i < district.CandidateCount && count < 6; i++)
                {
                    Vector3 candidate = district.CandidateAt(i);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                    if (!RaidEncounterBootstrap.HasOutdoorClearance(hit.position, 2.5f)) continue;
                    if (points.Any(p => Vector3.Distance(p, hit.position) < 32f)) continue;
                    points.Add(hit.position);
                    if (count == 0) centers.Add(hit.position);
                    count++;
                }
            }
            layout.outdoorPoints = points.ToArray(); layout.patrolCenters = centers.ToArray();
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null)
            {
                layout.mapNavMesh = surface.navMeshData;
                layout.navMeshPosition = surface.transform.position;
                layout.navMeshRotation = surface.transform.rotation;
            }
            BreckenAI template = UnityEngine.Object.FindObjectsByType<BreckenAI>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (template != null)
            {
                // This template contains only the team's simple AI placeholder, never ProBuilder scenery.
                GameObject clone = UnityEngine.Object.Instantiate(template.gameObject);
                try
                {
                    clone.name = "Brecken_Encounter";
                    clone.transform.SetParent(null); clone.transform.position = Vector3.zero;
                    clone.GetComponent<BreckenAI>().enabled = true;
                    var serialized = new SerializedObject(clone.GetComponent<BreckenAI>());
                    foreach (string field in new[] { "playerTransform", "runtimeCoordinator", "hiveUnitRegistry" })
                        serialized.FindProperty(field).objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    layout.breckenPrefab = PrefabUtility.SaveAsPrefabAsset(clone, ResourcesRoot + "/Brecken_Encounter.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            var raid = UnityEngine.Object.FindFirstObjectByType<VerticalSliceRaidController>();
            var clock = UnityEngine.Object.FindFirstObjectByType<ProjectHive.Core.Runtime.RaidClock>();
            if (raid != null)
            {
                var serialized = new SerializedObject(raid);
                serialized.FindProperty("raidDurationSeconds").floatValue = 1800f;
                serialized.ApplyModifiedProperties();
            }
            if (clock != null)
            {
                var serialized = new SerializedObject(clock);
                serialized.FindProperty("durationSeconds").floatValue = 1800f;
                serialized.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RaidEncounters] Authored {points.Count} outdoor flocks / {centers.Count} patrol areas; raid=1800s.");
        }

        private static Material MakeDiagnosticMaterial(string name, UnityEngine.Rendering.CompareFunction depthTest)
        {
            string path = ResourcesRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ResourcesRoot + "/Diagnostic.shader"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_ZTest", (float)depthTest);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MakeMaterial(string name, Color color, float smoothness)
        {
            string path = ResourcesRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
