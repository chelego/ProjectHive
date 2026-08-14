using UnityEngine;

namespace ProjectHive.AI.Mob
{

    [CreateAssetMenu(fileName = "mobAI", menuName = "Project Hive/MobAI/Mob AI Settings")]
    public class MobAISettings : ScriptableObject
    {

        [Header("Navi")] 
        [SerializeField, Min(0f)] private float arrivalDistanceThreshold = 0.6f;
        
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
        [SerializeField, Min(0f)] private float alertMax = 3f;
        [SerializeField, Min(0f)] private float alertDecreaseSpeed = 1f;
        [SerializeField, Min(0f)] private float alertIncreaseSpeed = 3f;
        

        
        [Header("Chase")] 
        // player가 마지막 갱신 위치로부터 destinationUpdateThreshold만큼 움직인 경우에만 경로 재계산
        [SerializeField, Min(0f)] private float destinationUpdateThreshold = 0.5f;
        // player가 시야 밖으로 벗어난 이후, chase 상태 유지 시간
        [SerializeField, Min(0f)] private float stuckDuration = 10f;
        [SerializeField, Min(0f)] private float chaseSpeed = 8f;

        
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
        
        
        // Tick Interval
        public float RequestPathTickInterval => requestPathTickInterval;
        public float JudgeTickInterval => judgeTickInterval;
        public float PerceptionTickInterval => perceptionTickInterval;
    }
}