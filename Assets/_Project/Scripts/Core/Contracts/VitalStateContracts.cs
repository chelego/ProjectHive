using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public enum VitalChangeReason
    {
        Initialized = 0,
        Damage = 1,
        Healing = 2,
        BleedingTick = 3,
        BleedingChanged = 4,
        Reset = 5
    }

    [Serializable]
    public readonly struct VitalStateSnapshot
    {
        public VitalStateSnapshot(float currentHealth, float maximumHealth, int bleedingStacks)
        {
            MaximumHealth = Mathf.Max(0f, maximumHealth);
            CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaximumHealth);
            BleedingStacks = Mathf.Clamp(bleedingStacks, 0, 5);
        }

        public float CurrentHealth { get; }
        public float MaximumHealth { get; }
        public int BleedingStacks { get; }
        public bool IsDead => CurrentHealth <= 0f;
        public float NormalizedHealth => MaximumHealth > 0.0001f
            ? CurrentHealth / MaximumHealth
            : 0f;
    }

    [Serializable]
    public readonly struct VitalStateChange
    {
        public VitalStateChange(
            in VitalStateSnapshot previous,
            in VitalStateSnapshot current,
            VitalChangeReason reason)
        {
            Previous = previous;
            Current = current;
            Reason = reason;
        }

        public VitalStateSnapshot Previous { get; }
        public VitalStateSnapshot Current { get; }
        public VitalChangeReason Reason { get; }
    }

    public interface IVitalStateProvider
    {
        VitalStateSnapshot VitalState { get; }
        event Action<VitalStateChange> VitalStateChanged;
    }

    public enum MedicalTreatmentKind
    {
        Bandage = 0,
        Disinfectant = 1,
        Injector = 2
    }

    [Serializable]
    public readonly struct MedicalTreatmentRequest
    {
        public MedicalTreatmentRequest(
            MedicalTreatmentKind kind,
            float healingAmount,
            int bleedingStacksToRemove,
            GameObject source)
        {
            Kind = kind;
            HealingAmount = Mathf.Max(0f, healingAmount);
            BleedingStacksToRemove = Mathf.Max(0, bleedingStacksToRemove);
            Source = source;
        }

        public MedicalTreatmentKind Kind { get; }
        public float HealingAmount { get; }
        public int BleedingStacksToRemove { get; }
        public GameObject Source { get; }
    }

    public enum MedicalTreatmentResult
    {
        Applied = 0,
        InvalidTarget = 1,
        TargetDead = 2,
        NoApplicableEffect = 3,
        Interrupted = 4
    }

    public interface IMedicalTreatmentTarget
    {
        MedicalTreatmentResult TryApplyTreatment(in MedicalTreatmentRequest request);
    }
}
