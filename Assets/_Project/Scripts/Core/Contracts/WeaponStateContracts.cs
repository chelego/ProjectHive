using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public enum EquippedWeaponSlot
    {
        None = 0,
        Firearm = 1,
        Melee = 2
    }

    public enum WeaponActionState
    {
        Holstered = 0,
        Ready = 1,
        Attacking = 2,
        Reloading = 3,
        UsingUtility = 4,
        Blocked = 5
    }

    public enum FirearmFeedType
    {
        None = 0,
        DetachableMagazine = 1,
        Revolver = 2
    }

    [Serializable]
    public readonly struct WeaponStateSnapshot
    {
        public WeaponStateSnapshot(
            string weaponItemId,
            EquippedWeaponSlot equippedSlot,
            WeaponActionState actionState,
            FirearmFeedType feedType,
            int magazineRounds,
            int magazineCapacity,
            int chamberedRounds,
            int reserveRounds)
        {
            WeaponItemId = weaponItemId ?? string.Empty;
            EquippedSlot = equippedSlot;
            ActionState = actionState;
            FeedType = feedType;
            MagazineCapacity = Mathf.Max(0, magazineCapacity);
            MagazineRounds = Mathf.Clamp(magazineRounds, 0, MagazineCapacity);
            ChamberedRounds = Mathf.Max(0, chamberedRounds);
            ReserveRounds = Mathf.Max(0, reserveRounds);
        }

        public string WeaponItemId { get; }
        public EquippedWeaponSlot EquippedSlot { get; }
        public WeaponActionState ActionState { get; }
        public FirearmFeedType FeedType { get; }
        public int MagazineRounds { get; }
        public int MagazineCapacity { get; }
        public int ChamberedRounds { get; }
        public int ReserveRounds { get; }
        public int TotalReadyRounds => MagazineRounds + ChamberedRounds;
        public bool HasWeapon => EquippedSlot != EquippedWeaponSlot.None &&
                                 !string.IsNullOrEmpty(WeaponItemId);
    }

    public interface IWeaponStateProvider
    {
        WeaponStateSnapshot WeaponState { get; }
        event Action<WeaponStateSnapshot> WeaponStateChanged;
    }
}
