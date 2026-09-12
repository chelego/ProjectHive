using ProjectHive.AI.Hive;
using ProjectHive.Combat;
using ProjectHive.Core.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectHive.AI.Mob
{
    [DisallowMultipleComponent]
    public sealed class SpotterAI : MonoBehaviour, IHiveControllable
    {
        public enum FlightState { Patrol, Chase, Search, Down }
        [SerializeField] private float flightHeight = 22f;
        [SerializeField] private float followDistance = 15f;
        [SerializeField] private float speed = 8f;
        [SerializeField] private float sightRange = 72f;
        [SerializeField] private float coneAngle = 44f;
        [SerializeField] private float searchSeconds = 18f;
        private Transform player, sensor;
        private GameEventBus bus;
        private HiveUnitRegistry registry;
        private Health health;
        private Light searchlight;
        private Mesh beamMesh;
        private MeshRenderer beamRenderer;
        private const int Segments = 24;
        private readonly Vector3[] beamVertices = new Vector3[Segments * 2 + 1];
        private readonly Color[] beamColors = new Color[Segments * 2 + 1];
        private Vector3 home, lastSeen, flightTarget, followOffset;
        private float nextSense, nextReport, seenAt, acquisition, searchUntil, phase;
        private int commandSequence;
        public FlightState State { get; private set; }
        public bool HasVisualContact { get; private set; }
        public Vector3 Destination => flightTarget;
        public Vector3 LastKnownTarget => lastSeen;
        public Transform Sensor => sensor;

        public void Configure(Transform target, Vector3 patrolCenter, Material beamMaterial)
        {
            player = target; home = patrolCenter;
            lastSeen = home;
            phase = Random.Range(0f, Mathf.PI * 2f);
            nextSense = Time.time + Random.Range(0f, 0.15f);
            bus = GameEventBus.Instance;
            registry = FindFirstObjectByType<HiveUnitRegistry>();
            health = GetComponent<Health>();
            GameObject lightPrefab = Resources.Load<GameObject>("HiveEncounters/SpotterSearchlight");
            var pivot = lightPrefab != null ? Instantiate(lightPrefab, transform) : new GameObject("Searchlight_Gimbal");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = Vector3.down * 0.6f;
            sensor = pivot.transform;
            searchlight = pivot.GetComponent<Light>() ?? pivot.AddComponent<Light>();
            searchlight.type = LightType.Spot;
            searchlight.range = sightRange;
            searchlight.spotAngle = coneAngle;
            searchlight.innerSpotAngle = coneAngle * 0.65f;
            searchlight.intensity = 12f;
            searchlight.color = new Color(0.78f, 0.9f, 0.85f);
            searchlight.shadows = LightShadows.Soft;
            searchlight.shadowBias = 0.025f;
            sensor.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var beamObject = new GameObject("Searchlight_Beam");
            beamObject.transform.SetParent(sensor, false);
            beamMesh = new Mesh { name = "Spotter_OccludedBeam" };
            beamMesh.MarkDynamic();
            beamObject.AddComponent<MeshFilter>().sharedMesh = beamMesh;
            beamRenderer = beamObject.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beamMaterial;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            int[] indices = new int[Segments * 9];
            for (int i = 0; i < Segments; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % Segments, c = 1 + Segments + i, d = 1 + Segments + (i + 1) % Segments;
                int j = i * 9;
                indices[j] = 0; indices[j + 1] = b; indices[j + 2] = a;
                indices[j + 3] = a; indices[j + 4] = b; indices[j + 5] = d;
                indices[j + 6] = a; indices[j + 7] = d; indices[j + 8] = c;
            }
            beamMesh.vertices = beamVertices;
            beamMesh.triangles = indices;
            registry?.Register(this);
        }

        private void Update()
        {
            if (player == null || sensor == null) return;
            if (health != null && health.IsDead)
            {
                State = FlightState.Down; searchlight.enabled = beamRenderer.enabled = false;
                registry?.Unregister(this); enabled = false; return;
            }
            float t = Time.time;
            float observerDistanceSquared = (player.position - transform.position).sqrMagnitude;
            searchlight.enabled = observerDistanceSquared < 140f * 140f;
            beamRenderer.enabled = observerDistanceSquared < 180f * 180f;
            if (t >= nextSense)
            {
                float delta = 0.15f;
                nextSense = t + delta;
                HasVisualContact = CanSee(player.position + Vector3.up * 1.25f);
                acquisition = HasVisualContact ? acquisition + delta : 0f;
                if (HasVisualContact && (acquisition >= 0.3f || State == FlightState.Chase))
                {
                    if (State != FlightState.Chase)
                    {
                        followOffset = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up).normalized;
                        if (followOffset.sqrMagnitude < 0.1f) followOffset = Vector3.back;
                    }
                    State = FlightState.Chase;
                    lastSeen = player.position; seenAt = t;
                    if (t >= nextReport)
                    {
                        Report(EnemyReportKind.SpotterContact, lastSeen, 1f, 0f);
                        nextReport = t + 1.5f;
                    }
                }
                if (State == FlightState.Chase && !HasVisualContact && t - seenAt > 1f)
                {
                    State = FlightState.Search; searchUntil = t + searchSeconds;
                    Report(EnemyReportKind.LostTarget, lastSeen, 0.6f, 8f);
                }
                if (State == FlightState.Search && t > searchUntil)
                { State = FlightState.Patrol; commandSequence = 0; }
                UpdateBeam();
            }

            Vector3 focus;
            if (State == FlightState.Chase)
            {
                // The target position is refreshed only by verified sightings, never through walls.
                focus = lastSeen + Vector3.up * 1.2f;
                flightTarget = lastSeen + followOffset * followDistance + Vector3.up * flightHeight;
            }
            else
            {
                Vector3 center = State == FlightState.Search ? lastSeen : home;
                float radius = State == FlightState.Search ? 13f : 30f;
                float angle = t * (State == FlightState.Search ? 0.22f : 0.085f) + phase;
                Vector3 orbit = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                flightTarget = center + orbit * radius + Vector3.up * flightHeight;
                focus = center + new Vector3(Mathf.Sin(t * 0.65f + phase), 0f, Mathf.Cos(t * 0.47f + phase)) * 10f;
            }
            Vector3 movement = flightTarget - transform.position;
            float step = Mathf.Min(movement.magnitude, speed * Time.deltaTime);
            if (movement.sqrMagnitude > 0.001f)
            {
                Vector3 direction = movement.normalized;
                if (Physics.SphereCast(transform.position, 0.65f, direction, out RaycastHit obstruction,
                    step + 3f, ~0, QueryTriggerInteraction.Ignore) && !obstruction.transform.IsChildOf(transform))
                {
                    direction = (Vector3.up + Vector3.ProjectOnPlane(direction, obstruction.normal)).normalized;
                    if (Physics.SphereCast(transform.position, 0.65f, direction, out _, step + 0.3f, ~0, QueryTriggerInteraction.Ignore)) step = 0f;
                }
                transform.position += direction * step;
                Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
                if (flat.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 2f);
            }
            Vector3 aim = focus - sensor.position;
            // The cube stays upright; only the independent gimbal aims downwards.
            aim.y = Mathf.Min(aim.y, -Mathf.Max(3f, new Vector2(aim.x, aim.z).magnitude * 0.55f));
            sensor.rotation = Quaternion.Slerp(sensor.rotation, Quaternion.LookRotation(aim), Time.deltaTime * 3f);
        }

        public bool CanSee(Vector3 point)
        {
            if (sensor == null) return false;
            Vector3 delta = point - sensor.position;
            if (delta.sqrMagnitude > sightRange * sightRange || Vector3.Angle(sensor.forward, delta) > coneAngle * 0.5f) return false;
            if (!Physics.Raycast(sensor.position, delta.normalized, out RaycastHit hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)) return true;
            return player != null && (hit.transform == player || hit.transform.IsChildOf(player));
        }

        private void Report(EnemyReportKind kind, Vector3 position, float confidence, float uncertainty)
        {
            var report = new EnemyReport(kind, position, confidence, uncertainty, EnemyReportSource.Spotter, GetInstanceID(), Time.time);
            bus?.PublishEnemyReport(in report);
        }

        private void UpdateBeam()
        {
            beamVertices[0] = Vector3.zero;
            beamColors[0] = new Color(0.6f, 0.9f, 0.75f, 0.12f);
            float spread = Mathf.Tan(coneAngle * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 local = new Vector3(Mathf.Cos(a) * spread, Mathf.Sin(a) * spread, 1f).normalized;
                Vector3 world = sensor.TransformDirection(local);
                float length = Physics.Raycast(sensor.position, world, out RaycastHit hit, sightRange, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : sightRange;
                beamVertices[1 + i] = local * length * 0.55f;
                beamVertices[1 + Segments + i] = local * length;
                beamColors[1 + i] = new Color(0.6f, 0.9f, 0.75f, 0.07f);
                beamColors[1 + Segments + i] = new Color(0.6f, 0.9f, 0.75f, 0f);
            }
            beamMesh.vertices = beamVertices;
            beamMesh.colors = beamColors;
            beamMesh.RecalculateBounds();
        }

        public HiveUnitSnapshot GetHiveUnitSnapshot() => new HiveUnitSnapshot(GetInstanceID(), transform.position,
            HiveUnitRole.Scout, HiveUnitCapabilities.Flight | HiveUnitCapabilities.Investigate | HiveUnitCapabilities.ReportTarget,
            speed, isActiveAndEnabled && State != FlightState.Chase && State != FlightState.Down, commandSequence);

        public bool TryAcceptHiveCommand(in HiveCommand command)
        {
            if (!isActiveAndEnabled || State == FlightState.Chase || command.IsExpired(Time.time) || command.Sequence <= commandSequence) return false;
            if (command.Kind == HiveCommandKind.None) return false;
            lastSeen = command.TargetPosition; commandSequence = command.Sequence;
            State = FlightState.Search; searchUntil = Mathf.Min(command.ExpiresAt, Time.time + searchSeconds);
            return true;
        }

        private void OnEnable() { registry?.Register(this); }
        private void OnDisable() { registry?.Unregister(this); }
        private void OnDestroy() { if (beamMesh != null) Destroy(beamMesh); }
    }
}
