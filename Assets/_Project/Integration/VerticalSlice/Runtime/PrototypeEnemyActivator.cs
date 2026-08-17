using System.Collections;
using ProjectHive.AI.Mob;
using ProjectHive.Player;
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

                if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 40f, agent.areaMask))
                {
                    Debug.LogError($"[VerticalSlice] {enemy.name} 주변에서 NavMesh를 찾지 못했습니다.", enemy);
                    continue;
                }

                enemy.transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
                enemy.enabled = true;
            }
        }

        private void OnValidate()
        {
            minimumPlayerDistance = Mathf.Max(1f, minimumPlayerDistance);
        }
    }
}
