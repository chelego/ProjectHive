using UnityEngine;
// NavMeshAgent, NavMesh
using UnityEngine.AI;
// IRuntimeTickable, RuntimeTickContext, RuntimeCoordinator
using ProjectHive.Core.Runtime;
// GameEventBus
using ProjectHive.Core.Events;

namespace ProjectHive.AI.Mob
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class BreckenAI : MonoBehaviour, IRuntimeTickable
    {
        private enum EnemyState
        {
            Idle, Patrol, Alert, Investigate, Chase, Attack, Search
        }

        private NavMeshAgent agent;
        
        [Header("Tick")]
        [SerializeField, Min(0.02f)] private float tickInterval = 0.05f;

        [Header("Player Perception")]
        [SerializeField] private Transform playerTransform;
        [SerializeField, Min(0f)] private float sightRange = 15f;
        [SerializeField, Range(0f, 180f)] private float sightAngle = 110f;
        [SerializeField, Range(0f, 90f)] private float sightAngleUp = 30f;
        [SerializeField, Range(0f, 90f)] private float sightAngleDown = 45f;
        [SerializeField] private LayerMask sightBlockerMask;
        [SerializeField, Min(0f)] private float eyeHeight = 2f;
        [SerializeField] private bool isPlayerVisible;

        [Header("Enemy State")] 
        [SerializeField] private EnemyState currentState = EnemyState.Idle;

        [Header("State - Alert")] 
        [SerializeField, Min(0f)] private float alertDuration = 1f;

        [Header("State - Patrol")]
        [SerializeField, Min(1f)] private float patrolRadius = 10f;

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
            patrolOriginPosition = transform.position; // 순찰 기준점
        }

        public void RuntimeTick(in RuntimeTickContext context)
        {
            isPlayerVisible = CanSeePlayer();

            switch (currentState)
            {
                case EnemyState.Idle:        TickIdle(context);        break;
                case EnemyState.Patrol:      TickPatrol(context);      break;
                case EnemyState.Alert:       TickAlert(context);       break;
                case EnemyState.Investigate: TickInvestigate(context); break;
                case EnemyState.Chase:       TickChase(context);       break;
                case EnemyState.Attack:      TickAttack(context);      break;
                case EnemyState.Search:      TickSearch(context);      break;
            }
        }

        void OnEnable()
        {
            ResolveServices();

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

            // 아직 서로 발 밑 기준으로 계산
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
            
            // 장애물 판정
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

            // Alert 상태로 전이하는 경우, 회전은 FaceTarget()이 맡음
            // 복귀하는 경우, NavMesh의 자동 회전이 맡음
            switch (next)
            {
                case EnemyState.Alert:
                    agent.updateRotation = false;
                    if (agent.isOnNavMesh) agent.ResetPath();
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

            // 목적지에 도달하지 못할 수 있으므로 0.1만큼 여유
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
        
        private void TickInvestigate(in RuntimeTickContext context) { }
        private void TickChase(in RuntimeTickContext context) { }
        private void TickAttack(in RuntimeTickContext context) { }
        private void TickSearch(in RuntimeTickContext context) { }



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
        
        
    }
}
    

