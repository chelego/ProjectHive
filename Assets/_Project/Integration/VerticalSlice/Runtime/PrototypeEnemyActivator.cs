using System.Collections;
using System.Collections.Generic;
using ProjectHive.AI.Mob;
using ProjectHive.Player;
using Unity.AI.Navigation;
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
        [SerializeField, Min(1f)] private float navMeshSearchRadius = 120f;
        [SerializeField, Min(0.5f)] private float minimumEnemySeparation = 3f;

        public void Configure(BreckenAI[] breckens, Transform playerTransform)
        {
            enemies = breckens;
            player = playerTransform;
        }

        private IEnumerator Start()
        {
            yield return null;

            if (player == null)
            {
                FirstPersonMotor motor = FindFirstObjectByType<FirstPersonMotor>();
                player = motor != null ? motor.transform : null;
            }

            EnsureRuntimeNavMesh();

            if (player == null)
            {
                Debug.LogError("[VerticalSlice] 브레켄 배치에 사용할 플레이어를 찾지 못했습니다.", this);
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

                if (!TryFindSpawnPosition(i, runtimeEnemies.Count, spawnCenter, enemy.transform.position,
                        agent.areaMask, occupiedPositions, out Vector3 spawnPosition))
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
            Vector3 originalPosition,
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

            if (TryFindNavMeshPosition(originalPosition, originalPosition, areaMask, out NavMeshHit fallbackHit) &&
                IsValidSpawnPosition(fallbackHit.position, occupiedPositions))
            {
                result = fallbackHit.position;
                return true;
            }

            result = default;
            return false;
        }

        private bool IsValidSpawnPosition(Vector3 candidate, List<Vector3> occupiedPositions)
        {
            Vector3 playerDelta = candidate - player.position;
            playerDelta.y = 0f;
            float playerDistanceSqr = playerDelta.sqrMagnitude;
            if (playerDistanceSqr < innerSpawnRadius * innerSpawnRadius ||
                playerDistanceSqr > outerSpawnRadius * outerSpawnRadius)
            {
                return false;
            }

            return HasMinimumSeparation(candidate, occupiedPositions);
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

        private static void EnsureRuntimeNavMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices != null && triangulation.vertices.Length > 0)
                return;

            NavMeshSurface[] surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface == null)
                    continue;

                surface.BuildNavMesh();
            }
        }

        private bool TryFindNavMeshPosition(Vector3 desiredPosition, Vector3 originalPosition, int areaMask, out NavMeshHit hit)
        {
            if (NavMesh.SamplePosition(desiredPosition, out hit, navMeshSearchRadius, areaMask))
                return true;

            if (NavMesh.SamplePosition(originalPosition, out hit, navMeshSearchRadius, areaMask))
                return true;

            if (player != null && NavMesh.SamplePosition(player.position, out hit, navMeshSearchRadius, areaMask))
                return true;

            return NavMesh.SamplePosition(Vector3.zero, out hit, navMeshSearchRadius * 2f, areaMask);
        }

        private void OnValidate()
        {
            targetEnemyCount = Mathf.Max(1, targetEnemyCount);
            innerSpawnRadius = Mathf.Max(1f, innerSpawnRadius);
            outerSpawnRadius = Mathf.Max(innerSpawnRadius, outerSpawnRadius);
            navMeshSampleRadius = Mathf.Max(0.5f, navMeshSampleRadius);
            navMeshSearchRadius = Mathf.Max(1f, navMeshSearchRadius);
            minimumEnemySeparation = Mathf.Max(0.5f, minimumEnemySeparation);
        }
    }
}
