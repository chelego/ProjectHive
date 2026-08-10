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

            [SerializeField] private MobAISettings settings;
            [SerializeField] private Transform headTransform;
            
            private NavMeshAgent agent;
            // 가장 짧은 감지 주기를 minimumInterval로 사용
            public float MinimumTickInterval => settings.PerceptionTickInterval;


            [Header("Player Perception")] 
            [SerializeField] private Transform playerTransform;
            [SerializeField] private bool isPlayerVisible;

            [Header("Enemy State")] [SerializeField]
            private EnemyState currentState = EnemyState.Idle;

            [Header("State - Patrol")] 
            [SerializeField] private bool isPatrolWaiting;
            [SerializeField] private float patrolWaitingTimer;
            private float patrolSweepBaseYaw; //도착 지점의 몸통 방향
            private float patrolSweepOffset; // 기준점으로부터 머리가 틀어진 각도, 기준은 도착한 순간 몸통의 정면
            private int patrolSweepPhase; // 0: 한쪽끝, 1: 반대쪽 끝
            private int patrolSweepSign = -1; 
            


            [Header("State - Chase")] [SerializeField, Min(0f)]
            private float attackRange = 2f; // 공격 범위, 임시값
            [SerializeField] private float stuckTimer;

            [Header("State - Search")] 
            [SerializeField, Min(0f)] private float searchRadius = 8f;
            [SerializeField, Min(1)] private int searchPointCount = 3;
            [SerializeField, Min(0f)] private float searchDuration = 15f;
            [SerializeField, Range(0f, 180f)] private float searchSweepAngle = 90f;
            [SerializeField] private float searchTimer;
            private int visitedSearchPointsCount;
            private bool isSweepDone;
            private Quaternion searchSweepTarget;
            private int searchSweepSign = 1; // 1: 시계, -1: 반시계
            

            private Vector3 lastKnownPlayerPosition;
            private Vector3 lastKnownPlayerDirection;
            private Vector3 lastRequestedDestination;

            private Vector3 patrolSpawnPosition;

            [SerializeField] private float alertness;

            [SerializeField] private RuntimeCoordinator runtimeCoordinator;

            public bool RuntimeTickEnabled => isActiveAndEnabled;
            public Transform RuntimeTransform => transform;
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
                if (settings == null || headTransform == null)
                {
                    Debug.LogError("settings or headTransform이 연결되지 않음", this);
                    enabled = false;
                    return;
                }
                
                // patrol reset
                isPatrolWaiting = false;
                patrolWaitingTimer = 0f;
                patrolSweepBaseYaw = 0f;
                patrolSweepOffset = 0f;
                patrolSweepPhase = 0;
                patrolSweepSign = -1;
                
                //chase reset
                stuckTimer = 0f;
                
                ResolveServices();
                patrolSpawnPosition = transform.position; // 순찰 기준점, 풀에서 꺼낼 때마다 새로운 기준점
                // 브레켄이 죽은 후 풀 반환 이후
                // 풀에서 다시 꺼낼 때 초기화용
                currentState = EnemyState.Idle;
                alertness = 0f;

                searchTimer = 0f;
                visitedSearchPointsCount = 0;
                isSweepDone = false;
                lastKnownPlayerPosition = Vector3.zero;
                lastKnownPlayerDirection = Vector3.zero;
                // 플레이어가 우연히 월드의 원점에 서있는 경우 방지
                lastRequestedDestination = Vector3.positiveInfinity;
                
                if (agent != null) agent.updateRotation = true; // Alert/Search에서 꺼둔 회전을 되돌린다
                headTransform.localRotation = Quaternion.identity; // Chase에서 바뀐 머리를 정면으로 초기화


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
                if (playerTransform == null || headTransform == null) return false;

                Vector3 eyePosition = headTransform.position;
                // 플레이어의 몸통 위치
                Vector3 targetPoint = playerTransform.position + Vector3.up * 0.9f;
                Vector3 toTarget = targetPoint - eyePosition;
                Vector3 toTargetFlat = new Vector3(toTarget.x, 0, toTarget.z);
                
                
                // 거리 - 발밑 기준으로
                Vector3 toPlayer = playerTransform.position - transform.position;
                if (toPlayer.sqrMagnitude > settings.SightDistance * settings.SightDistance) return false;
                
                // 시야각 - 좌우
                Vector3 forwardFlat = new Vector3(transform.forward.x, 0, transform.forward.z);
                if (Vector3.Angle(forwardFlat, toTargetFlat) > settings.BothSideSightsAngle * 0.5f) return false;
                
                // 시야각 - 상하, 눈 기준
                float verticalAngle = Mathf.Atan2(toTarget.y, toTargetFlat.magnitude) * Mathf.Rad2Deg;
                if (verticalAngle > settings.UpSightAngle || verticalAngle < -settings.DownSightAngle) return false;
                
                // Raycast로 장애물 판정
                if (Physics.Raycast(
                        eyePosition,
                        toTarget.normalized,
                        toTarget.magnitude,
                        settings.SightBlockMask)) return false;

                return true;

            }

            private void ChangeState(EnemyState next)
            {
                if (currentState == next) return;
                Debug.Log($"{currentState} -> {next}", this);
                currentState = next;

                // Alert 상태로 전이하는 경우, 회전은 FaceTarget()이 맡음
                // 복귀하는 경우, NavMesh의 자동 회전이 맡음
                switch (next)
                {
                    case EnemyState.Patrol:
                        agent.updateRotation = true;
                        agent.speed = settings.PatrolSpeed;
                        isPatrolWaiting = false;
                        patrolWaitingTimer = 0f;
                        patrolSweepOffset = 0f;
                        patrolSweepPhase = 0;
                        headTransform.localRotation = Quaternion.identity;
                        break;
                    
                    case EnemyState.Alert:
                        agent.updateRotation = false;
                        if (agent.isOnNavMesh) agent.ResetPath();
                        break;
                    
                    case EnemyState.Chase:
                        stuckTimer = 0f;
                        agent.updateRotation = false;
                        agent.speed = settings.ChaseSpeed;
                        break;
                    
                    case EnemyState.Search:
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
                if (alertness > 0f)
                {
                    // Search 상태에서 patrol로 복귀한 경우, 서서히 깎는다
                    alertness = Mathf.Max(0f, alertness - settings.AlertDecreaseSpeed * context.DeltaTime);
                }
                
                if (isPlayerVisible)
                {
                    ChangeState(EnemyState.Alert);
                    return;
                }

                if (!agent.isOnNavMesh) return;

                if (isPatrolWaiting)
                {
                    TickPatrolWait(context.DeltaTime);
                    return;
                }

                if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .1f)
                {
                    BeginPatrolWait();
                }
            }

            /// <summary>
            /// 순찰 지점 도착 후 두리번거리기 구현
            /// 몸통의 updateRotation을 해제하여 몸통이 머리의 회전을 특정 비율로 따라간다
            /// </summary>
            private void BeginPatrolWait()
            {
                isPatrolWaiting = true;
                patrolWaitingTimer = 0f;
                agent.ResetPath();
                agent.updateRotation = false; // 두리번거리기 구현, 몸통을 직접 돌린다

                patrolSweepBaseYaw = transform.eulerAngles.y;
                patrolSweepOffset = 0f;
                patrolSweepPhase = 0;
            }

            private void TickPatrolWait(float deltaTime)
            {
                patrolWaitingTimer += deltaTime;
                
                //머리가 향할 각도
                float half = settings.PatrolHeadSweepAngle * 0.5f;
                float targetOffset = patrolSweepPhase == 0 ? half * patrolSweepSign : -half * patrolSweepSign;

                patrolSweepOffset = Mathf.MoveTowards(
                    patrolSweepOffset, targetOffset, settings.PatrolHeadSweepSpeed * deltaTime);
                
                // 머리는 목이 돌아가는 각도만큼 회전, 몸은 특정 비율만큼만 회전
                float bodyOffset = patrolSweepOffset * settings.BodyFollowRatio;
                float headOffset = Mathf.Clamp(
                    patrolSweepOffset - bodyOffset, -settings.HorizontalHeadTurnAngle, settings.HorizontalHeadTurnAngle);

                // 몸통에 월드 기준 회전, 머리에 로컬 기준 회전
                transform.rotation = Quaternion.Euler(0f, patrolSweepBaseYaw + bodyOffset, 0f);
                headTransform.localRotation = Quaternion.Euler(0f, headOffset, 0f);

                if (patrolSweepPhase == 0 && Mathf.Approximately(patrolSweepOffset, targetOffset))
                {
                    patrolSweepPhase = 1;
                }

                if (patrolWaitingTimer >= settings.WaitingTimeAfterPatrolPoint)
                {
                    EndPatrolWait();
                }
            }

            private void EndPatrolWait()
            {
                isPatrolWaiting = false;
                agent.updateRotation = true;
                headTransform.localRotation = Quaternion.identity;
                patrolSweepSign = -patrolSweepSign; // 이번에 왼->오로 둘러봤다면, 다음에는 오->왼으로

                if (TryGetPatrolPoint(out Vector3 point)) agent.SetDestination(point);
            }

            private bool TryGetPatrolPoint(out Vector3 result)
            {
                // 현재 스폰 지점 주위로 랜덤, 차후 순찰 지점 설정 필요
                // 현재 평면을 가정하고 circle로 지점을 추출한다
                // 현재 지점을 찾는데 실패하면 둘러보기를 1회 더 반복, 차후 수정 필요
                Vector2 randomCircle = Random.insideUnitCircle * settings.PatrolRadius;
                Vector3 randomPoint = patrolSpawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

                if (NavMesh.SamplePosition(
                        randomPoint,
                        out NavMeshHit hit,
                        2f,
                        NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }

                result = patrolSpawnPosition;
                return false;
            }

            private void TickAlert(in RuntimeTickContext context)
            {
                if (playerTransform != null && isPlayerVisible)
                {
                    // 플레이어가 시야에 있으므로 위치를 갱신
                    lastKnownPlayerPosition = playerTransform.position;

                    FaceTarget(playerTransform.position, context.DeltaTime);

                    alertness += settings.AlertIncreaseSpeed * context.DeltaTime;
                    if (alertness >= settings.AlertMax)
                    {
                        alertness = settings.AlertMax;
                        ChangeState(EnemyState.Chase);
                    }
                }
                else
                {
                    // 플레이어에 대한 시야를 잃었다면, 경계도를 서서히 깎는다
                    alertness -= settings.AlertDecreaseSpeed * context.DeltaTime;
                    if (alertness <= 0f)
                    {
                        alertness = 0f;
                        ChangeState(EnemyState.Patrol);
                    }
                }
            }

            private void FaceTarget(Vector3 targetPosition, float deltaTime)
            {
                Vector3 toTarget = targetPosition - transform.position;
                toTarget.y = 0f;
                
                // 너무 가까운 경우 불안정, 즉시 return
                if (toTarget.sqrMagnitude < 0.01f)
                {
                    return;
                }

                float targetYaw = Quaternion.LookRotation(toTarget).eulerAngles.y;
                
                // 몸통은 목표를 향해 천천히 돌아간다
                float bodyYaw = Mathf.MoveTowardsAngle(
                    transform.eulerAngles.y, targetYaw, settings.BodyTurnSpeed * deltaTime);
                transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
                
                // 머리는 목표를 향해 몸통이 돈 만큼을 빼서 그만큼 꺾는다
                float remainingYaw = Mathf.DeltaAngle(bodyYaw, targetYaw);
                remainingYaw = Mathf.Clamp(
                    remainingYaw, -settings.HorizontalHeadTurnAngle, settings.HorizontalHeadTurnAngle);

                float headYaw = Mathf.MoveTowardsAngle(
                    headTransform.localEulerAngles.y, remainingYaw, settings.HeadTurnSpeed * deltaTime);
                headTransform.localRotation = Quaternion.Euler(0f, headYaw, 0f);
            }

            private void TickInvestigate(in RuntimeTickContext context)
            {
            }

            private void TickChase(in RuntimeTickContext context)
            {
                if (playerTransform != null && isPlayerVisible)
                {
                    Vector3 playerDelta = playerTransform.position - lastKnownPlayerPosition;
                    if (playerDelta.sqrMagnitude > 0.01f)
                    {
                        lastKnownPlayerDirection = playerDelta.normalized;
                    }

                    // 마지막 본 플레이어의 위치 할당
                    lastKnownPlayerPosition = playerTransform.position;
                    alertness = settings.AlertMax;

                    FaceTarget(playerTransform.position, context.DeltaTime);
                }
                else
                {
                    // 플레이어에 대한 시야를 잃었다면, alertness를 조금씩 깎는다
                    alertness -= settings.ChaseAlertDecreaseSpeed * context.DeltaTime;
                    FaceTarget(lastKnownPlayerPosition, context.DeltaTime);

                    // 놓친 후 alertness가 0까지 내려갔다면,
                    // 마지막으로 놓친 곳으로 돌아가 주위를 Search한다
                    if (alertness <= 0f)
                    {
                        alertness = 0f;

                        float whichSide = Vector3.Dot(transform.right, lastKnownPlayerDirection);
                        searchSweepSign = whichSide >= 0f ? 1 : -1;

                        ChangeState(EnemyState.Search);
                        return;
                    }
                }

                if (!agent.isOnNavMesh) return;
                
                // 경로가 막히는 등, chase에 갇히는 경우를 배제하기 위해 도달하지 못하는 시간 측정
                bool isPathBlocked = !agent.pathPending && agent.hasPath &&
                                     agent.pathStatus != NavMeshPathStatus.PathComplete;

                if (isPathBlocked)
                {
                    stuckTimer += context.DeltaTime;

                    if (stuckTimer >= settings.StuckDuration)
                    {
                        float whichSide = Vector3.Dot(transform.right, lastKnownPlayerDirection);
                        searchSweepSign = whichSide >= 0 ? 1 : -1;

                        ChangeState(EnemyState.Search);
                        return;
                    }
                }

                else
                {
                    stuckTimer = 0f;
                }
                
                // 플레이어가 보이든 아니든 마지막 목격 위치를 향한다
                // 아래가 무조건 참, 영벡터에서 시작
                Vector3 toDestination = lastRequestedDestination - lastKnownPlayerPosition;

                if (toDestination.sqrMagnitude >
                    settings.DestinationUpdateThreshold * settings.DestinationUpdateThreshold)
                {
                    lastRequestedDestination = lastKnownPlayerPosition;
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
                    // 첫 진입(path 없음)이면 아직 방문한 곳이 없다
                    if (agent.hasPath) visitedSearchPointsCount++;

                    // 다음 방문지 설정
                    if (TryGetSearchPoint(out Vector3 point)) agent.SetDestination(point);
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

            
            /// <summary>
            /// 디버깅용 메서드
            /// </summary>
            // 시야 확인용
            private void OnDrawGizmosSelected()
            {
                if (settings == null || headTransform == null) return;

                Vector3 eyePosition = headTransform.position;
                Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(eyePosition, settings.SightDistance);

                // 좌우 시야
                Vector3 left  = Quaternion.AngleAxis(-settings.BothSideSightsAngle * 0.5f, Vector3.up) * forwardFlat;
                Vector3 right = Quaternion.AngleAxis( settings.BothSideSightsAngle * 0.5f, Vector3.up) * forwardFlat;

                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(eyePosition, left  * settings.SightDistance);
                Gizmos.DrawRay(eyePosition, right * settings.SightDistance);

                // 상하 시야
                Vector3 upward   = Quaternion.AngleAxis(-settings.UpSightAngle,   transform.right) * forwardFlat;
                Vector3 downward = Quaternion.AngleAxis( settings.DownSightAngle, transform.right) * forwardFlat;

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(eyePosition, upward   * settings.SightDistance);
                Gizmos.DrawRay(eyePosition, downward * settings.SightDistance);

                if (playerTransform == null) return;
                Vector3 targetPoint = playerTransform.position + Vector3.up * 0.9f;
                Gizmos.color = isPlayerVisible ? Color.green : Color.red;
                Gizmos.DrawLine(eyePosition, targetPoint);
            }

            // 경계도 확인용
            private void OnDrawGizmos()
            {
                if (settings == null || headTransform == null) return;
                if (currentState != EnemyState.Alert || settings.AlertMax <= 0f) return;

                Camera camera = Camera.current;
                if (camera == null) return;

                float fill = Mathf.Clamp01(alertness/ settings.AlertMax);

                Vector3 center = headTransform.position + Vector3.up * 0.8f;
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