    using UnityEngine;
    // NavMeshAgent, NavMesh
    using UnityEngine.AI;
    // IRuntimeTickable, RuntimeTickContext, RuntimeCoordinator
    using ProjectHive.Core.Runtime;
    // GameEventBus
    using ProjectHive.Core.Events;
    using ProjectHive.Core.Contracts;
    using ProjectHive.Combat;
    using System;
    using Random = UnityEngine.Random;

    namespace ProjectHive.AI.Mob
    {
        [RequireComponent(typeof(NavMeshAgent))]
        [RequireComponent(typeof(Health))]
        public sealed class BreckenAI : MonoBehaviour, IRuntimeTickable, IAssassinationStateProvider
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
            
            // 현재 암살 가능 여부 전달
            public bool IsAssassinable =>
                currentState is EnemyState.Idle or EnemyState.Patrol;


            [Header("Player Perception")]
            [SerializeField] private Transform playerTransform;
            [SerializeField] private bool isPlayerVisible;
            [SerializeField] private float lookPitch; // 시선이 수평선으로부터 꺾인 각도
            private const float PlayerHeight = 1.8f; // 플레이어 키, 1.8m, 임시, 차후 웅크리기 등에 맞춰 변경
            private float playerAimHeight = PlayerHeight * 0.7f; // 브레켄이 플레이어를 바라보는 플레이어의 몸통 각도, 명치 부근을 바라본다

            [Header("Damage")]
            private Health health;
            private Vector3 damageSourcePosition;
            private DamageKind lastDamageKind;

            [Header("Enemy State")]
            [SerializeField]
            private EnemyState currentState = EnemyState.Idle;

            [Header("State - Patrol")]
            [SerializeField] private bool isPatrolWaiting;
            [SerializeField] private float patrolWaitingTimer;
            private float patrolSweepBaseYaw; //도착 지점의 몸통 방향
            private float patrolSweepOffset; // 기준점으로부터 머리가 틀어진 각도, 기준은 도착한 순간 몸통의 정면
            private int patrolSweepPhase; // 0: 한쪽끝, 1: 반대쪽 끝
            private int patrolSweepSign = -1;



            [Header("State - Chase")]
            private float lastChaseRemainingDistance = float.PositiveInfinity;

            [Header("State - Attack")]
            private float lastAttackTime = float.NegativeInfinity;

            [Header("State - Search")]
            [SerializeField, Min(1)] private int searchPointCount = 3;
            private int visitedSearchPointsCount;


            private Vector3 lastKnownPlayerPosition;
            private Vector3 lastKnownPlayerDirection;
            private Vector3 lastRequestedDestination;
            // 탐색 반경 기준 계산을 위한, 마지막으로 플레이어를 목격한 시각 
            private float lastKnownPlayerSightingTime;

            private Vector3 patrolSpawnPosition;

            [SerializeField] private float alertness;

            [SerializeField] private RuntimeCoordinator runtimeCoordinator;

            public bool RuntimeTickEnabled => isActiveAndEnabled;
            public Transform RuntimeTransform => transform;
            public bool UseDistanceScaling => true;

            private void Awake()
            {
                agent = GetComponent<NavMeshAgent>();
                health = GetComponent<Health>();
            }

            public void RuntimeTick(in RuntimeTickContext context)
            {
                isPlayerVisible = CanSeePlayer();

                if (currentState == EnemyState.Chase &&
                    isPlayerVisible &&
                    IsPlayerInAttackRange())
                {
                    ChangeState(EnemyState.Attack);
                }

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
                // reset
                lookPitch = 0f;

                // patrol reset
                isPatrolWaiting = false;
                patrolWaitingTimer = 0f;
                patrolSweepBaseYaw = 0f;
                patrolSweepOffset = 0f;
                patrolSweepPhase = 0;
                patrolSweepSign = -1;

                //chase reset

                ResolveServices();
                patrolSpawnPosition = transform.position; // 순찰 기준점, 풀에서 꺼낼 때마다 새로운 기준점
                                                          // 브레켄이 죽은 후 풀 반환 이후
                                                          // 풀에서 다시 꺼낼 때 초기화용
                currentState = EnemyState.Idle;
                alertness = 0f;

                visitedSearchPointsCount = 0;
                lastAttackTime = float.NegativeInfinity;
                damageSourcePosition = Vector3.zero;
                lastDamageKind = DamageKind.Unknown;
                lastKnownPlayerPosition = Vector3.zero;
                lastKnownPlayerDirection = Vector3.zero;
                lastKnownPlayerSightingTime = 0f;
                // 플레이어가 우연히 월드의 원점에 서있는 경우 방지
                lastRequestedDestination = Vector3.positiveInfinity;

                if (agent != null) agent.updateRotation = true; // Alert/Search에서 꺼둔 회전을 되돌린다
                headTransform.localRotation = Quaternion.identity; // Chase에서 바뀐 머리를 정면으로 초기화

                if (health != null)
                {
                    health.Damaged += OnDamaged;
                    health.Died += OnDied;
                }

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

                if (health != null)
                {
                    health.Damaged -= OnDamaged;
                    health.Died -= OnDied;
                }
            }

            private void ResolveServices()
            {
                if (runtimeCoordinator == null)
                {
                    runtimeCoordinator = RuntimeCoordinator.Instance;
                }
            }

            // 시야는 거리로 -> y축을 살려 대각선 계산
            private bool CanSeePlayer()
            {
                if (playerTransform == null || headTransform == null) return false;

                Vector3 eyePosition = headTransform.position;
                // 플레이어의 몸통 위치
                Vector3 targetPoint = playerTransform.position + Vector3.up * playerAimHeight;
                Vector3 toTarget = targetPoint - eyePosition;
                Vector3 toTargetFlat = new Vector3(toTarget.x, 0, toTarget.z);


                // 거리 - 발밑 기준으로
                Vector3 toPlayer = playerTransform.position - transform.position;
                if (toPlayer.sqrMagnitude > settings.SightDistance * settings.SightDistance) return false;

                // 가까울수록 시야가 넓어진다. 좌우와 상하가 같은 비율을 쓴다
                float sightCloseness = Mathf.InverseLerp(
                    settings.SightBoostFarDistance, settings.SightBoostNearDistance, toPlayer.magnitude);

                // 시야각 - 좌우
                Vector3 forwardFlat = new Vector3(transform.forward.x, 0, transform.forward.z);
                float halfFan = Mathf.Lerp(
                    settings.BothSideSightsAngle, settings.CloseSightAngle, sightCloseness) * 0.5f;

                if (Vector3.Angle(forwardFlat, toTargetFlat) > halfFan) return false;

                // 시야각 - 상하, 눈 기준
                float upLimit = Mathf.Lerp(settings.UpSightAngle, settings.CloseUpSightAngle, sightCloseness);
                float downLimit = Mathf.Lerp(settings.DownSightAngle, settings.CloseDownSightAngle, sightCloseness);

                float verticalAngle = Mathf.Atan2(toTarget.y, toTargetFlat.magnitude) * Mathf.Rad2Deg;
                float relativeVerticalAngle = verticalAngle - lookPitch;
                if (relativeVerticalAngle > upLimit || relativeVerticalAngle < -downLimit) return false;

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
                        agent.ResetPath();
                        agent.updateRotation = true;
                        agent.speed = settings.PatrolSpeed;
                        isPatrolWaiting = false;
                        patrolWaitingTimer = 0f;
                        patrolSweepOffset = 0f;
                        patrolSweepPhase = 0;
                        headTransform.localRotation = Quaternion.identity;
                        lookPitch = 0f;
                        break;

                    case EnemyState.Alert:
                        agent.updateRotation = false;
                        if (agent.isOnNavMesh) agent.ResetPath();
                        break;

                    case EnemyState.Chase:
                        lastChaseRemainingDistance = float.PositiveInfinity;
                        agent.updateRotation = false;
                        lastRequestedDestination = Vector3.positiveInfinity;
                        agent.speed = settings.ChaseSpeed;
                        break;

                    case EnemyState.Attack:
                        agent.updateRotation = false;
                        if (agent.isOnNavMesh) agent.ResetPath();
                        agent.speed = settings.ChaseSpeed * settings.AttackSpeedMultiplier;
                        break;

                    case EnemyState.Search:
                        agent.updateRotation = true;
                        if (agent.isOnNavMesh) agent.ResetPath();
                        agent.speed = settings.PatrolSpeed; // 일단 patrol 상태의 이동속도와 동일
                        visitedSearchPointsCount = 0;
                        lookPitch = 0f;
                        break;

                    default:
                        agent.updateRotation = true;
                        lookPitch = 0f;
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

                if (!agent.hasPath ||
                    agent.remainingDistance <= agent.stoppingDistance + settings.ArrivalDistanceThreshold)
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
                    lastKnownPlayerDirection =
                        (playerTransform.position - lastKnownPlayerPosition).normalized;
                    lastKnownPlayerPosition = playerTransform.position;
                    lastKnownPlayerSightingTime = context.CurrentTime;

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
                // 시선을 목표의 수직 각도로 이동, 몸통을 기울이지 않음
                Vector3 eyeToAim = targetPosition + Vector3.up * playerAimHeight - headTransform.position;
                Vector3 eyeToAimFlat = new Vector3(eyeToAim.x, 0f, eyeToAim.z);
                // 브레켄의 머리가 플레이어를 보기 위해 꺾어야 하는 목표 각도
                float toTargetPitch = Mathf.Clamp(
                    Mathf.Atan2(eyeToAim.y, eyeToAimFlat.magnitude) * Mathf.Rad2Deg,
                    -settings.VerticalHeadTurnAngle,
                    settings.VerticalHeadTurnAngle
                );
                lookPitch = Mathf.MoveTowardsAngle(lookPitch, toTargetPitch, settings.HeadTurnSpeed * deltaTime);
                    
                
                Vector3 toTarget = targetPosition - transform.position;
                Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z);

                // 너무 가까운 경우 불안정, 즉시 return
                if (toTargetFlat.sqrMagnitude < 0.01f)
                {
                    return;
                }

                // 회전용
                float targetYaw = Quaternion.LookRotation(toTargetFlat).eulerAngles.y;

                // 몸통은 목표를 향해 천천히 돌아간다
                // 가까울수록 더 빠르게 회전
                // 가까움정도를 봐야하므로 B < 현재값 < A로 사용
                float closeness = Mathf.InverseLerp(
                    settings.TurnBoostFarDistance, settings.TurnBoostNearDistance, toTarget.magnitude);
                float bodyTurnSpeed = Mathf.Lerp(
                    settings.BodyTurnSpeed, settings.CloseBodyTurnSpeed, closeness);

                float bodyYaw = Mathf.MoveTowardsAngle(
                    transform.eulerAngles.y, targetYaw, bodyTurnSpeed * deltaTime);
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
                Debug.Log("State : Investigate");
            }

            private void TickChase(in RuntimeTickContext context)
            {
                // Chase 중 플레이어가 보인다면
                if (playerTransform != null && isPlayerVisible)
                {
                    // 마지막으로 본 플레이어 위치와 방향을 갱신
                    lastKnownPlayerDirection =
                        (playerTransform.position - lastKnownPlayerPosition).normalized;

                    lastKnownPlayerPosition = playerTransform.position;
                    lastKnownPlayerSightingTime = context.CurrentTime;

                    alertness = settings.AlertMax;
                    agent.updateRotation = false;
                    FaceTarget(
                        playerTransform.position,
                        context.DeltaTime);
                }
                // Chase 중 플레이어가 보이지 않는다면
                else
                {
                    // 몸통은 길을 향하고, 시선은 수평으로 돌아온다
                    agent.updateRotation = true;
                    headTransform.localRotation = Quaternion.identity;
                    lookPitch = Mathf.MoveTowardsAngle(
                        lookPitch, 0f, settings.HeadTurnSpeed * context.DeltaTime);
                    
                    // 시야를 잃은 채, 너무 오래 chase 중이라면 탐색으로 넘긴다
                    if (context.CurrentTime - lastKnownPlayerSightingTime >=
                        settings.StuckDuration)
                    {
                        ChangeState(EnemyState.Search);
                        return;
                    }
                }

                if (!agent.isOnNavMesh) return;

                // 플레이어가 보이든 아니든 마지막 목격 위치를 향한다
                float destinationUpdateThresholdSqr =
                    settings.DestinationUpdateThreshold *
                    settings.DestinationUpdateThreshold;

                Vector3 toDestination =
                    lastRequestedDestination -
                    lastKnownPlayerPosition;

                // 마지막 목격 위치가 바뀌었다면 목적지를 갱신한다
                if (toDestination.sqrMagnitude >=
                    destinationUpdateThresholdSqr)
                {
                    bool isDestinationSet =
                        agent.SetDestination(
                            lastKnownPlayerPosition);

                    if (isDestinationSet)
                    {
                        // 목적지 요청에 성공했을 때만 기록한다
                        lastRequestedDestination =
                            lastKnownPlayerPosition;
                    }
                    
                    return;
                }

                // 마지막 목격 위치까지의 거리 계산
                Vector3 toLastKnownPlayerPosition =
                    lastKnownPlayerPosition -
                    transform.position;

                // Y축을 제거하기 전에 수직 거리 저장
                float verticalDistance =
                    Mathf.Abs(
                        toLastKnownPlayerPosition.y);

                // 수평 거리만 별도로 검사한다
                toLastKnownPlayerPosition.y = 0f;

                float horizontalArrivalThreshold =
                    agent.stoppingDistance + settings.ArrivalDistanceThreshold;

                float verticalArrivalThreshold = 1.0f;

                // 플레이어가 보이지 않고,
                // 경로 계산이 끝났으며,
                // 정상 경로로 마지막 목격 위치의 수평·수직 범위에 도착했고,
                // 경로가 끝났거나 남은 거리가 정지 거리 이내라면
                // 마지막 목격 위치에 도착했다고 판단한다
                bool isReachedLastKnownPosition =
                    !isPlayerVisible &&
                    !agent.pathPending &&
                    agent.pathStatus ==
                        NavMeshPathStatus.PathComplete &&
                    toLastKnownPlayerPosition.sqrMagnitude <=
                        horizontalArrivalThreshold *
                        horizontalArrivalThreshold &&
                    verticalDistance <=
                        verticalArrivalThreshold &&
                    (
                        !agent.hasPath ||
                        agent.remainingDistance <=
                        horizontalArrivalThreshold
                    );

                if (isReachedLastKnownPosition)
                {
                    ChangeState(EnemyState.Search);
                    return;
                }
            }



            private void TickAttack(in RuntimeTickContext context)
            {
                if (!isPlayerVisible || !IsPlayerInAttackRange())
                {
                    ChangeState(EnemyState.Chase);
                    return;
                }

                FaceTarget(playerTransform.position, context.DeltaTime);

                lastKnownPlayerDirection = (playerTransform.position - lastKnownPlayerPosition).normalized;
                lastKnownPlayerPosition = playerTransform.position;
                lastKnownPlayerSightingTime = context.CurrentTime;
                alertness = settings.AlertMax;

                if (context.CurrentTime - lastAttackTime < settings.AttackCooldown ||
                    !IsFacingPlayer())
                {
                    return;
                }

                ApplyAttackDamage();
                lastAttackTime = context.CurrentTime;
            }

            private bool IsPlayerInAttackRange()
            {
                if (playerTransform == null) return false;

                Vector3 toPlayer = playerTransform.position - transform.position;
                float verticalDistance = Mathf.Abs(toPlayer.y);
                toPlayer.y = 0f;

                return toPlayer.sqrMagnitude <= settings.AttackRange * settings.AttackRange &&
                    verticalDistance <= 1.0f;
            }

            private bool IsFacingPlayer()
            {
                if (playerTransform == null) return false;

                Vector3 toPlayer = playerTransform.position - transform.position;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude < 0.01f) return true;

                return Vector3.Angle(transform.forward, toPlayer) <= settings.AttackHalfAngle;
            }

            private void ApplyAttackDamage()
            {
                if (playerTransform == null) return;

                IDamageable target = playerTransform.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) return;

                Vector3 hitPoint = playerTransform.position + Vector3.up * (PlayerHeight * 0.5f);
                Vector3 direction = playerTransform.position - transform.position;
                DamageData damage = new DamageData(
                    settings.AttackDamage,
                    DamageKind.Monster,
                    DamageHitZone.Body,
                    DamageFlags.CanCauseBleeding,
                    hitPoint,
                    direction,
                    gameObject);

                target.ApplyDamage(in damage);
            }

            private void OnDamaged(Health damagedHealth, DamageData damage)
            {
                lastDamageKind = damage.Kind;

                if (damage.Source == null) return;

                damageSourcePosition = damage.Source.transform.position;
            }

            private void OnDied(Health deadHealth)
            {
                if (agent != null)
                    agent.enabled = false;

                if (lastDamageKind != DamageKind.Assassination)
                    LieDown();

                enabled = false;
            }

            private void LieDown()
            {
                BoxCollider body = null;
                Collider[] colliders = GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i].name == "BreckenBody")
                    {
                        body = colliders[i] as BoxCollider;
                        break;
                    }
                }

                float halfThickness = body != null
                    ? body.size.z * body.transform.lossyScale.z * 0.5f
                    : 0.5f;

                Vector3 toShooter = damageSourcePosition - transform.position;
                bool fallForward = Vector3.Dot(transform.forward, toShooter) < 0f;

                transform.SetPositionAndRotation(
                    transform.position + Vector3.up * halfThickness,
                    Quaternion.Euler(fallForward ? 90f : -90f, transform.eulerAngles.y, 0f));
            }

            private void TickSearch(in RuntimeTickContext context)
            {
                // Search 상태에서 player를 발견하면 즉시 추격
                if (playerTransform != null && isPlayerVisible)
                {
                    ChangeState(EnemyState.Chase);
                    return;
                }
                
                // 경계도를 증가시키는 외부 자극이 없다면, 경계도가 감소한다
                alertness -= settings.SearchAlertDecreaseSpeed * context.DeltaTime;
                
                if (alertness <= 0)
                {
                    alertness = 0f;
                    ChangeState(EnemyState.Patrol);
                    return;
                }

                if (!agent.isOnNavMesh) return;

                // 경로가 계산 중이라면, 도착 판정을 금한다
                if (agent.pathPending) return;

                // 목적지가 없거나, 이미 도착했다면 다음 지점을 향한다
                if (!agent.hasPath ||
                    agent.remainingDistance <= agent.stoppingDistance + settings.ArrivalDistanceThreshold) 
                {
                    MoveToNextSearchPoint(context);
                }
            }

            /// <summary>
            /// 다음 탐색 지점 지정
            /// 지점을 찾지 못하면 목적지를 잡지 않으며,  다음 틱에 다시 시도
            /// </summary>
            private void MoveToNextSearchPoint(in RuntimeTickContext context)
            {
                if (TryGetSearchPoint(context, out Vector3 point))
                {
                    agent.SetDestination(point);
                }
            }

            

            private bool TryGetSearchPoint(in RuntimeTickContext context, out Vector3 result)
            {
                // 시야를 잃은 뒤 흐른 시간
                float timeElapsed = context.CurrentTime - lastKnownPlayerSightingTime;
                
                // 그 시간 동안 플레이어가 이동한 거리 추정, 추정치를 반경으로 삼는다
                float searchRadius = Mathf.Min(
                    settings.AssumedPlayerSpeed * settings.FleeDirectness * timeElapsed,
                    settings.MaxSearchRadius);
                
                // 탐색 시간 대비 진행도, patrol 전이에 가까워질수록 부채꼴이 넓어진다
                float searchProgress = Mathf.Clamp01(1f - alertness / settings.AlertMax);

                Vector3 searchDirection = lastKnownPlayerDirection;
                searchDirection.y = 0f; 

                float halfAngle;
                if (searchDirection.sqrMagnitude < 0.01f)
                {
                    // 사라진 방향을 모르면 처음부터 원으로 뒤진다
                    searchDirection = transform.forward;
                    halfAngle = 180f;
                }
                else
                {
                    searchDirection.Normalize();
                    halfAngle = Mathf.Lerp(
                        settings.SearchStartHalfAngle,
                        settings.SearchEndHalfAngle,
                        searchProgress
                    );
                }

                // 랜덤 방향
                Vector3 direction =
                    Quaternion.Euler(0f, Random.Range(-halfAngle, halfAngle), 0f) * searchDirection;
                // 그 방향의 랜덤 거리만큼 이동한 지점
                Vector3 candidate =
                    lastKnownPlayerPosition + direction * Random.Range(0f, searchRadius);

                // 2m 내에 hit했다면 true, 아니면 마지막 위치라도 반환하며 false
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
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

            // 거리 판정은 발밑 기준이므로 원도 발밑에 그린다
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, settings.SightDistance);

            // 지금 이 거리에서 실제로 적용되는 각도를 구한다
            float distance = playerTransform != null
                ? (playerTransform.position - transform.position).magnitude
                : settings.SightBoostFarDistance;
            float sightCloseness = Mathf.InverseLerp(
                settings.SightBoostFarDistance, settings.SightBoostNearDistance, distance);
            float halfFan = Mathf.Lerp(
                settings.BothSideSightsAngle, settings.CloseSightAngle, sightCloseness) * 0.5f;
            float upLimit = Mathf.Lerp(settings.UpSightAngle, settings.CloseUpSightAngle, sightCloseness);
            float downLimit = Mathf.Lerp(settings.DownSightAngle, settings.CloseDownSightAngle, sightCloseness);

            // 좌우 시야
            Vector3 left = Quaternion.AngleAxis(-halfFan, Vector3.up) * forwardFlat;
            Vector3 right = Quaternion.AngleAxis(halfFan, Vector3.up) * forwardFlat;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(eyePosition, left * settings.SightDistance);
            Gizmos.DrawRay(eyePosition, right * settings.SightDistance);

            // 시선 중심선, lookPitch만큼 기울어져 있다
            Vector3 lookForward = Quaternion.AngleAxis(-lookPitch, transform.right) * forwardFlat;

            Gizmos.color = Color.gray;
            Gizmos.DrawRay(eyePosition, lookForward * settings.SightDistance);

            // 상하 시야, 시선을 기준으로 벌어진다
            Vector3 upward = Quaternion.AngleAxis(-upLimit, transform.right) * lookForward;
            Vector3 downward = Quaternion.AngleAxis(downLimit, transform.right) * lookForward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(eyePosition, upward * settings.SightDistance);
            Gizmos.DrawRay(eyePosition, downward * settings.SightDistance);

            if (playerTransform == null) return;
            Vector3 targetPoint = playerTransform.position + Vector3.up * playerAimHeight;
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

                float fill = Mathf.Clamp01(alertness / settings.AlertMax);

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
