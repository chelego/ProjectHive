using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class FirstPersonFlashlight : MonoBehaviour
    {
        [SerializeField] private bool startEnabled;

        private Light flashlight;

        public bool IsOn => flashlight != null && flashlight.enabled;

        // Shared by the terrain preview and the playable vertical slice.
        public static FirstPersonFlashlight CreateFor(Transform cameraTransform, string objectName = "Flashlight")
        {
            if (cameraTransform == null)
                throw new System.ArgumentNullException(nameof(cameraTransform));

            FirstPersonFlashlight existing = cameraTransform.GetComponentInChildren<FirstPersonFlashlight>(true);
            if (existing != null)
                return existing;

            GameObject flashlightObject = new GameObject(objectName);
            flashlightObject.transform.SetParent(cameraTransform, false);
            flashlightObject.transform.localPosition = new Vector3(0.08f, -0.08f, 0.15f);

            Light light = flashlightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.82f, 0.9f, 1f);
            light.intensity = 14f;
            light.range = 45f;
            light.spotAngle = 62f;
            light.innerSpotAngle = 38f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.4f;
            light.shadowNearPlane = 0.1f;
            light.enabled = false;
            return flashlightObject.AddComponent<FirstPersonFlashlight>();
        }

        private void Awake()
        {
            flashlight = GetComponent<Light>();
            flashlight.enabled = startEnabled;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Toggle();
        }

        public void Toggle()
        {
            SetIsOn(!IsOn);
        }

        public void SetIsOn(bool isOn)
        {
            if (flashlight == null)
                flashlight = GetComponent<Light>();

            flashlight.enabled = isOn;
        }
    }
}
