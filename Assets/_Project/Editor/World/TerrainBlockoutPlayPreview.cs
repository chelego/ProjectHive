#if UNITY_EDITOR
using ProjectHive.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.EditorTools.World
{
    /// <summary>
    /// Adds a disposable first-person controller only while TerrainBlockout is
    /// running in the Editor. Nothing is serialized into the map scene.
    /// </summary>
    [InitializeOnLoad]
    internal static class TerrainBlockoutPlayPreview
    {
        private const string TerrainScenePath = "Assets/_Project/Scenes/TerrainBlockout.unity";
        private const string PreviewPlayerName = "__TerrainBlockout_PlayPreview";
        private const string PivotKey = "ProjectHive.TerrainPreview.Pivot";
        private const string YawKey = "ProjectHive.TerrainPreview.Yaw";
        private const string HasViewKey = "ProjectHive.TerrainPreview.HasView";

        static TerrainBlockoutPlayPreview()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CacheSceneView();
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode ||
                SceneManager.GetActiveScene().path != TerrainScenePath ||
                GameObject.Find(PreviewPlayerName) != null)
            {
                return;
            }

            CreatePreviewPlayer();
        }

        private static void CacheSceneView()
        {
            if (SceneManager.GetActiveScene().path != TerrainScenePath)
            {
                SessionState.SetBool(HasViewKey, false);
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                SessionState.SetBool(HasViewKey, false);
                return;
            }

            SessionState.SetVector3(PivotKey, sceneView.pivot);
            SessionState.SetFloat(YawKey, sceneView.camera.transform.eulerAngles.y);
            SessionState.SetBool(HasViewKey, true);
        }

        private static void CreatePreviewPlayer()
        {
            DisableSceneCamerasAndListeners();
            Physics.SyncTransforms();

            Vector3 preferredPoint = SessionState.GetBool(HasViewKey, false)
                ? SessionState.GetVector3(PivotKey, Vector3.zero)
                : GetMapFallbackPoint();

            Vector3 spawnPosition = FindGroundedSpawn(preferredPoint);
            float yaw = SessionState.GetFloat(YawKey, 0f);

            GameObject player = new GameObject(PreviewPlayerName);
            player.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, yaw, 0f));

            GameObject view = new GameObject("PreviewCamera");
            view.tag = "MainCamera";
            view.transform.SetParent(player.transform, false);
            view.transform.localPosition = new Vector3(0f, 1.68f, 0f);
            view.AddComponent<Camera>();
            view.AddComponent<AudioListener>();

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 55f;
            controller.skinWidth = 0.04f;

            player.AddComponent<FirstPersonMotor>();
            Selection.activeGameObject = player;

            Debug.Log(
                $"[TerrainBlockout Preview] Temporary first-person player created at {spawnPosition}. " +
                "WASD move, Shift sprint, Space jump/parkour, C or Ctrl crouch/slide, mouse look.");
        }

        private static void DisableSceneCamerasAndListeners()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < cameras.Length; i++)
                cameras[i].enabled = false;

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < listeners.Length; i++)
                listeners[i].enabled = false;
        }

        private static Vector3 FindGroundedSpawn(Vector3 preferredPoint)
        {
            if (TryFindGround(preferredPoint, out Vector3 spawn))
                return spawn;

            Vector3 fallbackPoint = GetMapFallbackPoint();
            if (TryFindGround(fallbackPoint, out spawn))
                return spawn;

            fallbackPoint.y += 2f;
            return fallbackPoint;
        }

        private static bool TryFindGround(Vector3 point, out Vector3 spawn)
        {
            float rayStartY = Mathf.Max(point.y + 2000f, 4000f);
            Vector3 origin = new Vector3(point.x, rayStartY, point.z);

            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    10000f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                spawn = hit.point + Vector3.up * 0.05f;
                return true;
            }

            spawn = default;
            return false;
        }

        private static Vector3 GetMapFallbackPoint()
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains.Length > 0)
            {
                Terrain terrain = terrains[0];
                Vector3 position = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                return position + new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
            }

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            if (renderers.Length == 0)
                return Vector3.up * 2f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds.center + Vector3.up * bounds.extents.y;
        }
    }
}
#endif
