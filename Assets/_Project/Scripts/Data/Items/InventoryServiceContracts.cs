using System;
using System.Collections.Generic;

namespace ProjectHive.Data.Items
{
    public enum InventoryContainerKind
    {
        Player = 0,
        Backpack = 1,
        Stash = 2,
        SafePocket = 3,
        Loot = 4,
        Merchant = 5,
        CraftingInput = 6
    }

    [Serializable]
    public readonly struct InventorySlotReference
    {
        public InventorySlotReference(string containerId, int slotIndex)
        {
            ContainerId = containerId ?? string.Empty;
            SlotIndex = slotIndex;
        }

        public string ContainerId { get; }
        public int SlotIndex { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ContainerId) && SlotIndex >= 0;
    }

    [Serializable]
    public readonly struct InventoryTransferRequest
    {
        public InventoryTransferRequest(
            in InventorySlotReference source,
            in InventorySlotReference destination,
            int amount)
        {
            Source = source;
            Destination = destination;
            Amount = Math.Max(1, amount);
        }

        public InventorySlotReference Source { get; }
        public InventorySlotReference Destination { get; }
        public int Amount { get; }
    }

    public enum InventoryChangeKind
    {
        Added = 0,
        Removed = 1,
        Moved = 2,
        Swapped = 3,
        Stacked = 4,
        Equipped = 5,
        Unequipped = 6,
        Dropped = 7
    }

    [Serializable]
    public readonly struct InventoryChange
    {
        public InventoryChange(
            InventoryChangeKind kind,
            InventoryOperationResult result,
            in InventorySlotReference source,
            in InventorySlotReference destination,
            in ItemStack item)
        {
            Kind = kind;
            Result = result;
            Source = source;
            Destination = destination;
            Item = item;
        }

        public InventoryChangeKind Kind { get; }
        public InventoryOperationResult Result { get; }
        public InventorySlotReference Source { get; }
        public InventorySlotReference Destination { get; }
        public ItemStack Item { get; }
    }

    public interface IInventoryContainer
    {
        string ContainerId { get; }
        InventoryContainerKind Kind { get; }
        int SlotCount { get; }
        int Version { get; }
        IReadOnlyList<ItemStack> Slots { get; }
    }

    public interface IInventoryService
    {
        event Action<InventoryChange> InventoryChanged;

        bool TryGetContainer(string containerId, out IInventoryContainer container);
        InventoryOperationResult TryTransfer(in InventoryTransferRequest request);
        InventoryOperationResult TryExtract(
            in InventorySlotReference source,
            int amount,
            out ItemStack extracted);
        InventoryOperationResult TryInsert(
            string containerId,
            in ItemStack item,
            int preferredSlotIndex,
            out ItemStack remainder);
    }

    [Serializable]
    public readonly struct EquipmentSlotSnapshot
    {
        public EquipmentSlotSnapshot(EquipmentSlotType slotType, in ItemStack item)
        {
            SlotType = slotType;
            Item = item;
        }

        public EquipmentSlotType SlotType { get; }
        public ItemStack Item { get; }
        public bool IsEmpty => Item.IsEmpty;
    }

    public interface IEquipmentService
    {
        IReadOnlyList<EquipmentSlotSnapshot> Equipment { get; }
        event Action<EquipmentSlotSnapshot> EquipmentChanged;

        InventoryOperationResult TryEquip(
            in InventorySlotReference source,
            EquipmentSlotType destinationSlot);
        InventoryOperationResult TryUnequip(
            EquipmentSlotType sourceSlot,
            string destinationContainerId);
    }
}
