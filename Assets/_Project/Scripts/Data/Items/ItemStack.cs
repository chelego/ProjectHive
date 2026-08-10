using System;
using UnityEngine;

namespace ProjectHive.Data.Items
{
    [Serializable]
    public struct ItemStack
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField, Min(0)] private int amount;

        public ItemStack(ItemDefinition itemDefinition, int itemAmount)
        {
            definition = itemDefinition;
            amount = Mathf.Max(0, itemAmount);
        }

        public ItemDefinition Definition => definition;
        public int Amount => amount;
        public bool IsEmpty => definition == null || amount <= 0;

        public bool CanMergeWith(in ItemStack other)
        {
            return !IsEmpty &&
                   !other.IsEmpty &&
                   definition == other.definition &&
                   amount < definition.MaximumStack;
        }
    }
}
