using UnityEngine;
using UnityEngine.EventSystems;

public class ItemCellClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ItemData itemData;

    public void SetItem(ItemData data)
    {
        itemData = data;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null) return;
        ItemDetailPopup.Instance.Show(itemData);
    }
}