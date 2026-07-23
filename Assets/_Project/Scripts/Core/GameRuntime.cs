using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectHive.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRuntime : MonoBehaviour
    {
        public static GameRuntime Instance { get; private set; }

        [SerializeField, Min(30)] private int targetFrameRate = 120;
        [SerializeField] private bool runInBackground = true;

        public bool IsCursorCaptured => Cursor.lockState == CursorLockMode.Locked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Application.runInBackground = runInBackground;
            SetCursorCaptured(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsCursorCaptured)
                SetCursorCaptured(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SetCursorCaptured(false);
        }

        public void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
