namespace ProjectHive.Core.Contracts
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void ApplyDamage(in DamageData damage);
    }
}
