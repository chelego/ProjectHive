namespace ProjectHive.AI.Hive
{
    public interface IHiveDecisionPolicy
    {
        bool TryCreateCommand(
            HiveBlackboard blackboard,
            float currentTime,
            int commandSequence,
            out HiveCommand command);
    }
}
