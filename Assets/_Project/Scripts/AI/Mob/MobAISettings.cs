using UnityEngine;

namespace ProjectHive.AI.Mob
{

    [CreateAssetMenu(fileName = "mobAI", menuName = "Project Hive/MobAI/Mob AI Settings")]
    public class MobAISettings : ScriptableObject
    {
        [Header("Navi")] 
        [SerializeField, Min(0f)] private float arrivalDistanceThreshold = 1.2f;
        
        [Header("Sight")]
        // 시야
        [SerializeField, Range(0f, 180f)] private float bothSideSightsAngle = 110f;
        [SerializeField, Range(0f, 90f)] private float upSightAngle = 30f;
        [SerializeField, Range(0f, 90f)] private float downSightAngle = 45f;
        // 근접 시 벌어지는 좌우 시야각
        [SerializeField, Range(0f, 220f)] private float closeSightAngle = 180f;
        
        // 근접 시 벌어지는 상하 시야
        [SerializeField, Range(0f, 90f)] private float closeUpSightAngle = 60f;
        [SerializeField, Range(0f, 90f)] private float closeDownSightAngle = 70f;
        // 아래 두 줄은 이후 브레켄이 너무 예민하다면, 
        // 최대 거리를 줄여서 시야각이 넓어지는 폭을 줄여야 한다
        [SerializeField, Min(0f)] private float sightBoostNearDistance = 2f; // 이 거리 안이면 최대
        [SerializeField, Min(0f)] private float sightBoostFarDistance = 5f;  // 이 거리 밖이면 기본
        
        // 목꺾임 정도
        [SerializeField, Range(0f, 100f)] private float horizontalHeadTurnAngle = 90f;
        [SerializeField, Range(0f, 90f)] private float verticalHeadTurnAngle = 60f;

        // 시야 최대 거리
        [SerializeField, Min(0f)] private float sightDistance = 15f;
        [SerializeField] private LayerMask sightBlockMask;

        [SerializeField, Min(0f)] private float eyeHeight = 2f;
        [SerializeField, Min(0f)] private float headTurnSpeed = 120f;

        [SerializeField, Min(0f)] private float bodyTurnSpeed = 90f;
        [SerializeField, Min(0f)] private float closeBodyTurnSpeed = 220f; // 근접 시 브레켄 몸통 회전 각속도
        [SerializeField, Min(0f)] private float turnBoostNearDistance = 2f; // 이 거리 안이면 최대
        [SerializeField, Min(0f)] private float turnBoostFarDistance = 5f;
        
        
        [Header("Player")]

        [Header("Patrol")] 
        [SerializeField, Min(0f)] private float patrolRadius = 15f;
        [SerializeField, Min(0f)] private float patrolSpeed = 3f;
        [SerializeField, Min(0f)] private float waitingTimeAfterPatrolPoint = 3f; // 순찰 지점에 도착한 이후, 머무는 시간
        [SerializeField, Min(0f)] private float patrolHeadSweepAngle = 180f;
        [SerializeField, Min(0f)] private float patrolHeadSweepSpeed = 90f;
        [SerializeField, Min(0f)] private float bodyFollowRatio = 0.2f;
        

        [Header("Alert")] 
        [SerializeField, Min(0f)] private float alertMax = 10f;
        [SerializeField, Min(0f)] private float alertDecreaseSpeed = 1f;
        [SerializeField, Min(0f)] private float alertIncreaseSpeed = 20f;
        

        
        [Header("Chase")] 
        // player가 마지막 갱신 위치로부터 destinationUpdateThreshold만큼 움직인 경우에만 경로 재계산
        [SerializeField, Min(0f)] private float destinationUpdateThreshold = 0.5f;
        // player가 시야 밖으로 벗어난 이후, chase 상태 유지 시간
        [SerializeField, Min(0f)] private float stuckDuration = 10f;
        [SerializeField, Min(0f)] private float chaseSpeed = 8f;
        // 다수의 개체가 추격 시, 하나의 목격 지점으로 몰리는 현상을 방지하기 위해 흩어지는 반경
        [SerializeField, Min(0f)] private float chaseSpreadRadius = 2f;
        // 시야를 잃은 뒤 몹이 플레이어를 투시하는 시간
        [SerializeField, Min(0f)] private float chaseClairvoyanceDuration = 2f;

        [Header("Search")]
        // Search 상태 중 경계도가 깎이는 속도, patrol - alertDecreaseSpeed와 별개
        [SerializeField, Min(0f)] private float searchAlertDecreaseSpeed = 0.5f; 
        // 플레이어가 이 속도로 계속 이동했다고 가정하고 탐색 반경을 넓힌다
        [SerializeField, Min(0f)] private float assumedPlayerSpeed = 7f;
        // 플레이어가 얼마나 직선으로 도망친다고 볼 것인가. 1이면 전력 직선 질주 가정
        // 이 비율에 따라 Search 반경(부채꼴)을 넓히는 속도가 높아지거나 낮아진다
        [SerializeField, Range(0.01f, 1f)] private float fleeDirectness = 0.6f;
        // 탐색 반경 상한. alertness와 브레켄과 플레이어의 이동속도를 고려한 Search 범위
        [SerializeField, Min(0f)] private float maxSearchRadius = 25f;
        // 놓친 직후 부채꼴 반각
        [SerializeField, Range(0f, 180f)] private float searchStartHalfAngle = 45f;
        // 부채꼴이 최대로 벌어지는 반각
        [SerializeField, Range(0f, 180f)] private float searchEndHalfAngle = 135f;


