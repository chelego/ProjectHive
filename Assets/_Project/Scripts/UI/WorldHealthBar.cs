using ProjectHive.Combat;
using UnityEngine;

namespace ProjectHive.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private Vector2 size = new Vector2(86f, 10f);
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color fillColor = new Color(0.18f, 0.85f, 0.32f, 0.95f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.18f, 0.12f, 0.95f);

        private Health health;
        private GUIStyle labelStyle;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnGUI()
        {
            if (health == null || health.IsDead || Camera.main == null)
                return;

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + worldOffset);
            if (screen.z <= 0f)
                return;

            float x = screen.x - size.x * 0.5f;
            float y = Screen.height - screen.y - size.y * 0.5f;
            Rect background = new Rect(x, y, size.x, size.y);
            Rect fill = new Rect(x + 1f, y + 1f, (size.x - 2f) * health.Normalized, size.y - 2f);

            Color previous = GUI.color;
            GUI.color = backgroundColor;
            GUI.DrawTexture(background, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(lowHealthColor, fillColor, health.Normalized);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);

            EnsureLabelStyle();
            GUI.color = Color.white;
            GUI.Label(
                new Rect(x - 10f, y - 16f, size.x + 20f, 16f),
                $"{Mathf.CeilToInt(health.CurrentHealth)}/{Mathf.CeilToInt(health.MaxHealth)}",
                labelStyle);
            GUI.color = previous;
        }

        private void EnsureLabelStyle()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
