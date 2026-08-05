using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public enum PlayerLocomotionState
    {
        Grounded = 0,
        Sprint = 1,
        Crouch = 2,
        Slide = 3,
        Airborne = 4,
        Vault = 5,
        Mantle = 6
    }

    [Serializable]
    public readonly struct LocomotionStateChange
    {
        public LocomotionStateChange(
            PlayerLocomotionState previous,
            PlayerLocomotionState current)
        {
            Previous = previous;
            Current = current;
        }

        public PlayerLocomotionState Previous { get; }
        public PlayerLocomotionState Current { get; }
    }

    public interface ILocomotionStateProvider
    {
        PlayerLocomotionState LocomotionState { get; }
        event Action<LocomotionStateChange> LocomotionStateChanged;
    }

    [Serializable]
    public readonly struct StaminaStateSnapshot
    {
        public StaminaStateSnapshot(
            float currentStamina,
            float maximumStamina,
            bool isConsuming)
        {
            MaximumStamina = Mathf.Max(0f, maximumStamina);
            CurrentStamina = Mathf.Clamp(currentStamina, 0f, MaximumStamina);
            IsConsuming = isConsuming;
        }

        public float CurrentStamina { get; }
        public float MaximumStamina { get; }
        public bool IsConsuming { get; }
        public float NormalizedStamina => MaximumStamina > 0.0001f
            ? CurrentStamina / MaximumStamina
            : 0f;
        public bool IsFull => CurrentStamina >= MaximumStamina;
    }

    public interface IStaminaStateProvider
    {
        StaminaStateSnapshot StaminaState { get; }
        event Action<StaminaStateSnapshot> StaminaStateChanged;
    }
}
