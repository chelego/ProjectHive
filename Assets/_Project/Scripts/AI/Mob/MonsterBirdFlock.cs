using System.Collections.Generic;
using ProjectHive.Core.Events;
using ProjectHive.Player;
using UnityEngine;

namespace ProjectHive.AI.Mob
{
    public sealed class MonsterBirdFlock : MonoBehaviour
    {
        public enum FlockState { Feeding, Alarm, Escaping, Away }
        [SerializeField] private float sprintAlarmDistance = 12f;
        [SerializeField] private float passThroughDistance = 3.2f;
        private sealed class Bird
        {
            public Transform Root;
            public Animation Animation;
            public Vector3 FlightDirection;
            public float Delay, Speed;
        }
        private readonly List<Bird> birds = new List<Bird>(6);
        private FirstPersonMotor player;
        private AudioSource voice;
        private GameEventBus bus;
        private Vector3 flockCenter;
        private float nextCheck, alertedAt;
        public FlockState State { get; private set; }
        public int BirdCount => birds.Count;

        public void Configure(FirstPersonMotor target, GameObject birdPrefab, AudioClip call, int count)
        {
            player = target;
            bus = GameEventBus.Instance;
            flockCenter = transform.position;
            voice = gameObject.AddComponent<AudioSource>();
            voice.clip = call;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 4f; voice.maxDistance = 85f;
            voice.volume = 0.8f; voice.playOnAwake = false;
            nextCheck = Time.time + Random.Range(0f, 0.2f);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 2.2f;
                Vector3 candidate = flockCenter + new Vector3(offset.x, 1.5f, offset.y);
                if (!Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 3.5f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.85f) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.3f, 0.18f, ~0, QueryTriggerInteraction.Ignore)) continue;
                GameObject instance = Instantiate(birdPrefab, hit.point + Vector3.up * 0.025f,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                instance.name = "MonsterBird_" + (i + 1).ToString("00");
                instance.transform.localScale *= Random.Range(0.92f, 1.08f);
                Animation animation = instance.GetComponentInChildren<Animation>();
                if (animation != null && animation["Feed"] != null)
                { animation.Play("Feed"); animation["Feed"].time = Random.Range(0f, 2.4f); }
                birds.Add(new Bird { Root = instance.transform, Animation = animation,
                    Delay = Random.Range(0f, 0.55f), Speed = Random.Range(8f, 11f) });
            }
        }

        private void Update()
        {
            if (player == null) return;
            if (State == FlockState.Feeding)
            {
                if (Time.time < nextCheck) return;
                nextCheck = Time.time + 0.2f;
                float d = Vector3.Distance(player.transform.position, flockCenter);
                if (d <= passThroughDistance || player.IsSprinting && d <= sprintAlarmDistance)
                {
                    Vector3 eye = flockCenter + Vector3.up * 0.65f;
                    Vector3 target = player.transform.position + Vector3.up;
                    bool blocked = Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                        hit.transform != player.transform && !hit.transform.IsChildOf(player.transform);
                    if (!blocked) Alert(player.transform.position);
                }
                return;
            }
            if (State == FlockState.Away) return;
            float elapsed = Time.time - alertedAt;
            State = elapsed < 0.6f ? FlockState.Alarm : FlockState.Escaping;
            foreach (Bird bird in birds)
            {
                float flightTime = elapsed - bird.Delay;
                if (flightTime <= 0f || bird.Root == null || !bird.Root.gameObject.activeSelf) continue;
                if (bird.Animation != null && !bird.Animation.IsPlaying("Flight")) bird.Animation.CrossFade("Flight", 0.15f);
                Vector3 direction = bird.FlightDirection;
                if (Physics.SphereCast(bird.Root.position, 0.2f, direction, out _, 3f, ~0, QueryTriggerInteraction.Ignore))
                    direction = Vector3.up;
                Vector3 step = direction * (bird.Speed * Mathf.Clamp01(flightTime / 1.2f) * Time.deltaTime);
                if (!Physics.SphereCast(bird.Root.position, 0.15f, direction, out _, step.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                    bird.Root.position += step;
                bird.Root.rotation = Quaternion.Slerp(bird.Root.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 4f);
                if (elapsed > 10f) bird.Root.gameObject.SetActive(false);
            }
            if (elapsed > 10f) State = FlockState.Away;
        }

        public void Alert(Vector3 observedPlayerPosition)
        {
            if (State != FlockState.Feeding) return;
            State = FlockState.Alarm; alertedAt = Time.time;
            foreach (Bird bird in birds)
            {
                Vector3 away = Vector3.ProjectOnPlane(bird.Root.position - observedPlayerPosition, Vector3.up).normalized;
                bird.FlightDirection = (away + Vector3.up * Random.Range(0.65f, 1.1f)).normalized;
            }
            voice.pitch = Random.Range(0.88f, 1.08f); voice.Play();
            // Wildlife is environmental evidence, not monster speech (which Hive intentionally filters).
            var noise = new NoiseEvent(observedPlayerPosition, 0.95f, 90f, 6f,
                NoiseCategory.WildlifeAlarm, NoiseAffiliation.Environment, GetInstanceID(), Time.time);
            bus?.PublishNoise(in noise);
        }
    }
}
