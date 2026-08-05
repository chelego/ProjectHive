using UnityEngine;
// NavMeshAgent, NavMesh
using UnityEngine.AI;
// IRuntimeTickable, RuntimeTickContext, RuntimeCoordinator
using ProjectHive.Core.Runtime;
// GameEventBus
using ProjectHive.Core.Events;
using System;
using Random = UnityEngine.Random;

namespace ProjectHive.AI.Mob
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class BreckenAI : MonoBehaviour, IRuntimeTickable
    {
        private enum EnemyState
        {
            Idle,
            Patrol,
            Alert,
            Investigate,
            Chase,
            Attack,
            Search
        }

        private NavMeshAgent agent;

        [Header("Tick")] [SerializeField, Min(0.02f)]
        private float tickInterval = 0.05f;

        [Header("Player Perception")] [SerializeField]
        private Transform playerTransform;

        [SerializeField, Min(0f)] private float sightRange = 15f;
        [SerializeField, Range(0f, 180f)] private float sightAngle = 110f;
        [SerializeField, Range(0f, 90f)] private float sightAngleUp = 30f;
        [SerializeField, Range(0f, 90f)] private float sightAngleDown = 45f;
        [SerializeField] private LayerMask sightBlockerMask;
        [SerializeField, Min(0f)] private float eyeHeight = 2f;
        [SerializeField] private bool isPlayerVisible;

        [Header("Enemy State")] [SerializeField]
        private EnemyState currentState = EnemyState.Idle;

        [Header("State - Alert")] [SerializeField, Min(0f)]
        private float alertDuration = 1f;

        [Header("State - Patrol")] [SerializeField, Min(1f)]
        private float patrolRadius = 10f;

        [Header("State - Chase")] [SerializeField, Min(0f)]
        private float attackRange = 2f; // 공격 범위, 임시값
        [SerializeField, Min(0f)] private float destinationUpdateThreshold = 0.5f; // 플레이어로 향하는 거리 계산 주기
        [SerializeField, Min(0f)] private float lostSightDuration = 10f; // 추격 시간 상한, 추적 중 목적지에 도달 못하는 경우 방지
        [SerializeField] private float lostSightTimer;

        [Header("State - Search")] 
        [SerializeField, Min(0f)] private float searchRadius = 8f;
        [SerializeField, Min(1)] private int searchPointCount = 3;
        [SerializeField, Min(0f)] private float searchDuration = 15f;
        [SerializeField, Range(0f, 180f)] private float searchSweepAngle = 90f;
        [SerializeField] private float searchTimer;
        private bool isSearchPointAssigned; // 이동 중과 목적지 할당 간 충돌 해결
        private int visitedSearchPointsCount;
        private bool isSweepDone;
        private Quaternion searchSweepTarget;
        private int searchSweepSign = 1; // 1: 시계, -1: 반시계
        

        private Vector3 lastKnownPlayerPosition;
        private Vector3 lastKnownPlayerDirection;

        private Vector3 patrolOriginPosition;

        [SerializeField] private float alertTimer;

        [SerializeField] private RuntimeCoordinator runtimeCoordinator;

        public bool RuntimeTickEnabled => isActiveAndEnabled;
        public Transform RuntimeTransform => transform;
        public float MinimumTickInterval => tickInterval;
        public bool UseDistanceScaling => true;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void RuntimeTick(in RuntimeTickContext context)
        {
            isPlayerVisible = CanSeePlayer();

            switch (currentState)
            {
                case EnemyState.Idle: TickIdle(context); break;
                case EnemyState.Patrol: TickPatrol(context); break;
                case EnemyState.Alert: TickAlert(context); break;
                case EnemyState.Investigate: TickInvestigate(context); break;
                case EnemyState.Chase: TickChase(context); break;
                case EnemyState.Attack: TickAttack(context); break;
                case EnemyState.Search: TickSearch(context); break;
            }
        }

        void OnEnable()
        {
            ResolveServices();
            patrolOriginPosition = transform.position; // 순찰 기준점, 풀에서 꺼낼 때마다 새로운 기준점
            // 브레켄이 죽은 후 풀 반환 이후
            // 풀에서 다시 꺼낼 때 초기화용
            currentState = EnemyState.Idle;
            alertTimer = 0f;


            if (runtimeCoordinator == null)
            {
                Debug.LogWarning("RuntimeCoordinator를 찾지 못해 .register 실패", this);
                return;
            }

            runtimeCoordinator.Register(this);
        }

        private void OnDisable()
        {
            if (runtimeCoordinator != null)
            {
                runtimeCoordinator.Unregister(this);
            }
        }

        private void ResolveServices()
        {
            if (runtimeCoordinator == null)
            {
                runtimeCoordinator = RuntimeCoordinator.Instance;
            }
        }

        private bool CanSeePlayer()
        {
            if (playerTransform == null) return false;

            Vector3 toPlayer = playerTransform.position - transform.position;
            Vector3 toPlayerFlat = new Vector3(toPlayer.x, 0, toPlayer.z);
            float verticalAngle = Mathf.Atan2(toPlayer.y, toPlayerFlat.magnitude) * Mathf.Rad2Deg;

            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = playerTransform.position + Vector3.up * 1.2f;
            Vector3 toTarget = targetPoint - eyePosition;

            // 거리
            if (toPlayer.sqrMagnitude > sightRange * sightRange) return false;

            // 시야각 - 좌우
            if (Vector3.Angle(transform.forward, toPlayerFlat) > sightAngle * 0.5f) return false;

            // 시야각 - 상하
            if (verticalAngle > sightAngleUp || verticalAngle < -sightAngleDown) return false;

            // 장애물 판정 - Raycast
            if (Physics.Raycast(
                    eyePosition,
                    toTarget.normalized,
                    toTarget.magnitude,
                    sightBlockerMask)) return false;

            return true;
        }

        private void ChangeState(EnemyState next)
        {
            if (currentState == next) return;
            Debug.Log($"{currentState} -> {next}", this);
            currentState = next;
            alertTimer = 0f;
            lostSightTimer = 0f;

            // Alert 상태로 전이하는 경우, 회전은 FaceTarget()이 맡음
            // 복귀하는 경우, NavMesh의 자동 회전이 맡음
            switch (next)
            {
                case EnemyState.Alert:
                    agent.updateRotation = false;
                    if (agent.isOnNavMesh) agent.ResetPath();
                    break;
                
                case EnemyState.Search:
                    isSearchPointAssigned = false;
                    agent.updateRotation = false;
                    if (agent.isOnNavMesh) agent.ResetPath();
                    searchTimer = 0f;
                    visitedSearchPointsCount = 0;
                    isSweepDone = false;
                    // 제자리 두리번거리기
                    // searchSweepSign: 플레이어가 사라진 방향, 즉 그 방향으로 먼저 고개를 꺾음
                    searchSweepTarget = Quaternion.Euler(
                        0f,
                        transform.eulerAngles.y + searchSweepAngle * searchSweepSign,
                        0f);
                    break;

                default:
                    agent.updateRotation = true;
                    break;
            }
        }

        private void TickIdle(in RuntimeTickContext context)
        {
            if (!agent.isOnNavMesh) return; // 아직 navmesh에 올라가지 못했다면 다음 틱에 시도

            // 플레이어가 스폰 바로 앞이라면 바로 alert
            if (isPlayerVisible)
            {
                ChangeState(EnemyState.Alert);
                return;
            }

            ChangeState(EnemyState.Patrol);
        }

        private void TickPatrol(in RuntimeTickContext context)
        {
            if (isPlayerVisible)
            {
                ChangeState(EnemyState.Alert);
                return;
            }

            if (agent.pathPending) return;

            // 목적지에 도달하지 못할 수 있으므로 0.1만큼 여유,
            // stoppingDistance 기본값: 0
            if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                if (TryGetPatrolPoint(out Vector3 point)) agent.SetDestination(point);
            }
        }

        private bool TryGetPatrolPoint(out Vector3 result)
        {
            // 현재 랜덤, 차후 순찰 지점 설정 필요
            Vector3 randomPoint = patrolOriginPosition + Random.insideUnitSphere * patrolRadius;

            if (NavMesh.SamplePosition(
                    randomPoint,
                    out NavMeshHit hit,
                    2f,
                    NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = patrolOriginPosition;
            return false;
        }

        private void TickAlert(in RuntimeTickContext context)
        {
            // 차후 발각 판정 차등 배치

            if (playerTransform == null || !isPlayerVisible)
            {
                ChangeState(EnemyState.Patrol);
                return;
            }

            FaceTarget(playerTransform.position, context.DeltaTime);

            alertTimer += context.DeltaTime;
            if (alertTimer > alertDuration)
            {
                ChangeState(EnemyState.Chase);
            }
        }

        private void FaceTarget(Vector3 targetPosition, float deltaTime)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            // 너무 가까운 경우 방향이 불안정 -> 그대로 return
            if (direction.sqrMagnitude < 0.01f) return;

            Quaternion look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, agent.angularSpeed * deltaTime);
        }

        private void TickInvestigate(in RuntimeTickContext context)
        {
        }

        private void TickChase(in RuntimeTickContext context)
        {
            if (playerTransform == null)
            {
                ChangeState(EnemyState.Search);
                return;
            }

            if (isPlayerVisible)
            {
                // chase중 플레이어가 다시 보이는 경우, 리셋
                lostSightTimer = 0f;

                // lastKnownPlayerPosition을 덮어씌우기 전에, 이동 방향 추출
                Vector3 playerDelta = playerTransform.position - lastKnownPlayerPosition;
                if (playerDelta.sqrMagnitude > 0.01f)
                {
                    lastKnownPlayerDirection = playerDelta.normalized;
                }

                lastKnownPlayerPosition = playerTransform.position;

                // 공격 범위 내?
                Vector3 toPlayer = playerTransform.position - transform.position;
                if (toPlayer.sqrMagnitude <= attackRange * attackRange)
                {
                    ChangeState(EnemyState.Attack);
                    return;
                }
            }
            else
            {
                // 추격 중이지만, 플레이어가 시야 밖으로 벗어난 경우(코너를 도는 등)
                lostSightTimer += context.DeltaTime;

                bool isPathReady = agent.isOnNavMesh && !agent.pathPending;

                // 마지막 목격 위치에 도달(true) or 가는 중(false)
                bool isReached = isPathReady &&
                                 agent.remainingDistance <= agent.stoppingDistance + 0.1f;

                // 경로가 끊기는 등 도달할 수 없음
                bool isPathFailed = isPathReady &&
                                    agent.pathStatus != NavMeshPathStatus.PathComplete;

                if (isReached || isPathFailed || lostSightTimer >= lostSightDuration)
                {
                    float side = Vector3.Dot(transform.right, lastKnownPlayerDirection);
                    searchSweepSign = side >= 0f ? 1 : -1; // 시계 반시계

                    ChangeState(EnemyState.Search);
                    return;
                }
            }

            if (!agent.isOnNavMesh) return;

            // 플레이어가 보이든 보이지 않든 목적지를 향함
            Vector3 toDestination = agent.destination - lastKnownPlayerPosition;
            if (toDestination.sqrMagnitude >
                destinationUpdateThreshold * destinationUpdateThreshold)
            {
                agent.SetDestination(lastKnownPlayerPosition);
            }
        }

        private void TickAttack(in RuntimeTickContext context)
        {
        }

        private void TickSearch(in RuntimeTickContext context)
        {
            // 이미 alert max -> 보이면 바로 추격
            if (isPlayerVisible)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            searchTimer += context.DeltaTime;
            // 15초 이상을 Search했거나, 지정된 모든 searchPoint들을 방문했다면, 복귀
            if (searchTimer >= searchDuration || visitedSearchPointsCount >= searchPointCount)
            {
                ChangeState(EnemyState.Patrol);
                return;
            }
            
            // 틱 1~N: 사라진 방향으로 시야 훑기
            if (!isSweepDone)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, searchSweepTarget, agent.angularSpeed * context.DeltaTime);
                
                if(Quaternion.Angle(transform.rotation, searchSweepTarget) < 1f)
                {
                    isSweepDone = true;
                    agent.updateRotation = true; // 이동 방향으로 자동 rotation
                }
                return;
            }

            
            if(!agent.isOnNavMesh || agent.pathPending) return;
            // 틱 N+1~: 주변을 훑은 이후, 주변 지점들을 하나씩 search
            // 길(목적지)이 없거나, 정상적으로 도착했거나, 길이 있지만 PathComplete가 아닌 경우 다음 지점 필요 -> true
            bool isNextPointNeeded = !agent.hasPath ||
                             agent.remainingDistance <= agent.stoppingDistance + 0.1f ||
                             agent.pathStatus != NavMeshPathStatus.PathComplete;
            
            if (isNextPointNeeded)
            {
                // 이미 다음 지점이 존재한다면, 해당 지점은 수색이 끝난 것
                // -> visitedCount 1 증가
                if (isSearchPointAssigned) visitedSearchPointsCount++;

                if (TryGetSearchPoint(out Vector3 point))
                {
                    agent.SetDestination(point);
                    isSearchPointAssigned = true;
                }
            }

        }

        private bool TryGetSearchPoint(out Vector3 result)
        {
            Vector3 randomPoint = lastKnownPlayerPosition + Random.insideUnitSphere * searchRadius;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = lastKnownPlayerPosition;
            return false;
        }

        // 시야 확인용
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, sightRange);

            Vector3 left = Quaternion.Euler(0f, -sightAngle * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, sightAngle * 0.5f, 0f) * transform.forward;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, left * sightRange);
            Gizmos.DrawRay(transform.position, right * sightRange);

            // 브레켄 눈 -> 플레이어 몸통(1.2f) 판정
            if (playerTransform == null) return;
            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = playerTransform.position + Vector3.up * 1.2f;
            Gizmos.color = isPlayerVisible ? Color.green : Color.red;
            Gizmos.DrawLine(eyePosition, targetPoint);
        }

        // 경계도 확인용
        private void OnDrawGizmos()
        {
            if (currentState != EnemyState.Alert || alertDuration <= 0f) return;

            Camera camera = Camera.current;
            if (camera == null) return;

            float fill = Mathf.Clamp01(alertTimer / alertDuration);

            Vector3 center = transform.position + Vector3.up * (eyeHeight + 0.8f);
            Vector3 halfWidth = camera.transform.right * 0.35f;
            Vector3 halfHeight = camera.transform.up * 0.5f;

            Vector3 apex = center - halfHeight; // 아래 꼭짓점 (정중앙 아래)
            Vector3 topLeft = center + halfHeight - halfWidth; // 좌상단
            Vector3 topRight = center + halfHeight + halfWidth; // 우상단

            Gizmos.color = Color.black;
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, apex);
            Gizmos.DrawLine(apex, topLeft);

            if (fill <= 0f) return;

            Gizmos.color = Color.Lerp(Color.yellow, Color.red, fill);

            const int steps = 80;
            for (int i = 1; i <= steps; i++)
            {
                float t = fill * i / steps;
                Vector3 left = apex + (topLeft - apex) * t;
                Vector3 right = apex + (topRight - apex) * t;
                Gizmos.DrawLine(left, right);
            }

        }
    }
}