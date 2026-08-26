using UnityEngine;

namespace ProjectHive.Player
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PlayerBodyPreviewCamera : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject bodyVisual;
        [SerializeField] private Vector3 editModeCameraLocalPosition = new(0f, 0.45f, -3.25f);
        [SerializeField] private Vector3 editModeCameraLocalEulerAngles = new(10f, 0f, 0f);
        [SerializeField] private Vector3 playModeCameraLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 playModeCameraLocalEulerAngles = Vector3.zero;
        [SerializeField] private bool hideBodyVisualInPlayMode = true;
        [SerializeField] private string bodyVisualLayerName = "PlayerBody";

        private Renderer[] bodyRenderers;

        private void OnEnable()
        {
            ResolveReferences();
            ApplyCurrentMode();
        }

        private void Start()
        {
            ApplyCurrentMode();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                ApplyEditModePreview();
        }

        private void ResolveReferences()
        {
            if (cameraRoot == null)
                cameraRoot = transform.Find("Camera Root");

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true);

            if (bodyVisual == null)
            {
                Transform visual = transform.Find("PH_PlayerBody_Visual");
                if (visual != null)
                    bodyVisual = visual.gameObject;
            }

            bodyRenderers = bodyVisual != null
                ? bodyVisual.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
        }

        private void ApplyCurrentMode()
        {
            ResolveReferences();

            if (Application.isPlaying)
                ApplyPlayModeView();
            else
                ApplyEditModePreview();
        }

        private void ApplyEditModePreview()
        {
            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = editModeCameraLocalPosition;
                playerCamera.transform.localRotation = Quaternion.Euler(editModeCameraLocalEulerAngles);
                SetPlayerCameraBodyLayerVisible(true);
            }

            SetBodyRenderersVisible(true);
        }

        private void ApplyPlayModeView()
        {
            if (cameraRoot != null)
                cameraRoot.localPosition = new Vector3(0f, 1.62f, 0f);

            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = playModeCameraLocalPosition;
                playerCamera.transform.localRotation = Quaternion.Euler(playModeCameraLocalEulerAngles);
                SetPlayerCameraBodyLayerVisible(!hideBodyVisualInPlayMode);
            }

            SetBodyRenderersVisible(true);
        }

        private void SetBodyRenderersVisible(bool isVisible)
        {
            if (bodyRenderers == null)
                return;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].enabled = isVisible;
            }
        }

        private void SetPlayerCameraBodyLayerVisible(bool isVisible)
        {
            int bodyLayer = LayerMask.NameToLayer(bodyVisualLayerName);
            if (bodyLayer < 0)
                return;

            if (bodyVisual != null)
                SetLayerRecursively(bodyVisual.transform, bodyLayer);

            if (playerCamera == null)
                return;

            int bodyMask = 1 << bodyLayer;
            playerCamera.cullingMask = isVisible
                ? playerCamera.cullingMask | bodyMask
                : playerCamera.cullingMask & ~bodyMask;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
