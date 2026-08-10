namespace ProjectHive.Data.Items
{
    public enum EquipmentSlotType
    {
        Firearm = 0,
        Melee = 1,
        Armor = 2,
        Bag = 3,
        SafePocket = 4,
        QuickUse = 5
    }

    public enum InventoryOperationResult
    {
        Success = 0,
        InvalidItem = 1,
        InvalidSlot = 2,
        SlotOccupied = 3,
        CapacityReached = 4,
        StackLimitReached = 5,
        Restricted = 6,
        ContainerNotFound = 7,
        SourceEmpty = 8,
        InsufficientAmount = 9,
        ItemNotAllowed = 10,
        SameSlot = 11
    }
}
