using UnityEngine;

namespace ProjectHive.Data.Items
{
    public enum ItemCategory
    {
        Miscellaneous = 0,
        Weapon = 1,
        Ammunition = 2,
        Medical = 3,
        Utility = 4,
        Armor = 5,
        Valuable = 6,
        Quest = 7
    }

    [CreateAssetMenu(fileName = "Item_", menuName = "Project Hive/Data/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "item.unassigned";
        [SerializeField] private string displayName = "Unnamed Item";
        [SerializeField] private ItemCategory category;
        [SerializeField, Min(0f)] private float weight;
        [SerializeField, Min(0)] private int baseValue;
        [SerializeField, Min(1)] private int maximumStack = 1;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject worldPrefab;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ItemCategory Category => category;
        public float Weight => weight;
        public int BaseValue => baseValue;
        public int MaximumStack => maximumStack;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;

        private void OnValidate()
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? "item.unassigned" : itemId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
            maximumStack = Mathf.Max(1, maximumStack);
            weight = Mathf.Max(0f, weight);
            baseValue = Mathf.Max(0, baseValue);
        }
    }
}
