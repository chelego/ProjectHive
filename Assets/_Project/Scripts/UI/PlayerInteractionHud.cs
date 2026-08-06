using ProjectHive.Interaction;
using UnityEngine;

namespace ProjectHive.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color promptColor = new Color(0.9f, 0.95f, 1f, 1f);

        private GUIStyle centeredStyle;

        private void Awake()
        {
            if (interactor == null)
                interactor = GetComponent<PlayerInteractor>();
        }

        private void OnGUI()
        {
            EnsureStyle();

            Color previousColor = GUI.color;
            GUI.color = crosshairColor;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            GUI.Label(new Rect(centerX - 8f, centerY - 10f, 16f, 20f), "+", centeredStyle);

            if (interactor != null && interactor.HasTarget)
            {
                GUI.color = promptColor;
                GUI.Label(new Rect(centerX - 120f, centerY + 28f, 240f, 28f), interactor.CurrentPrompt, centeredStyle);
            }

            GUI.color = previousColor;
        }

        private void EnsureStyle()
        {
            if (centeredStyle != null)
                return;

            centeredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
