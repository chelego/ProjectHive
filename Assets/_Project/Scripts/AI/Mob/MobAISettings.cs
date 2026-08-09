using UnityEngine;

namespace ProjectHive.AI.Mob
{

    [CreateAssetMenu(fileName = "mobAI", menuName = "Project Hive/MobAI/Mob AI Settings")]
    public class MobAISettings : ScriptableObject
    {


        [Header("Sight")]
        // 시야
        [SerializeField, Range(0f, 180f)] private float bothSideSightsAngle = 110f;
        [SerializeField, Range(0f, 90f)] private float upSightAngle = 30f;
        [SerializeField, Range(0f, 90f)] private float downSightAngle = 45f;
        
        // 목꺾임 정도
        [SerializeField, Range(0f, 100f)] private float horizontalHeadTurnAngle = 90f;
        [SerializeField, Range(0f, 90f)] private float verticalHeadTurnAngle = 60f;

        // 시야 최대 거리
        [SerializeField, Min(0f)] private float sightDistance = 15f;
        [SerializeField] private LayerMask sightBlockMask;

        [SerializeField, Min(0f)] private float eyeHeight = 2f;
        [SerializeField, Min(0f)] private float headTurnSpeed = 120f;

        [SerializeField, Min(0f)] private float bodyTurnSpeed = 90f;

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
        [SerializeField, Min(0f)] private float chaseSpeed = 6f;
        [SerializeField, Min(0f)] private float chaseAlertDecreaseSpeed = 0.3f;

        
        [Header("Tick Interval")] 
        [SerializeField, Min(0.01f)] private float requestPathTickInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float judgeTickInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float perceptionTickInterval = 0.1f;
        
        
        // ==========================================
        // Properties (Getter)
        // ==========================================
        
        // Sight
        public float BothSideSightsAngle => bothSideSightsAngle;
        public float UpSightAngle => upSightAngle;
        public float DownSightAngle => downSightAngle;
        
        public float HorizontalHeadTurnAngle => horizontalHeadTurnAngle;
        public float VerticalHeadTurnAngle => verticalHeadTurnAngle;
        
        public float SightDistance => sightDistance;
        public LayerMask SightBlockMask => sightBlockMask;
        
        public float EyeHeight => eyeHeight;
        public float HeadTurnSpeed => headTurnSpeed;
        public float BodyTurnSpeed => bodyTurnSpeed;
        
        
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
        public float ChaseAlertDecreaseSpeed => chaseAlertDecreaseSpeed;
        
        
        // Tick Interval
        public float RequestPathTickInterval => requestPathTickInterval;
        public float JudgeTickInterval => judgeTickInterval;
        public float PerceptionTickInterval => perceptionTickInterval;
    }
}