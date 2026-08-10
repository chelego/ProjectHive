using ProjectHive.Interaction;
using ProjectHive.Combat;
using UnityEngine;

namespace ProjectHive.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionHud : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private FirearmWeapon firearm;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color promptColor = new Color(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private Color ammoPanelColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color ammoTextColor = new Color(0.92f, 0.96f, 1f, 1f);
        [SerializeField] private Color liveRoundColor = new Color(0.25f, 0.9f, 0.35f, 0.95f);
        [SerializeField] private Color spentCaseColor = new Color(0.85f, 0.55f, 0.16f, 0.95f);
        [SerializeField] private Color emptyChamberColor = new Color(0.18f, 0.2f, 0.23f, 0.95f);
        [SerializeField] private Color activeChamberColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        private GUIStyle centeredStyle;
        private GUIStyle ammoNameStyle;
        private GUIStyle ammoCountStyle;
        private GUIStyle chamberStyle;

        private void Awake()
        {
            if (interactor == null)
                interactor = GetComponent<PlayerInteractor>();

            if (firearm == null)
                firearm = GetComponentInChildren<FirearmWeapon>();
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

            DrawFirearmHud();
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

            ammoNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            ammoCountStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };

            chamberStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawFirearmHud()
        {
            if (firearm == null)
                return;

            const float width = 230f;
            float height = firearm.IsRevolver ? 94f : 76f;
            float x = Screen.width - width - 24f;
            float y = Screen.height - height - 24f;
            Rect panel = new Rect(x, y, width, height);

            GUI.color = ammoPanelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            GUI.color = ammoTextColor;
            GUI.Label(new Rect(x + 12f, y + 8f, width - 24f, 20f), firearm.DisplayName, ammoNameStyle);
            GUI.Label(new Rect(x + 12f, y + 30f, width - 24f, 30f), firearm.AmmoText, ammoCountStyle);

            if (firearm.IsReloading)
            {
                GUI.Label(new Rect(x + 12f, y + 56f, width - 24f, 16f), "RELOADING", ammoNameStyle);
            }

            if (firearm.IsRevolver)
                DrawRevolverChambers(x + 12f, y + 70f);
        }

        private void DrawRevolverChambers(float x, float y)
        {
            RevolverChamberState[] chambers = firearm.GetRevolverChamberStates();
            const float size = 16f;
            const float gap = 4f;
            for (int i = 0; i < chambers.Length; i++)
            {
                Rect rect = new Rect(x + i * (size + gap), y, size, size);
                GUI.color = GetChamberColor(chambers[i]);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                if (i == firearm.ActiveCylinderIndex)
                {
                    GUI.color = activeChamberColor;
                    GUI.DrawTexture(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, 2f), Texture2D.whiteTexture);
                }

                GUI.color = Color.black;
                GUI.Label(rect, GetChamberLabel(chambers[i]), chamberStyle);
            }
        }

        private Color GetChamberColor(RevolverChamberState state)
        {
            return state switch
            {
                RevolverChamberState.Live => liveRoundColor,
                RevolverChamberState.Spent => spentCaseColor,
                _ => emptyChamberColor
            };
        }

        private static string GetChamberLabel(RevolverChamberState state)
        {
            return state switch
            {
                RevolverChamberState.Live => "L",
                RevolverChamberState.Spent => "S",
                _ => "E"
            };
        }
    }
}
