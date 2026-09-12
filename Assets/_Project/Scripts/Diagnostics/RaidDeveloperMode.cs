using System.Collections.Generic;
using ProjectHive.AI.Hive;
using ProjectHive.AI.Mob;
using ProjectHive.Combat;
using ProjectHive.Gameplay.Raid;
using ProjectHive.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectHive.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class RaidDeveloperMode : MonoBehaviour
    {
        public bool IsActive { get; private set; }
        public int NearbyCount { get; private set; }
        private FirstPersonMotor motor;
        private Health health;
        private Camera view;
        private HiveDirector hive;
        private VerticalSliceRaidController raid;
        private Vector3 entryPosition;
        private bool previousInvulnerability, previousFog, previousPostProcessing;
        private AmbientMode ambientMode;
        private Color ambientSky, ambientEquator, ambientGround;
        private float ambientIntensity;
        private Light fill;
        private Material pathMaterial;
        private float nextDiscovery, nextSample;
        private readonly List<Track> tracks = new List<Track>();
        private readonly HashSet<int> trackedIds = new HashSet<int>();
        private readonly List<Track> nearby = new List<Track>();
        private GUIStyle label;

        private sealed class Track
        {
            public Transform Target;
            public BreckenAI Brecken;
            public SpotterAI Spotter;
            public NavMeshAgent Agent;
            public Health Health;
            public readonly List<Vector3> History = new List<Vector3>(1801);
            public readonly Vector3[] Corners = new Vector3[64];
            public LineRenderer Past, Planned;
            public float Distance;
            public string State => Brecken != null ? Brecken.DiagnosticState : Spotter != null ? Spotter.State.ToString() : "Unknown";
        }

        public void Configure(FirstPersonMotor player)
        {
            motor = player;
            health = player.GetComponent<Health>();
            view = player.GetComponentInChildren<Camera>();
            hive = FindFirstObjectByType<HiveDirector>();
            raid = FindFirstObjectByType<VerticalSliceRaidController>();
            pathMaterial = Resources.Load<Material>("HiveEncounters/DiagnosticLine");
        }

        private void Update()
        {
            if (motor == null) return;
            if (raid != null && raid.HasEnded) { SetActive(false); return; }
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
                SetActive(!IsActive);
            if (Time.time >= nextDiscovery)
            {
                nextDiscovery = Time.time + 2f;
                foreach (BreckenAI enemy in FindObjectsByType<BreckenAI>(FindObjectsSortMode.None))
                    Register(enemy.transform, enemy, null);
                foreach (SpotterAI enemy in FindObjectsByType<SpotterAI>(FindObjectsSortMode.None))
                    Register(enemy.transform, null, enemy);
            }
            if (Time.time < nextSample) return;
            nextSample = Time.time + 0.5f;
            nearby.Clear();
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                Track t = tracks[i];
                if (t.Target == null) { DestroyTrack(t); tracks.RemoveAt(i); continue; }
                if (t.History.Count == 0 || Vector3.Distance(t.History[t.History.Count - 1], t.Target.position) > 0.75f)
                {
                    // Bounded history covers a complete 30-minute raid at one sample every half second.
                    if (t.History.Count < 3601) t.History.Add(t.Target.position + Vector3.up * 0.12f);
                }
                t.Distance = Vector3.Distance(motor.transform.position, t.Target.position);
                bool visible = IsActive && t.Distance < 160f && t.Target.gameObject.activeInHierarchy;
                if (visible && (t.Health == null || !t.Health.IsDead)) nearby.Add(t);
                t.Past.enabled = t.Planned.enabled = visible;
                if (!visible) continue;
                t.Past.positionCount = t.History.Count;
                for (int j = 0; j < t.History.Count; j++) t.Past.SetPosition(j, t.History[j]);
                int count = 0;
                if (t.Agent != null && t.Agent.enabled && t.Agent.isOnNavMesh && t.Agent.hasPath)
                    count = t.Agent.path.GetCornersNonAlloc(t.Corners);
                else if (t.Spotter != null)
                { t.Corners[0] = t.Target.position; t.Corners[1] = t.Spotter.Destination; count = 2; }
                t.Planned.positionCount = count;
                for (int j = 0; j < count; j++) t.Planned.SetPosition(j, t.Corners[j] + Vector3.up * 0.2f);
            }
            nearby.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            NearbyCount = nearby.Count;
        }

        private void Register(Transform target, BreckenAI brecken, SpotterAI spotter)
        {
            if (!trackedIds.Add(target.GetInstanceID())) return;
            var track = new Track { Target = target, Brecken = brecken, Spotter = spotter,
                Agent = target.GetComponent<NavMeshAgent>(), Health = target.GetComponent<Health>() };
            track.Past = CreateLine("History_" + target.name, new Color(0.15f, 0.85f, 1f, 0.65f), 0.075f);
            track.Planned = CreateLine("Route_" + target.name, new Color(1f, 0.7f, 0.1f, 1f), 0.12f);
            tracks.Add(track);
        }

        private LineRenderer CreateLine(string objectName, Color color, float width)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = pathMaterial;
            line.startColor = line.endColor = color;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        public void SetActive(bool value)
        {
            if (IsActive == value || motor == null) return;
            if (value && (health != null && health.IsDead || raid != null && raid.HasEnded)) return;
            IsActive = value;
            if (value)
            {
                entryPosition = motor.transform.position;
                previousInvulnerability = health != null && health.IsInvulnerable;
                if (health != null) health.IsInvulnerable = true;
                ambientMode = RenderSettings.ambientMode;
                ambientSky = RenderSettings.ambientSkyColor;
                ambientEquator = RenderSettings.ambientEquatorColor;
                ambientGround = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                previousFog = RenderSettings.fog;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.white;
                RenderSettings.ambientEquatorColor = new Color(0.8f, 0.8f, 0.8f);
                RenderSettings.ambientGroundColor = new Color(0.65f, 0.65f, 0.65f);
                RenderSettings.ambientIntensity = 1.5f;
                RenderSettings.fog = false;
                if (view != null)
                {
                    var data = view.GetUniversalAdditionalCameraData();
                    previousPostProcessing = data.renderPostProcessing;
                    data.renderPostProcessing = false;
                    var go = new GameObject("DeveloperFill");
                    go.transform.SetParent(view.transform, false);
                    fill = go.AddComponent<Light>();
                    fill.type = LightType.Point; fill.range = 160f; fill.intensity = 7f;
                    fill.shadows = LightShadows.None;
                }
            }
            else
            {
                if (health != null) health.IsInvulnerable = previousInvulnerability;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.fog = previousFog;
                if (view != null) view.GetUniversalAdditionalCameraData().renderPostProcessing = previousPostProcessing;
                if (fill != null) Destroy(fill.gameObject);
                // Restore a safe entry location rather than leaving the player inside walls after noclip.
                motor.transform.position = entryPosition;
                foreach (Track track in tracks) { track.Past.enabled = false; track.Planned.enabled = false; }
            }
            motor.SetDeveloperFlight(value);
            nextSample = 0f;
        }

        private void OnGUI()
        {
            if (!IsActive || view == null) return;
            Color previousGuiColor = GUI.color;
            GUI.color = Color.white;
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12, 130, 460, 162), "");
            string report = hive != null && hive.Blackboard.HasLastReport ?
                hive.Blackboard.LastReport.Kind + " " + hive.Blackboard.LastReport.Position.ToString("F1") : "none";
            GUI.Label(new Rect(24, 138, 440, 146),
                "F5  DEVELOPER\nWASD fly · Space up · Ctrl down · Shift fast\n" +
                "Invulnerable / AI detection ON / time continues\n" +
                "Nearby (160m): " + NearbyCount + "  |  tracked: " + tracks.Count +
                "\nCyan: travelled / Amber: planned\nHive: " + report, label);
            for (int i = 0; i < Mathf.Min(18, nearby.Count); i++)
            {
                Track t = nearby[i];
                Vector3 p = view.WorldToScreenPoint(t.Target.position + Vector3.up * 2f);
                if (p.z <= 0f || p.x < 0f || p.x > Screen.width || p.y < 0f || p.y > Screen.height) continue;
                string vision = t.Brecken != null ? t.Brecken.HasVisualContact.ToString() : t.Spotter.HasVisualContact.ToString();
                string details = t.Target.name + "  " + t.Distance.ToString("F0") + "m\n" + t.State + " / LOS " + vision;
                if (t.Health != null) details += " / HP " + t.Health.CurrentHealth.ToString("F0");
                Rect box = new Rect(Mathf.Clamp(p.x, 0f, Screen.width - 315f), Screen.height - p.y, 315, 46);
                GUI.Box(box, ""); GUI.Label(box, details, label);
            }
            GUI.color = previousGuiColor;
        }

        private void OnDisable() { SetActive(false); }
        private void OnDestroy() { foreach (Track t in tracks) DestroyTrack(t); }
        private void DestroyTrack(Track t)
        {
            if (t.Past != null) Destroy(t.Past.gameObject);
            if (t.Planned != null) Destroy(t.Planned.gameObject);
        }
    }
}
