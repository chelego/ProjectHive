using UnityEngine;

namespace ProjectHive.Player
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PlayerBodyMirrorCamera : MonoBehaviour
    {
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private Camera mirrorCamera;
        [SerializeField] private Transform mirrorSurface;
        [SerializeField] private Transform bodyTarget;
        [SerializeField] private string mirrorSurfaceLayerName = "MirrorSurface";
        [SerializeField] private string[] additionalVisibleLayerNames = { "PlayerViewModel" };
        [SerializeField] private float nearClipPlane = 0.05f;
        [SerializeField] private float farClipPlane = 35f;
        [SerializeField] private float surfaceCameraOffset = 0.2f;
        [SerializeField] private float verticalAimOffset = 0.95f;
        [SerializeField] private float mirrorFieldOfView = 32f;

        private void OnEnable()
        {
            ResolveReferences();
            UpdateMirrorCamera();
        }

        private void LateUpdate()
        {
            UpdateMirrorCamera();
        }

        private void ResolveReferences()
        {
            if (sourceCamera == null)
                sourceCamera = Camera.main;

            if (mirrorCamera == null)
                mirrorCamera = GetComponentInChildren<Camera>(true);

            if (mirrorSurface == null)
            {
                Transform surface = transform.Find("Mirror Screen");
                if (surface != null)
                    mirrorSurface = surface;
            }

            if (bodyTarget == null && sourceCamera != null)
            {
                Transform playerRoot = sourceCamera.transform.root;
                Transform visual = playerRoot.Find("PH_PlayerBody_Visual");
                bodyTarget = visual != null ? visual : playerRoot;
            }
        }

        private void UpdateMirrorCamera()
        {
            ResolveReferences();
            if (sourceCamera == null || mirrorCamera == null || mirrorSurface == null)
                return;

            Vector3 normal = mirrorSurface.forward;
            Vector3 cameraPosition = mirrorSurface.position + normal * surfaceCameraOffset;
            Vector3 targetPosition = bodyTarget != null
                ? bodyTarget.position + Vector3.up * verticalAimOffset
                : sourceCamera.transform.position;
            Vector3 lookDirection = targetPosition - cameraPosition;

            if (lookDirection.sqrMagnitude < 0.0001f)
                lookDirection = normal;

            mirrorCamera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(lookDirection.normalized, Vector3.up));

            mirrorCamera.fieldOfView = mirrorFieldOfView;
            mirrorCamera.nearClipPlane = nearClipPlane;
            mirrorCamera.farClipPlane = farClipPlane;
            mirrorCamera.clearFlags = CameraClearFlags.SolidColor;

            int mirrorLayer = LayerMask.NameToLayer(mirrorSurfaceLayerName);
            if (mirrorLayer >= 0)
                mirrorCamera.cullingMask &= ~(1 << mirrorLayer);

            if (additionalVisibleLayerNames == null)
                return;

            for (int i = 0; i < additionalVisibleLayerNames.Length; i++)
            {
                int layer = LayerMask.NameToLayer(additionalVisibleLayerNames[i]);
                if (layer >= 0)
                    mirrorCamera.cullingMask |= 1 << layer;
            }
        }
    }
}
