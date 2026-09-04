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
