using UnityEngine;
// NavMeshAgent, NavMesh
using UnityEngine.AI;
// IRuntimeTickable, RuntimeTickContext, RuntimeCoordinator
using ProjectHive.Core.Runtime;
// GameEventBus
using ProjectHive.Core.Events;
// IAssassinationStateProvider, IDamageable, DamageData
using ProjectHive.Core.Contracts;
using ProjectHive.Combat;
using ProjectHive.AI.Hive;
using Random = UnityEngine.Random;
using System.Collections.Generic;

namespace ProjectHive.AI.Mob
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public sealed class BreckenAI : MonoBehaviour, IRuntimeTickable, IAssassinationStateProvider, IHiveControllable
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

        [Header("Hear")]
        // 소리에 의한 판단은 틱 주기에 이루어져야 하므로, 콜백이 적어두고 틱이 읽어 간다
        private bool isHeardNoise;
        private Vector3 heardNoisePosition;
        private float heardNoiseOccurredTime;
        // 들은 순간의 거리 / 소리 반경 - 0이면 바로 앞, 1이면 한계점
        // 들은 시점에 기록
        private float heardNoiseDistanceRatio;
        // 들은 소리를 얼마나 오래 기억할 지
        // TODO 플레이테스트 후 settings로
        private const float NoiseMemoryDuration = 5f;

        [Header("Damage")]
        // 사망 처리에 필요한 값만 콜백이 적어둔다
        private Health health;
        // 사망 쓰러짐 임시 - 8/23
        // 맞은 순간에 복사해 둔 가해 위치, 쓰러질 방향 계산에 쓴다
        private Vector3 damageSourcePosition;
        // 암살은 김철희의 연출이 몸을 움직이므로 눕히기에서 제외한다
        private DamageKind lastDamageKind;



        [Header("Enemy State")]
        [SerializeField] private EnemyState currentState = EnemyState.Idle;

        [Header("State - Patrol")]
        [SerializeField] private bool isPatrolWaiting;
        [SerializeField] private float patrolWaitingTimer;
        private float patrolSweepBaseYaw; //도착 지점의 몸통 방향
        private float patrolSweepOffset; // 기준점으로부터 머리가 틀어진 각도, 기준은 도착한 순간 몸통의 정면
        private int patrolSweepPhase; // 0: 한쪽끝, 1: 반대쪽 끝
        private int patrolSweepSign = -1;
        private Vector3 patrolSpawnPosition;
        // 순찰 지점 하나, 위치와 그 자리에서의 트인 방향을 담는다
        private struct PatrolPoint
        {
            public Vector3 Position;
            public Vector3 LookDirection;
        }
        private PatrolPoint[] patrolPoints;
        // 이번 사이클의 순찰 순서
        private int[] patrolOrder;
        private int patrolOrderIndex;
        private Vector3 patrolLookDirection;
        // 도달 가능 여부 검사용 버퍼
        private NavMeshPath patrolPath;
        // 후보를 모으는 중이면 non-null, 확정하면 다시 null로 돌린다
        private List<Vector3> patrolCandidates;
        // 다음 후보를 검사해도 되는 시각, 경로 요청 주기를 지키기 위한 것
        private float nextPatrolCandidateTime;
        // 후보를 몇 번 시도했는지, 갈 수 없는 지형에서 폭주하지 않게 막는다
        private int patrolCandidateAttempts;


        [Header("State - Investigate")]
        // 지금 확인하러 가는 지점과 훑을 반경
        // 직접 들은 소리가 채웠는지, 하이브에 명령에 의해 채워졌는지 구분하지 않는다
        private Vector3 investigatePosition;
        private float investigateRadius;
        // 수색 임무를 만들어낸 자극의 발생 시각. 최신 정보를 더 우선해서 받아들이며, 타임아웃을 결정
        private float investigateStimulusTime;
        private int visitedInvestigatePointCount;
        private NavMeshPath investigatePath;


        [Header("State - Attack")]
        // 마지막으로 때린 시각, 쿨다운 계산용
        private float lastAttackTime = float.NegativeInfinity;



        private Vector3 lastKnownPlayerPosition;
        private Vector3 lastKnownPlayerDirection;
        private Vector3 lastRequestedDestination;
        // 탐색 반경 기준 계산을 위한, 마지막으로 플레이어를 목격한 시각
        private float lastKnownPlayerSightingTime;


        [SerializeField] private float alertness;


        [Header("Hive")]
        [SerializeField] private HiveUnitRegistry hiveUnitRegistry;

        [SerializeField] private HiveUnitRole hiveRole = HiveUnitRole.Hunter;
        [SerializeField]
        private HiveUnitCapabilities hiveCapabilities =
            HiveUnitCapabilities.GroundMovement |
            HiveUnitCapabilities.Investigate |
            HiveUnitCapabilities.Guard |
            HiveUnitCapabilities.Attack;

        // 하이브가 스냅샷에 담아가는 값, 0이면 수행 중인 명령 없음
        private int currentHiveCommandSequence;
        // 명령도 콜백이 적어두고 틱이 읽어간다
        private bool hasPendingCommand;
        private Vector3 pendingCommandPosition;
        private float pendingCommandRadius;
        // 소리가 세상에 발생한 시각을 명령 하달에 그대로 사용
        private float pendingCommandIssuedTime;

        // 소리 반응, 명령 수락, 하이브 후보 자격을 이것으로 판별
        private bool CanReactToStimulus =>
            !isPlayerVisible &&
            currentState != EnemyState.Chase &&
            currentState != EnemyState.Attack;

        // 마지막으로 목격 보고를 올린 시각
        private float lastVisualContactReportTime = float.NegativeInfinity;


        [SerializeField] private RuntimeCoordinator runtimeCoordinator;
        public bool RuntimeTickEnabled => isActiveAndEnabled;
        public Transform RuntimeTransform => transform;
        public bool UseDistanceScaling => true;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            investigatePath = new NavMeshPath();
            patrolPath = new NavMeshPath();
        }

        public void RuntimeTick(in RuntimeTickContext context)
        {
            // 오래된 소리라면 잊는다
            if (isHeardNoise && context.CurrentTime - heardNoiseOccurredTime > NoiseMemoryDuration)
            {
                isHeardNoise = false;
            }

            isPlayerVisible = CanSeePlayer();

            // 자극 판정은 상태와 무관하게 RuntimeTick에서 판별
            // 반응을 하지 못하는 동안 들어온 자극은 버린다
            // chase -> search로 전이해버리면 터지기 때문
            if (!CanReactToStimulus)
            {
                isHeardNoise = false;
                hasPendingCommand = false;
            }
            else if (TryTakeInvestigateOrder(currentState == EnemyState.Investigate))
            {
                visitedInvestigatePointCount = 0;
                if (agent.isOnNavMesh) agent.ResetPath();
                ChangeState(EnemyState.Investigate); // 이미 investigate라면 무시
            }

            // 사거리 안이라면 chase의 도착 판정보다 먼저 Attack으로 전이
            // switch 앞에서 처리해야 같은 틱에 타격한다
            if (currentState == EnemyState.Chase &&
                IsPlayerInAttackRange() &&
                (isPlayerVisible || IsInChaseClairvoyance(context)))
            {
                ChangeState(EnemyState.Attack);
            }

            // 쫓는 중, 플레이어가 보이면 주기적으로 하이브에게 보고한다
            if (isPlayerVisible &&
            (currentState == EnemyState.Chase || currentState == EnemyState.Attack) &&
            context.CurrentTime - lastVisualContactReportTime >= settings.VisualContactReportInterval)
            {
                ReportToHive(
                    EnemyReportKind.VisualContact,
                    playerTransform.position,
                    1f,
                    context.CurrentTime
                );
                lastVisualContactReportTime = context.CurrentTime;
            }

            // 순찰 지점 후보는 경로 계산이 비싸므로 틱마다 하나씩만 모은다
            if (patrolPoints == null) CollectPatrolCandidate(context);

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
            isPlayerVisible = false;
            currentHiveCommandSequence = 0;
            isHeardNoise = false;
            hasPendingCommand = false;
            lastVisualContactReportTime = float.NegativeInfinity;


            // patrol reset
            isPatrolWaiting = false;
            patrolWaitingTimer = 0f;
            patrolSweepBaseYaw = 0f;
            patrolSweepOffset = 0f;
            patrolSweepPhase = 0;
            patrolSweepSign = -1;
            patrolPoints = null;
            patrolLookDirection = Vector3.zero;
            // 풀에서 다시 꺼내면 수집도 처음부터
            patrolCandidates = null;
            nextPatrolCandidateTime = 0f;
            patrolCandidateAttempts = 0;

            //chase reset

            ResolveServices();
            patrolSpawnPosition = transform.position; // 순찰 기준점, 풀에서 꺼낼 때마다 새로운 기준점
                                                      // 브레켄이 죽은 후 풀 반환 이후
                                                      // 풀에서 다시 꺼낼 때 초기화용
            currentState = EnemyState.Idle;
            alertness = 0f;

            investigatePosition = Vector3.zero;
            investigateRadius = 0f;
            investigateStimulusTime = 0f;
            visitedInvestigatePointCount = 0;

            lastAttackTime = float.NegativeInfinity;

            damageSourcePosition = Vector3.zero;
            lastDamageKind = DamageKind.Unknown;

            lastKnownPlayerPosition = Vector3.zero;
            lastKnownPlayerDirection = Vector3.zero;
            lastKnownPlayerSightingTime = 0f;
            // 플레이어가 우연히 월드의 원점에 서있는 경우 방지
            lastRequestedDestination = Vector3.positiveInfinity;

            if (agent != null)
            {
                agent.enabled = true; // 사망 시 꺼둔 것을 다시 켠다
                agent.updateRotation = true; // Alert/Search에서 꺼둔 회전을 되돌린다
            }
            headTransform.localRotation = Quaternion.identity; // Chase에서 바뀐 머리를 정면으로 초기화
                                                               // 사망 쓰러짐 임시 - 8/23
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);


            hiveUnitRegistry?.Register(this);
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.NoiseEmitted += OnNoiseEmitted;
            }
            health.Damaged += OnDamaged;
            health.Died += OnDied;
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

            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.NoiseEmitted -= OnNoiseEmitted;
            }

            health.Damaged -= OnDamaged;
            health.Died -= OnDied;

            hiveUnitRegistry?.Unregister(this);
        }

        private void ResolveServices()
        {
            if (runtimeCoordinator == null)
            {
                runtimeCoordinator = RuntimeCoordinator.Instance;
            }
            if (hiveUnitRegistry == null)
            {
                hiveUnitRegistry = FindFirstObjectByType<HiveUnitRegistry>();
            }
        }

        public HiveUnitSnapshot GetHiveUnitSnapshot()
        {
            return new HiveUnitSnapshot(
                gameObject.GetInstanceID(),
                transform.position,
                hiveRole,
                hiveCapabilities,
                settings.ChaseSpeed,
                CanReactToStimulus, // 소리에 반응하는 것과 같은 기준으로 설정
                currentHiveCommandSequence);
        }

        public bool TryAcceptHiveCommand(in HiveCommand command)
        {
            if (command.Kind == HiveCommandKind.None) return false;
            if (command.IsExpired(Time.time)) return false;
            if (!CanReactToStimulus) return false;

            // 이미 수색 중이라면 수색 명령을 받지 않는다
            // LostTarget 보고를 되받아 부채꼴 Search 반경을 잃는 경우 방지
            if (command.Kind == HiveCommandKind.SearchArea &&
                currentState == EnemyState.Search)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(
                command.TargetPosition, out NavMeshHit hit,
                settings.InvestigateMaxRadius, NavMesh.AllAreas
            ))
            {
                return false;
            }

            // 판단은 틱에서 수행, 여기서는 기록만
            hasPendingCommand = true;
            pendingCommandPosition = hit.position;
            pendingCommandRadius = command.Radius;
            pendingCommandIssuedTime = command.IssuedAt;
            currentHiveCommandSequence = command.Sequence;
            return true;
        }

        /// <summary>
        /// 하이브에게 보고를 올린다
        /// 버스가 없는 씬에서는 조용히 넘어간다
        /// </summary>
        private void ReportToHive(EnemyReportKind kind, Vector3 position, float confidence, float time)
        {
            if (GameEventBus.Instance == null) return;

            GameEventBus.Instance.PublishEnemyReport(new EnemyReport(
                kind, position, confidence, gameObject.GetInstanceID(), time
            ));
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

        /// <summary>
        /// 소리를 듣는다, 기록만 한 후 판단은 틱 주기에 맞춘다
        /// </summary>
        private void OnNoiseEmitted(in NoiseEvent noiseEvent)
        {
            // 동족이 낸 소리에 반응하지 않는다
            if (noiseEvent.Affiliation == NoiseAffiliation.Monster) return;

            // 소리가 닿는 반경 밖이라면, 듣지 못한다
            Vector3 toNoise = noiseEvent.Position - transform.position;
            if (toNoise.sqrMagnitude >= noiseEvent.Radius * noiseEvent.Radius) return;

            // 소리 위치 정보를 navmesh 바닥으로 꽂는다
            // 갈 수 없는 장소라면 소리를 버린다
            if (!NavMesh.SamplePosition(
                noiseEvent.Position, out NavMeshHit hit,
                settings.InvestigateMaxRadius, NavMesh.AllAreas
            ))
            {
                return;
            }

            // 더 최근 소리가 이전 소리를 덮어씌운다
            isHeardNoise = true;
            heardNoisePosition = hit.position;
            heardNoiseOccurredTime = noiseEvent.OccurredAt;
            // 들은 소리의 최대 크기에 비례하여 내가 얼마나 가깝게 들었는지를 필드에 저장
            // 이 값을 이용하여 수색 반경을 넓히거나 좁힌다
            heardNoiseDistanceRatio = noiseEvent.Radius > 0f
                ? Mathf.Clamp01(toNoise.magnitude / noiseEvent.Radius)
                : 1f;

            Debug.Log($"소리 들음: {noiseEvent.Category} at {noiseEvent.Position} (거리 {toNoise.magnitude:F1}m)", this);
        }

        /// <summary>
        /// 피해를 입는다
        /// 피격 반응은 보류 상태라, 사망 처리에 필요한 값만 기록한다
        /// </summary>
        private void OnDamaged(Health damagedHealth, DamageData damage)
        {
            // 사망 쓰러짐 임시 - 8/23
            lastDamageKind = damage.Kind;

            if (damage.Source == null) return;

            // 더 최근 피격이 이전 피격을 덮는다
            damageSourcePosition = damage.Source.transform.position;
        }

        /// <summary>
        /// 죽으면 이동과 판단을 멈춘다
        /// 오브젝트는 일단 남긴다 - 암살 연출이 이 오브젝트에서 코루틴을 돌리며
        /// 트랜스폼을 움직이므로 NavMeshAgent도 같이 끈다
        /// </summary>
        private void OnDied(Health deadHealth)
        {
            if (agent != null) agent.enabled = false;

            // 사망 쓰러짐 임시 - 8/23
            if (lastDamageKind != DamageKind.Assassination)
            {
                LieDown();
            }

            enabled = false;
        }

        // 사망 쓰러짐 임시 - 8/23
        /// <summary>
        /// 그 자리에서 바닥에 눕는다 (임시 - 사망 애니메이션 전까지)
        /// 맞은 반대쪽으로 넘어진다
        /// </summary>
        private void LieDown()
        {
            BoxCollider body = null;
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (c.name == "BreckenBody") body = c as BoxCollider;
            }
            // 누우면 몸통 두께의 절반만큼 띄워야 바닥에 걸친다
            // bounds는 회전에 따라 부풀어 오르는 AABB라 실제 두께를 직접 계산한다
            float halfThickness = body != null
                ? body.size.z * body.transform.lossyScale.z * 0.5f
                : 0.5f;

            Vector3 toShooter = damageSourcePosition - transform.position;
            bool fallForward = Vector3.Dot(transform.forward, toShooter) < 0f;

            transform.SetPositionAndRotation(
                transform.position + Vector3.up * halfThickness,
                Quaternion.Euler(fallForward ? 90f : -90f, transform.eulerAngles.y, 0f));
        }

        /// <summary>
        /// 대기 중인 소리와 명령 중 나중에 발생한 쪽을 현재 작업으로 삼는다
        /// 새 작업이 잡혔다면 true, 병합하거나 버렸다면 false
        /// </summary>
        private bool TryTakeInvestigateOrder(bool isAlreadyInvestigating)
        {
            if (!isHeardNoise && !hasPendingCommand) return false;

            float mergeSqr = Mathf.Pow(settings.InvestigateMergeDistance, 2);

            // 도착 순서가 아닌 발생 시각으로 비교
            // 더 최신 정보이거나, 완전히 같은 사건이라면 브레켄의 귀로 판단
            bool takeNoise = isHeardNoise &&
                             (!hasPendingCommand || heardNoiseOccurredTime >= pendingCommandIssuedTime);


            // 같은 사건이 두 개의 경로로 들어왔다면 내 귀를 신뢰
            bool isSameEvent = isHeardNoise && hasPendingCommand &&
                               (heardNoisePosition - pendingCommandPosition).sqrMagnitude <= mergeSqr;

            if (isSameEvent) takeNoise = true;

            // 소리를 들었나 ? 들음 : 듣지 않음
            Vector3 position = takeNoise ? heardNoisePosition : pendingCommandPosition;
            float radius = takeNoise ? GetHeardNoiseRadius() : pendingCommandRadius;
            float stimulusTime = isSameEvent
                ? Mathf.Max(heardNoiseOccurredTime, pendingCommandIssuedTime)
                : (takeNoise ? heardNoiseOccurredTime : pendingCommandIssuedTime);
            bool isHeard = takeNoise;

            // 선택하지 않은 쪽은 더 오래된 자극이므로 버린다
            isHeardNoise = false;
            hasPendingCommand = false;

            if (isAlreadyInvestigating)
            {
                // 사실상 같은 곳이라고 판단하면 다시 출발하지 않는다
                // 조사 기한을 늘리고 처음부터 다시 훑는다
                if ((position - investigatePosition).sqrMagnitude <= mergeSqr)
                {
                    investigateStimulusTime = Mathf.Max(investigateStimulusTime, stimulusTime);
                    if (isHeard) investigateRadius = radius;
                    visitedInvestigatePointCount = 0;
                    return false;
                }

                // 이미 더 최신의 자극으로 행동하고 있다면 무시
                if (stimulusTime <= investigateStimulusTime) return false;
            }

            investigatePosition = position;
            investigateRadius = radius;
            investigateStimulusTime = stimulusTime;
            return true;
        }

        /// <summary>
        /// 직접 들은 소리의 탐색 반경을 계산하여 반환
        /// </summary>
        private float GetHeardNoiseRadius()
        {
            return Mathf.Lerp(
                settings.InvestigateMinRadius,
                settings.InvestigateMaxRadius,
                heardNoiseDistanceRatio);
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
                    patrolLookDirection = Vector3.zero;
                    break;

                case EnemyState.Alert:
                    agent.updateRotation = false;
                    if (agent.isOnNavMesh) agent.ResetPath();
                    break;

                case EnemyState.Chase:
                    agent.updateRotation = false;
                    lastRequestedDestination = Vector3.positiveInfinity;
                    agent.speed = settings.ChaseSpeed;
                    break;

                case EnemyState.Search:
                    agent.updateRotation = true;
                    if (agent.isOnNavMesh) agent.ResetPath();
                    agent.speed = settings.SearchSpeed;
                    lookPitch = 0f;

                    // 플레이어를 놓쳤다는 사실을 하이브에게 1회 보고
                    // 마지막으로 본 장소이므로 신뢰도를 낮춰서 전달한다
                    ReportToHive(
                        EnemyReportKind.LostTarget,
                        lastKnownPlayerPosition,
                        0.7f,
                        Time.time
                    );
                    break;

                case EnemyState.Investigate:
                    agent.updateRotation = true;
                    if (agent.isOnNavMesh) agent.ResetPath();
                    agent.speed = settings.InvestigateSpeed;
                    visitedInvestigatePointCount = 0;
                    headTransform.localRotation = Quaternion.identity;
                    lookPitch = 0f;
                    break;


                case EnemyState.Attack:
                    agent.updateRotation = false;
                    agent.speed = settings.ChaseSpeed * settings.AttackSpeedMultiplier;
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

            if (agent.pathPending) return;

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

            patrolSweepBaseYaw = patrolLookDirection.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(patrolLookDirection).eulerAngles.y
                : transform.eulerAngles.y;
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

            // transform.rotation = Quaternion.Euler(0f, patrolSweepBaseYaw + bodyOffset, 0f);
            // 트인 방향이 뒤쪽이면 목표 각도가 멀 수 있으므로 돌아가는 과정을 보여준다
            float desiredYaw = patrolSweepBaseYaw + bodyOffset;
            float steppedYaw = Mathf.MoveTowardsAngle(
                transform.eulerAngles.y, desiredYaw, settings.BodyTurnSpeed * deltaTime);
            transform.rotation = Quaternion.Euler(0f, steppedYaw, 0f);
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
            // 아직 모으는 중이거나 하나도 못 뽑았다면 즉석에서 뽑는다
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                patrolLookDirection = Vector3.zero;
                return TryGetPointNearSpawn(out result);
            }

            // 한 바퀴 모두 돌았다면 순서를 섞는다
            if (patrolOrderIndex >= patrolOrder.Length) ShufflePatrolOrder();

            PatrolPoint point = patrolPoints[patrolOrder[patrolOrderIndex]];
            patrolOrderIndex++;

            patrolLookDirection = point.LookDirection;
            result = point.Position;
            return true;
        }

        /// <summary>
        /// 순찰 지점을 뽑지 못한 경우 폴백
        /// </summary>
        private bool TryGetPointNearSpawn(out Vector3 result)
        {
            Vector2 randomCircle = Random.insideUnitCircle * settings.PatrolRadius;
            Vector3 randomPoint = patrolSpawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas) &&
                agent.CalculatePath(hit.position, patrolPath) &&
                patrolPath.status == NavMeshPathStatus.PathComplete)
            {
                result = hit.position;
                return true;
            }

            result = hit.position;
            return false;
        }

        /// <summary>
        /// 순찰 지점 후보를 틱마다 하나씩 모은다
        /// 경로 요청이 비싸서 한 번에 몰면 프레임이 튄다
        /// </summary>
        private void CollectPatrolCandidate(in RuntimeTickContext context)
        {
            if (!agent.isOnNavMesh) return;

            if (context.CurrentTime < nextPatrolCandidateTime) return;
            nextPatrolCandidateTime = context.CurrentTime + settings.RequestPathTickInterval;

            if (patrolCandidates == null)
            {
                patrolCandidates = new List<Vector3>(settings.PatrolPointCandidateCount);
                patrolCandidateAttempts = 0;
            }

            patrolCandidateAttempts++;
            TryAddPatrolCandidate();

            // 목표만큼 모았거나 시도를 다 썼으면 확정한다
            if (patrolCandidates.Count >= settings.PatrolPointCandidateCount ||
                patrolCandidateAttempts >= settings.PatrolPointMaxAttempts)
            {
                FinalizePatrolPoints();
            }
        }

        /// <summary>
        /// 후보 하나를 검사해 조건을 만족하면 목록에 담는다
        /// </summary>
        private void TryAddPatrolCandidate()
        {
            Vector2 circle = Random.insideUnitCircle * settings.PatrolRadius;
            Vector3 candidate = patrolSpawnPosition + new Vector3(circle.x, 0f, circle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;

            // 지점이 뭉치면 제자리걸음이 된다
            float spacingSqr = settings.PatrolPointSpacing * settings.PatrolPointSpacing;
            for (int i = 0; i < patrolCandidates.Count; i++)
            {
                if ((patrolCandidates[i] - hit.position).sqrMagnitude < spacingSqr) return;
            }

            // 바닥이 있는 것과 갈 수 있는 것은 다르다
            if (!agent.CalculatePath(hit.position, patrolPath) ||
                patrolPath.status != NavMeshPathStatus.PathComplete) return;

            patrolCandidates.Add(hit.position);
        }

        /// <summary>
        /// 모은 후보 중 트인 자리부터 순찰 지점으로 확정한다
        /// </summary>
        private void FinalizePatrolPoints()
        {
            // 트인 자리일수록 높은 점수를 부여, 점수순으로 정렬
            var candidates = patrolCandidates;
            int count = candidates.Count;
            var sortKey = new float[count];
            var sorted = new int[count];
            var lookDirections = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                sortKey[i] = -MeasureOpenness(candidates[i], out lookDirections[i]);
                sorted[i] = i;
            }

            System.Array.Sort(sortKey, sorted);

            int take = Mathf.Min(settings.PatrolPointCount, count);
            patrolPoints = new PatrolPoint[take];
            for (int i = 0; i < take; i++)
            {
                patrolPoints[i].Position = candidates[sorted[i]];
                patrolPoints[i].LookDirection = lookDirections[sorted[i]];
            }

            patrolOrder = new int[take];
            for (int i = 0; i < take; i++) patrolOrder[i] = i;
            patrolOrderIndex = take;

            patrolCandidates = null;

            // 하나도 뽑지 못했다면 확정 x
            if (take == 0)
            {
                patrolPoints = null;
                patrolCandidateAttempts = 0;
            }

        }


        /// <summary>
        /// 그 자리에 섰을 때 얼마나 넓게 보이는지를 재고, 가장 트인 방향을 반환
        /// </summary>
        /// <summary>
        /// 그 자리에 섰을 때 얼마나 넓게 보이는지를 재고, 가장 트인 방향을 반환
        /// 방향은 이웃한 세 갈래의 합으로 고른다, 좁은 틈 하나가 이기지 않도록
        /// </summary>
        private float MeasureOpenness(Vector3 point, out Vector3 bestDirection)
        {
            Vector3 eye = point + Vector3.up * settings.EyeHeight;
            int rays = settings.PatrolPointOpennessRays;

            // 방향별 거리를 먼저 모은다
            var distances = new float[rays];
            float total = 0f;
            for (int i = 0; i < rays; i++)
            {
                float angle = i * (360f / rays) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                distances[i] = Physics.Raycast(
                    eye, direction, out RaycastHit hit,
                    settings.SightDistance, settings.SightBlockMask)
                    ? hit.distance
                    : settings.SightDistance;

                total += distances[i];
            }

            // 자기와 좌우 이웃의 합이 가장 큰 갈래를 고른다
            // 동점이면 무작위로 골라 특정 방향으로 쏠리지 않게 한다
            float bestScore = -1f;
            int bestIndex = 0;
            int tieCount = 0;
            for (int i = 0; i < rays; i++)
            {
                float score = distances[(i - 1 + rays) % rays]
                            + distances[i]
                            + distances[(i + 1) % rays];

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                    tieCount = 1;
                }
                else if (Mathf.Approximately(score, bestScore))
                {
                    tieCount++;
                    // 동점 후보 중 하나를 균등하게 고른다
                    if (Random.Range(0, tieCount) == 0) bestIndex = i;
                }
            }

            float bestAngle = bestIndex * (360f / rays) * Mathf.Deg2Rad;
            bestDirection = new Vector3(Mathf.Cos(bestAngle), 0f, Mathf.Sin(bestAngle));

            return total / rays;
        }

        /// <summary>
        /// patrolPoints에 대해 방문순서 섞기
        /// </summary>
        private void ShufflePatrolOrder()
        {
            // 직전 바퀴의 마지막 지점
            int previousLastPoint = patrolOrder[patrolOrder.Length - 1];

            for (int i = patrolOrder.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = patrolOrder[i];
                patrolOrder[i] = patrolOrder[j];
                patrolOrder[j] = swap;
            }

            if (patrolOrder.Length > 1 && patrolOrder[0] == previousLastPoint)
            {
                int swap = patrolOrder[0];
                patrolOrder[0] = patrolOrder[1];
                patrolOrder[1] = swap;
            }

            patrolOrderIndex = 0;
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
            // 주의를 기울이는 중이므로
            // search와 동일하게 보이면 바로 쫓아간다
            if (playerTransform != null && isPlayerVisible)
            {
                lastKnownPlayerDirection =
                    (playerTransform.position - lastKnownPlayerPosition).normalized;
                lastKnownPlayerPosition = playerTransform.position;
                lastKnownPlayerSightingTime = context.CurrentTime;

                alertness = settings.AlertMax;
                ChangeState(EnemyState.Chase);
                return;
            }

            if (!agent.isOnNavMesh) return;

            if (context.CurrentTime - investigateStimulusTime >= settings.InvestigateTimeout)
            {
                ChangeState(EnemyState.Patrol);
                return;
            }

            if (agent.pathPending) return;

            bool hasArrived =
                !agent.hasPath ||
                agent.remainingDistance <= agent.stoppingDistance + settings.ArrivalDistanceThreshold;

            if (!hasArrived) return;

            // 정해진 지점을 다 보았다면 순찰로 복귀
            if (visitedInvestigatePointCount >= settings.InvestigatePointCount)
            {
                ChangeState(EnemyState.Patrol);
                return;
            }

            // 조사 지점 설정에 실패하면 다음 틱에 다시 시도
            if (TryGetInvestigatePoint(out Vector3 point) && agent.SetDestination(point))
            {
                visitedInvestigatePointCount++;
            }
        }

        /// <summary>
        /// 해당 지점을 조사 지점으로 쓸 수 있는지 판단
        /// 거의 제자리거나, 끊김 없이 갈 수 없다면 지점을 버린다
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private bool IsUsableInvestigatePoint(Vector3 point)
        {
            float arrivalDistance = agent.stoppingDistance + settings.ArrivalDistanceThreshold;
            if ((point - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                return false;
            }

            if (!agent.CalculatePath(point, investigatePath))
            {
                return false;
            }

            return investigatePath.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>
        /// 첫 지점은 중심 근처에서 개체에서 갈라지는 자리
        /// 이후 지점은 반경 안 무작위
        /// </summary>
        private bool TryGetInvestigatePoint(out Vector3 result)
        {
            if (visitedInvestigatePointCount == 0)
            {
                Vector3 fromCenter = transform.position - investigatePosition;
                fromCenter.y = 0f;

                // 이미 구역 안이라면 spread원의 가장자리로 돌아갈 이유 x -> random 재개
                if (fromCenter.sqrMagnitude > investigateRadius * investigateRadius)
                {
                    Vector3 entry = investigatePosition + fromCenter.normalized * investigateRadius;

                    if (NavMesh.SamplePosition(entry, out NavMeshHit entryHit, 2f, NavMesh.AllAreas) &&
                        IsUsableInvestigatePoint(entryHit.position))
                    {
                        result = entryHit.position;
                        return true;
                    }
                }
            }

            Vector2 offset = Random.insideUnitCircle * investigateRadius;
            Vector3 candidate = investigatePosition + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) &&
                IsUsableInvestigatePoint(hit.position))
            {
                result = hit.position;
                return true;
            }

            result = investigatePosition;
            return false;
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
                // 놓친 직후 잠깐은 플레이어를 향해 벽 너머를 투시한다 (8/17)
                // 보이지 않는 동안 목격 시간은 갱신하지 않는다
                if (IsInChaseClairvoyance(context))
                {
                    lastKnownPlayerDirection =
                        (playerTransform.position - lastKnownPlayerPosition).normalized;
                    lastKnownPlayerPosition = playerTransform.position;
                }

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

            // 목격지점을 기반으로 한 위치로 향한다
            Vector3 chaseDestination = GetChaseDestination(context);

            float destinationUpdateThresholdSqr =
                settings.DestinationUpdateThreshold *
                settings.DestinationUpdateThreshold;

            Vector3 toDestination = lastRequestedDestination - chaseDestination;

            // 마지막 목격 위치가 바뀌었다면 목적지를 갱신한다
            if (toDestination.sqrMagnitude >=
                destinationUpdateThresholdSqr)
            {
                bool isDestinationSet =
                    agent.SetDestination(chaseDestination);

                if (isDestinationSet)
                {
                    // 목적지 요청에 성공했을 때만 기록한다
                    lastRequestedDestination =
                        chaseDestination;
                }

                return;
            }

            // 마지막 목격 위치까지의 거리 계산
            Vector3 toChaseDestination = chaseDestination - transform.position;

            // Y축을 제거하기 전에 수직 거리 저장
            float verticalDistance =
                Mathf.Abs(
                    toChaseDestination.y);

            // 수평 거리만 별도로 검사한다
            toChaseDestination.y = 0f;

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
                toChaseDestination.sqrMagnitude <=
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

        /// <summary>
        /// 개체 다수가 플레이어를 쫓을 때
        /// 목표 근처에서 개체마다 다른 값을 돌려준다
        /// 에이전트 반경 (현재 두 브레켄의 몸 둘레(반지름)0.5 + 0.5 = 1m) 때문에
        /// 늦게 온 개체가 도착 판정 거리 이내(0.6m)로 들어가지 않는다
        /// 즉 몬스터의 겹침 문제를 해결한다
        /// </summary>
        // todo 해시 기반이므로 방향이 겹치는 상황이 발생 가능, 차후 필요하다면 수정
        private Vector3 GetSpreadDestination(Vector3 target, float radius)
        {
            if (radius <= 0f) return target;

            int seed = unchecked(gameObject.GetInstanceID() * 397 ^ target.GetHashCode());
            float angle = (seed & 1023) / 1023f * Mathf.PI * 2f;
            float distance = radius * Mathf.Lerp(0.4f, 1f, ((seed >> 10) & 255) / 255f);

            Vector3 candidate = target + new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance);

            // 흩어진 목적지가 navmesh 밖이라면, 목적지를 그대로 사용한다
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return target;
        }

        /// <summary>
        /// 시야 내에 플레이어가 있다면 그대로 플레이어를 쫓고
        /// 시야를 벗어났을 때만 흩어진다
        /// </summary>
        private Vector3 GetChaseDestination(in RuntimeTickContext context)
        {
            if (isPlayerVisible || IsInChaseClairvoyance(context)) return lastKnownPlayerPosition;

            return GetSpreadDestination(lastKnownPlayerPosition, settings.ChaseSpreadRadius);
        }

        /// <summary>
        /// 시야를 잃은 뒤 짧은 시간동안 투시(8/17 기준 2초)
        /// 기본 시야 거리는 유지
        /// </summary>
        private bool IsInChaseClairvoyance(in RuntimeTickContext context)
        {
            if (playerTransform == null) return false;

            if (context.CurrentTime - lastKnownPlayerSightingTime >= settings.ChaseClairvoyanceDuration)
                return false;

            Vector3 toPlayer = playerTransform.position - transform.position;
            return toPlayer.sqrMagnitude <= settings.SightDistance * settings.SightDistance;
        }



        private void TickAttack(in RuntimeTickContext context)
        {
            if (!IsPlayerInAttackRange() || !(isPlayerVisible || IsInChaseClairvoyance(context)))
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            // 공격 중에는 항상 플레이어 방향으로 몸을 돌린다
            FaceTarget(playerTransform.position, context.DeltaTime);

            // 공격이 끝난 뒤, 추격이 이어지도록 목격 정보 갱신
            if (isPlayerVisible)
            {
                lastKnownPlayerDirection =
                    (playerTransform.position - lastKnownPlayerPosition).normalized;
                lastKnownPlayerPosition = playerTransform.position;
                lastKnownPlayerSightingTime = context.CurrentTime;
                alertness = settings.AlertMax;
            }

            else if (IsInChaseClairvoyance(context))
            {
                lastKnownPlayerDirection =
                    (playerTransform.position - lastKnownPlayerPosition).normalized;
                lastKnownPlayerPosition = playerTransform.position;
            }

            // 쿨다운이 지났고, 실제로 보이고, 몸이 정면을 향했다면 Attack
            if (context.CurrentTime - lastAttackTime >= settings.AttackCooldown &&
                isPlayerVisible &&
                IsFacingPlayer())
            {
                Attack();
                lastAttackTime = context.CurrentTime;
            }
        }

        /// <summary>
        /// 공격 사거리 판정
        /// </summary>
        private bool IsPlayerInAttackRange()
        {
            if (playerTransform == null) return false;

            Vector3 toPlayer = playerTransform.position - transform.position;
            float verticalDistance = Mathf.Abs(toPlayer.y);
            toPlayer.y = 0f;


            return toPlayer.sqrMagnitude <=
                settings.AttackRange * settings.AttackRange &&
                verticalDistance <= 1.0f;
        }

        /// <summary>
        /// 몸통이 플레이어를 향하고 있는지 판정
        /// 다 돌았다고 판정되기 전까지는 때리지 않는다
        /// </summary>
        private bool IsFacingPlayer()
        {
            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.01f) return true;

            return Vector3.Angle(transform.forward, toPlayer) <= settings.AttackHalfAngle;
        }

        /// <summary>
        /// 실제 공격
        /// </summary>
        private void Attack()
        {
            // 쿨다운 간격으로만 호출
            IDamageable target = playerTransform.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) return;

            Vector3 hitPoint =
                playerTransform.position + Vector3.up * (PlayerHeight * 0.5f);
            Vector3 direction = playerTransform.position - transform.position;

            target.ApplyDamage(new DamageData(
                settings.AttackDamage,
                DamageKind.Monster,
                DamageHitZone.Body,
                DamageFlags.CanCauseBleeding,
                hitPoint,
                direction,
                gameObject
            ));
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

        // 튜닝용 상태 표시, 머리 위에 현재 상태와 포기까지 남은 시간을 띄운다
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (headTransform == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // 가까운 개체만 그린다, 전부 그리면 화면이 라벨로 덮인다
            if ((headTransform.position - cam.transform.position).sqrMagnitude > 40f * 40f) return;

            Vector3 screen = cam.WorldToScreenPoint(headTransform.position + Vector3.up * 0.3f);
            if (screen.z <= 0f) return;

            string text = currentState.ToString();

            // 추격을 포기하기까지 남은 시간
            if (currentState == EnemyState.Search)
            {
                text += "  " + (alertness / settings.SearchAlertDecreaseSpeed).ToString("F1") + "s";
            }
            else if (currentState == EnemyState.Chase && !isPlayerVisible)
            {
                float left = settings.StuckDuration - (Time.time - lastKnownPlayerSightingTime);
                text += "  " + Mathf.Max(0f, left).ToString("F1") + "s";
            }

            GUI.color = Color.yellow;
            GUI.Label(
                new Rect(screen.x - 50f, Screen.height - screen.y - 20f, 100f, 20f),
                text);
            GUI.color = Color.white;
        }
#endif
    }
}
