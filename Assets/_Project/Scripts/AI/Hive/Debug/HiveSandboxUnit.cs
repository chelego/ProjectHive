using ProjectHive.Core.Runtime;
using UnityEngine;

namespace ProjectHive.AI.Hive.Debugging
{
    [DisallowMultipleComponent]
    public sealed class HiveSandboxUnit :
        MonoBehaviour,
        IHiveControllable,
        IRuntimeTickable
    {
        [SerializeField] private HiveUnitRegistry registry;
        [SerializeField] private RuntimeCoordinator runtimeCoordinator;
        [SerializeField] private HiveUnitRole role = HiveUnitRole.Hunter;
        [SerializeField] private HiveUnitCapabilities capabilities =
            HiveUnitCapabilities.GroundMovement |
            HiveUnitCapabilities.Attack |
            HiveUnitCapabilities.Investigate |
            HiveUnitCapabilities.Guard;
        [SerializeField, Min(0.1f)] private float movementSpeed = 4f;
        [SerializeField] private bool available = true;

        private HiveCommand currentCommand;
        private Vector3 assignedTarget;
        private bool hasCommand;

        public bool RuntimeTickEnabled => isActiveAndEnabled && available && hasCommand;
        public Transform RuntimeTransform => transform;
        public float MinimumTickInterval => 0.05f;
        public bool UseDistanceScaling => false;
        public int AcceptedCommandCount { get; private set; }

        public void Configure(
            HiveUnitRegistry targetRegistry,
            RuntimeCoordinator coordinator,
            HiveUnitRole unitRole,
            HiveUnitCapabilities unitCapabilities,
            float speed)
        {
            registry = targetRegistry;
            runtimeCoordinator = coordinator;
            role = unitRole;
            capabilities = unitCapabilities;
            movementSpeed = Mathf.Max(0.1f, speed);
        }

        private void OnEnable()
        {
            ResolveServices();
            registry?.Register(this);
            runtimeCoordinator?.Register(this);
        }

        private void OnDisable()
        {
            registry?.Unregister(this);
            runtimeCoordinator?.Unregister(this);
        }

        public HiveUnitSnapshot GetHiveUnitSnapshot()
        {
            return new HiveUnitSnapshot(
                gameObject.GetInstanceID(),
                transform.position,
                role,
                capabilities,
                movementSpeed,
                available,
                hasCommand ? currentCommand.Sequence : 0);
        }

        public bool TryAcceptHiveCommand(in HiveCommand command)
        {
            if (!available ||
                command.Kind == HiveCommandKind.None ||
                command.IsExpired(Time.time))
            {
                return false;
            }

            currentCommand = command;
            assignedTarget = GetDeterministicTarget(in command);
            hasCommand = true;
            AcceptedCommandCount++;
            return true;
        }

        public void RuntimeTick(in RuntimeTickContext context)
        {
            if (!hasCommand)
                return;

            if (currentCommand.IsExpired(context.CurrentTime))
            {
                hasCommand = false;
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                assignedTarget,
                movementSpeed * context.DeltaTime);
        }

        private Vector3 GetDeterministicTarget(in HiveCommand command)
        {
            int seed = unchecked(
                gameObject.GetInstanceID() * 397 ^ command.Sequence * 31);
            float angle = (seed & 1023) / 1023f * Mathf.PI * 2f;
            float radiusScale = ((seed >> 10) & 255) / 255f;
            float radius = command.Radius * Mathf.Lerp(0.15f, 0.65f, radiusScale);
            return command.TargetPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
        }

        private void ResolveServices()
        {
            if (registry == null)
                registry = FindFirstObjectByType<HiveUnitRegistry>();
            if (runtimeCoordinator == null)
                runtimeCoordinator = RuntimeCoordinator.Instance;
        }
    }
}
