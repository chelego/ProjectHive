using ProjectHive.Interaction;
using UnityEngine;

namespace ProjectHive.Gameplay.Raid
{
    [DisallowMultipleComponent]
    public sealed class VerticalSliceHud : MonoBehaviour
    {
        [SerializeField] private VerticalSliceRaidController controller;
        private GUIStyle labelStyle;
        private GUIStyle centeredStyle;

        public void Configure(VerticalSliceRaidController raidController)
        {
            controller = raidController;
        }

        private void OnGUI()
        {
            if (controller == null)
                return;

            EnsureStyles();
            var clock = controller.RaidClock;
            string time = clock != null
                ? $"{clock.ClockState.WorldHour:00}:{clock.ClockState.WorldMinute:00}"
                : "--:--";
            GUI.Label(new Rect(Screen.width - 180f, 24f, 150f, 40f), time, labelStyle);
            GUI.Label(new Rect(24f, 24f, 500f, 90f),
                $"통합 테스트 | 탈출 가능 벙커 {controller.AvailableGates.Count}개\nWASD 이동 / Shift 달리기 / Space 점프 / E 상호작용",
                labelStyle);

            if (controller.Player != null)
            {
                PlayerInteractor interactor = controller.Player.GetComponent<PlayerInteractor>();
                if (interactor != null && interactor.HasTarget)
                    GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.62f, 360f, 45f), interactor.CurrentPrompt, centeredStyle);
            }

            if (!controller.HasEnded)
                return;

            GUI.Box(new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - 70f, 520f, 140f), string.Empty);
            GUI.Label(new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 45f, 480f, 90f),
                $"{controller.ResultMessage}\nR 키로 한 판 다시 시작",
                centeredStyle);
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
            centeredStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24
            };
        }
    }
}
