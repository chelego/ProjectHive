using ProjectHive.Core;
using UnityEngine;

namespace ProjectHive.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PrototypeWeaponController weapons;

        private GUIStyle centeredStyle;
        private GUIStyle helpStyle;

        public void Configure(PlayerInteractor playerInteractor, PrototypeWeaponController weaponController)
        {
            interactor = playerInteractor;
            weapons = weaponController;
        }

        private void Awake()
        {
            if (interactor == null)
                interactor = GetComponent<PlayerInteractor>();
            if (weapons == null)
                weapons = GetComponent<PrototypeWeaponController>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(
                new Rect(Screen.width * 0.5f - 10f, Screen.height * 0.5f - 14f, 20f, 28f),
                "+",
                centeredStyle);

            string prompt = interactor != null ? interactor.CurrentPrompt : string.Empty;
            if (!string.IsNullOrEmpty(prompt))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 34f), GUIContent.none);
                GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 34f), prompt, centeredStyle);
            }

            string weaponName = weapons == null
                ? "-"
                : weapons.CurrentMode == PrototypeWeaponController.WeaponMode.Melee
                    ? "근접 무기"
                    : "물리 투사체 총";

            string cursorHint = GameRuntime.Instance != null && GameRuntime.Instance.IsCursorCaptured
                ? "Esc: 마우스 해제"
                : "좌클릭: 마우스 다시 잡기";

            GUI.Box(new Rect(18f, 18f, 295f, 104f), GUIContent.none);
            GUI.Label(
                new Rect(30f, 27f, 275f, 90f),
                "WASD 이동 / Shift 달리기 / Space 점프\n" +
                "E 상호작용 / 1 근접 / 2 총 / 좌클릭 공격\n" +
                "장착: " + weaponName + "\n" + cursorHint,
                helpStyle);
        }

        private void EnsureStyles()
        {
            if (centeredStyle == null)
            {
                centeredStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            if (helpStyle == null)
            {
                helpStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
            }
        }
    }
}
