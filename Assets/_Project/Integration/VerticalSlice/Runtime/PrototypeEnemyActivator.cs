using System.Collections;
using System.Collections.Generic;
using ProjectHive.AI.Mob;
using ProjectHive.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace ProjectHive.Gameplay.Raid
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PrototypeEnemyActivator : MonoBehaviour
    {
        [SerializeField] private BreckenAI[] enemies;
        [SerializeField] private Transform player;
        [SerializeField, Min(1)] private int targetEnemyCount = 10;
        [SerializeField, Min(1f)] private float innerSpawnRadius = 18f;
        [FormerlySerializedAs("minimumPlayerDistance")]
        [SerializeField, Min(1f)] private float outerSpawnRadius = 35f;
        [SerializeField, Min(0.5f)] private float navMeshSampleRadius = 10f;
        [SerializeField, Min(0.5f)] private float minimumEnemySeparation = 3f;
        [SerializeField] private PrototypeSpawnDistrict[] districts = new PrototypeSpawnDistrict[0];
        [SerializeField, Min(1)] private int activationsPerFrame = 5;

        private readonly Dictionary<string, int> districtSpawnCounts = new Dictionary<string, int>();
        public bool SpawnComplete { get; private set; }
        public int ActivatedEnemyCount { get; private set; }
        public int RequestedEnemyCount
        {
            get
            {
                int count = 0;
                if (districts != null)
                    foreach (PrototypeSpawnDistrict district in districts)
                        if (district != null) count += district.Population;
                return count > 0 ? count : targetEnemyCount;
            }
        }
        public IReadOnlyDictionary<string, int> DistrictSpawnCounts => districtSpawnCounts;
        public IReadOnlyList<PrototypeSpawnDistrict> Districts => districts;

        public void Configure(BreckenAI[] breckens, Transform playerTransform, PrototypeSpawnDistrict[] spawnDistricts = null)
        {
            enemies = breckens;
            player = playerTransform;
            districts = spawnDistricts ?? new PrototypeSpawnDistrict[0];
        }

        private IEnumerator Start()
        {
            yield return null;

            if (player == null)
            {
                FirstPersonMotor motor = FindFirstObjectByType<FirstPersonMotor>();
                player = motor != null ? motor.transform : null;
            }

            if (player == null)
            {
                Debug.LogError("[VerticalSlice] 브레켄 배치에 사용할 플레이어를 찾지 못했습니다.", this);
                yield break;
            }

            if (districts != null && districts.Length > 0)
            {
                yield return SpawnAcrossDistricts();
                yield break;
            }

            List<BreckenAI> runtimeEnemies = CreateRuntimeEnemySet();
            if (runtimeEnemies.Count == 0)
            {
                Debug.LogError("[VerticalSlice] 복제할 브레켄 템플릿이 없습니다.", this);
                yield break;
            }

            NavMeshAgent templateAgent = runtimeEnemies[0].GetComponent<NavMeshAgent>();
            int centerAreaMask = templateAgent != null ? templateAgent.areaMask : NavMesh.AllAreas;
            Vector3 spawnCenter = player.position;
            if (NavMesh.SamplePosition(player.position, out NavMeshHit centerHit, 20f, centerAreaMask))
                spawnCenter = centerHit.position;

            List<Vector3> occupiedPositions = new List<Vector3>(runtimeEnemies.Count);
            int activatedCount = 0;

            for (int i = 0; i < runtimeEnemies.Count; i++)
            {
                BreckenAI enemy = runtimeEnemies[i];
                if (enemy == null)
                    continue;

                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent == null)
                    continue;

                enemy.enabled = false;
                agent.enabled = false;

                if (!TryFindSpawnPosition(i, runtimeEnemies.Count, spawnCenter, agent.areaMask, occupiedPositions,
                        out Vector3 spawnPosition))
                {
                    Debug.LogError($"[VerticalSlice] {enemy.name}의 근거리 NavMesh 배치 지점을 찾지 못했습니다.", enemy);
                    continue;
                }

                Vector3 outward = spawnPosition - spawnCenter;
                outward.y = 0f;
                Quaternion rotation = outward.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(outward.normalized, Vector3.up)
                    : enemy.transform.rotation;

                enemy.transform.SetPositionAndRotation(spawnPosition, rotation);
                agent.enabled = true;
                agent.Warp(spawnPosition);
                enemy.enabled = true;
                occupiedPositions.Add(spawnPosition);
                activatedCount++;
            }

            Debug.Log(
                $"[VerticalSlice] 브레켄 {activatedCount}마리를 플레이어 시작 위치에서 " +
                $"{innerSpawnRadius:0.#}~{outerSpawnRadius:0.#}m 범위에 배치했습니다.",
                this);
            ActivatedEnemyCount = activatedCount;
            SpawnComplete = true;
        }

        private IEnumerator SpawnAcrossDistricts()
        {
            targetEnemyCount = RequestedEnemyCount;
            List<BreckenAI> runtimeEnemies = CreateRuntimeEnemySet();
            if (runtimeEnemies.Count == 0)
            {
                Debug.LogError("[DistrictSpawn] No Brecken template is configured.", this);
                yield break;
            }

            var occupied = new List<Vector3>(targetEnemyCount);
            int enemyIndex = 0;
            foreach (PrototypeSpawnDistrict district in districts)
            {
                if (district == null) continue;
                int[] order = new int[district.CandidateCount];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                int placed = 0;
                int cursor = 0;
                while (placed < district.Population && enemyIndex < runtimeEnemies.Count)
                {
                    BreckenAI enemy = runtimeEnemies[enemyIndex];
                    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                    enemy.enabled = false;
                    agent.enabled = false;
                    bool found = false;
                    Vector3 position = default;
                    var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                    while (cursor < order.Length)
                    {
                        Vector3 candidate = district.CandidateAt(order[cursor++]);
                        Vector3 delta = candidate - player.position;
                        delta.y = 0f;
                        if (delta.sqrMagnitude < innerSpawnRadius * innerSpawnRadius || !HasMinimumSeparation(candidate, occupied)) continue;
                        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, filter)) continue;
                        float radius = Mathf.Max(0.35f, agent.radius);
                        Vector3 bottom = hit.position + Vector3.up * (radius + 0.15f);
                        Vector3 top = hit.position + Vector3.up * Mathf.Max(radius + 0.15f, agent.height - radius);
                        if (Physics.CheckCapsule(bottom, top, radius * 0.9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        position = hit.position;
                        found = true;
                        break;
                    }
                    if (!found)
                    {
                        Debug.LogError($"[DistrictSpawn] {district.DistrictName}: {placed}/{district.Population}; no safe candidate remains.", district);
                        break;
                    }
                    enemy.name = $"Brecken_{district.DistrictName}_{placed + 1:00}";
                    enemy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    agent.enabled = true;
                    if (!agent.isOnNavMesh || !agent.Warp(position))
                    {
                        agent.enabled = false;
                        Debug.LogError($"[DistrictSpawn] Failed to place {enemy.name} on NavMesh.", enemy);
                        break;
                    }
                    enemy.enabled = true;
                    occupied.Add(position);
                    placed++;
                    enemyIndex++;
                    ActivatedEnemyCount++;
                    if (ActivatedEnemyCount % Mathf.Max(1, activationsPerFrame) == 0) yield return null;
                }
                districtSpawnCounts[district.DistrictName] = placed;
                Debug.Log($"[DistrictSpawn] {district.DistrictName}: {placed}/{district.Population}", district);
            }
            SpawnComplete = true;
            Debug.Log($"[DistrictSpawn] Spawn complete: {ActivatedEnemyCount}/{RequestedEnemyCount}, districts={districtSpawnCounts.Count}", this);
        }

        private List<BreckenAI> CreateRuntimeEnemySet()
        {
            List<BreckenAI> result = new List<BreckenAI>(targetEnemyCount);
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    BreckenAI enemy = enemies[i];
                    if (enemy != null && !result.Contains(enemy))
                        result.Add(enemy);
                }
            }

            if (result.Count == 0)
                return result;

            BreckenAI template = result[0];
            while (result.Count < targetEnemyCount)
            {
                GameObject cloneObject = Instantiate(template.gameObject, template.transform.parent);
                cloneObject.name = $"PrototypeBrecken_{result.Count + 1:00}";

                BreckenAI clone = cloneObject.GetComponent<BreckenAI>();
                NavMeshAgent cloneAgent = cloneObject.GetComponent<NavMeshAgent>();
                if (clone == null || cloneAgent == null)
                {
                    Destroy(cloneObject);
                    break;
                }

                clone.enabled = false;
                cloneAgent.enabled = false;
                result.Add(clone);
            }

            enemies = result.ToArray();
            return result;
        }

        private bool TryFindSpawnPosition(
            int enemyIndex,
            int enemyCount,
            Vector3 center,
            int areaMask,
            List<Vector3> occupiedPositions,
            out Vector3 result)
        {
            float baseAngle = 360f * enemyIndex / Mathf.Max(1, enemyCount);
            float baseRadius = enemyIndex % 2 == 0 ? innerSpawnRadius : outerSpawnRadius;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = baseAngle + attempt * 23f;
                float radiusOffset = (attempt % 3 - 1) * 2f;
                float radius = Mathf.Max(1f, baseRadius + radiusOffset);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidate = center + direction * radius;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, areaMask))
                    continue;

                Vector3 playerDelta = hit.position - player.position;
                playerDelta.y = 0f;
                float playerDistanceSqr = playerDelta.sqrMagnitude;
                if (playerDistanceSqr < innerSpawnRadius * innerSpawnRadius ||
                    playerDistanceSqr > outerSpawnRadius * outerSpawnRadius)
                    continue;

                if (!HasMinimumSeparation(hit.position, occupiedPositions))
                    continue;

                result = hit.position;
                return true;
            }

            result = default;
            return false;
        }

        private bool HasMinimumSeparation(Vector3 candidate, List<Vector3> occupiedPositions)
        {
            float minimumSeparationSqr = minimumEnemySeparation * minimumEnemySeparation;
            for (int i = 0; i < occupiedPositions.Count; i++)
            {
                Vector3 difference = candidate - occupiedPositions[i];
                difference.y = 0f;
                if (difference.sqrMagnitude < minimumSeparationSqr)
                    return false;
            }

            return true;
        }

        private void OnValidate()
        {
            targetEnemyCount = Mathf.Max(1, targetEnemyCount);
            innerSpawnRadius = Mathf.Max(1f, innerSpawnRadius);
            outerSpawnRadius = Mathf.Max(innerSpawnRadius, outerSpawnRadius);
            navMeshSampleRadius = Mathf.Max(0.5f, navMeshSampleRadius);
            minimumEnemySeparation = Mathf.Max(0.5f, minimumEnemySeparation);
            activationsPerFrame = Mathf.Max(1, activationsPerFrame);
        }
    }
}