        [Header("Investigate")]
        [SerializeField, Min(0f)] private float investigateSpeed = 6f;
        // 도착 후 반경 내에서 훑어볼 지점 수
        [SerializeField, Min(1f)] private int investigatePointCount = 3;
        // 자극이 발생한 뒤, 조사하는 시간. 이 시간이 지나면 patrol로 복귀한다
        [SerializeField, Min(0f)] private float investigateTimeout = 20f;
        // 직접 들은 소리의 탐색 반경, 최소 3, 최대 12m(하이브 연동 값)
        [SerializeField, Min(0f)] private float investigateMinRadius = 3f;
        [SerializeField, Min(0f)] private float investigateMaxRadius = 12f;
        // 이 필드의 거리 안의 새로운 자극은 같은 사건으로 보고 다시 출발하지 않는다
        [SerializeField, Min(0f)] private float investigateMergeDistance = 5f;


        [Header("Attack")]
        // 이 거리 안으로 들어오면 즉각적으로 chase -> attack으로 전이
        [SerializeField, Min(0f)] private float attackRange = 2f;
        [SerializeField, Min(0f)] private float attackDamage = 20f;
        // 최소 타격 간격 - 공격 속도
        [SerializeField, Min(0.01f)] private float attackCooldown = 1f;
        // 몸통 전면부가 이 각도 내에 들어와야 attack
        [SerializeField, Range(0f, 180f)] private float attackHalfAngle = 45f;
        // 공격 중 이동속도 감쇠 비율
        [SerializeField, Range(0f, 1f)] private float attackSpeedMultiplier = 0.7f;


        [Header("Report")]
        // 하이브에게 목격 정보를 보고하는 간격
        [SerializeField, Min(0.1f)] private float visualContactReportInterval = 2f;

        
        [Header("Tick Interval")] 
        [SerializeField, Min(0.01f)] private float requestPathTickInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float judgeTickInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float perceptionTickInterval = 0.1f;
        
        
        // ==========================================
        // Properties (Getter)
        // ==========================================
        
        // Navi
        public float ArrivalDistanceThreshold => arrivalDistanceThreshold;
        
        // Sight
        public float BothSideSightsAngle => bothSideSightsAngle;
        public float UpSightAngle => upSightAngle;
        public float DownSightAngle => downSightAngle;
        public float CloseSightAngle => closeSightAngle;
        public float CloseUpSightAngle => closeUpSightAngle;
        public float CloseDownSightAngle => closeDownSightAngle;
        public float SightBoostNearDistance => sightBoostNearDistance;
        public float SightBoostFarDistance => sightBoostFarDistance;
        
        public float HorizontalHeadTurnAngle => horizontalHeadTurnAngle;
        public float VerticalHeadTurnAngle => verticalHeadTurnAngle;
        
        public float SightDistance => sightDistance;
        public LayerMask SightBlockMask => sightBlockMask;
        
        public float EyeHeight => eyeHeight;
        public float HeadTurnSpeed => headTurnSpeed;
        public float BodyTurnSpeed => bodyTurnSpeed;
        public float CloseBodyTurnSpeed => closeBodyTurnSpeed;
        public float TurnBoostNearDistance => turnBoostNearDistance;
        public float TurnBoostFarDistance => turnBoostFarDistance;
        
        
        // Patrol
        public float PatrolRadius => patrolRadius;
        public float PatrolSpeed => patrolSpeed;
        public float WaitingTimeAfterPatrolPoint => waitingTimeAfterPatrolPoint;
        public float PatrolHeadSweepAngle => patrolHeadSweepAngle;
        public float PatrolHeadSweepSpeed => patrolHeadSweepSpeed;
        public float BodyFollowRatio => bodyFollowRatio;
        
        
        // Alert
        public float AlertMax => alertMax;
        public float AlertDecreaseSpeed => alertDecreaseSpeed;
        public float AlertIncreaseSpeed => alertIncreaseSpeed;
        
        
        // Chase
        public float DestinationUpdateThreshold => destinationUpdateThreshold;
        public float StuckDuration => stuckDuration;
        public float ChaseSpeed => chaseSpeed;
        public float ChaseSpreadRadius => chaseSpreadRadius;
        public float ChaseClairvoyanceDuration => chaseClairvoyanceDuration;
        
        
        // Search
        public float SearchAlertDecreaseSpeed => searchAlertDecreaseSpeed;
        public float AssumedPlayerSpeed => assumedPlayerSpeed;
        public float FleeDirectness => fleeDirectness;
        public float MaxSearchRadius => maxSearchRadius;
        public float SearchStartHalfAngle => searchStartHalfAngle;
        public float SearchEndHalfAngle => searchEndHalfAngle;
        
        
        // Investigate
        public float InvestigateSpeed => investigateSpeed;
        public int InvestigatePointCount => investigatePointCount;
        public float InvestigateTimeout => investigateTimeout;
        public float InvestigateMinRadius => investigateMinRadius;
        public float InvestigateMaxRadius => investigateMaxRadius;
        public float InvestigateMergeDistance => investigateMergeDistance;

        // Attack
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public float AttackHalfAngle => attackHalfAngle;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;

        // Report
        public float VisualContactReportInterval => visualContactReportInterval;


        // Tick Interval
        public float RequestPathTickInterval => requestPathTickInterval;
        public float JudgeTickInterval => judgeTickInterval;
        public float PerceptionTickInterval => perceptionTickInterval;
    }
}
