using System.Collections;
using ProjectHive.AI.Mob;
using ProjectHive.Player;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectHive.Gameplay.Raid
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PrototypeEnemyActivator : MonoBehaviour
    {
        [SerializeField] private BreckenAI[] enemies;
        [SerializeField] private Transform player;
        [SerializeField, Min(1f)] private float minimumPlayerDistance = 35f;
        [SerializeField, Min(1f)] private float navMeshSearchRadius = 120f;

        public void Configure(BreckenAI[] breckens, Transform playerTransform)
        {
            enemies = breckens;
            player = playerTransform;
        }

        private IEnumerator Start()
        {
            yield return null;

            if (enemies == null)
                yield break;
            if (player == null)
            {
                FirstPersonMotor motor = FindFirstObjectByType<FirstPersonMotor>();
                player = motor != null ? motor.transform : null;
            }

            EnsureRuntimeNavMesh();

            for (int i = 0; i < enemies.Length; i++)
            {
                BreckenAI enemy = enemies[i];
                if (enemy == null)
                    continue;

                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent == null)
                    continue;

                Vector3 desiredPosition = enemy.transform.position;
                if (player != null)
                {
                    Vector3 fromPlayer = desiredPosition - player.position;
                    fromPlayer.y = 0f;
                    if (fromPlayer.sqrMagnitude < minimumPlayerDistance * minimumPlayerDistance)
                    {
                        Vector3 direction = fromPlayer.sqrMagnitude > 0.01f
                            ? fromPlayer.normalized
                            : Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward;
                        desiredPosition = player.position + direction * (minimumPlayerDistance + i * 5f);
                    }
                }

                if (!TryFindNavMeshPosition(desiredPosition, enemy.transform.position, agent.areaMask, out NavMeshHit hit))
                {
                    Debug.LogError($"[VerticalSlice] Could not place {enemy.name} on the NavMesh.", enemy);
                    continue;
                }

                enemy.transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
                enemy.enabled = true;
            }
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
            minimumPlayerDistance = Mathf.Max(1f, minimumPlayerDistance);
            navMeshSearchRadius = Mathf.Max(1f, navMeshSearchRadius);
        }
    }
}
