using UnityEngine;

public class InventoryGridBuilder : MonoBehaviour
{
    [SerializeField] private GameObject itemCellPrefab;
    [SerializeField] private Transform gridParent;
    [SerializeField] private int slotCount = 32;

    private void Start()
    {
        for (int i = 0; i < slotCount; i++)
        {
            Instantiate(itemCellPrefab, gridParent);
        }
    }
}