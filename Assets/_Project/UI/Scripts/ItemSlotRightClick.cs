using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 아이템 슬롯에 붙여서 우클릭 시 ContextMenuController를 통해 메뉴를 띄우는 예시 스크립트.
/// 실제 프로젝트에서는 이 스크립트를 각 슬롯의 아이템 데이터에 맞게 확장해서 쓰면 됩니다.
/// (예: 아이템 종류에 따라 Craft/Purchase 항목을 다르게 보여주는 등)
/// </summary>
public class ItemSlotRightClick : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("이 슬롯이 비어있으면 메뉴를 띄우지 않음")]
    public bool hasItem = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hasItem) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;

        var options = new List<(string, UnityAction)>
        {
            ("Remove",  () => Debug.Log($"{name}: Remove 클릭됨")),
            ("Inspect", () => Debug.Log($"{name}: Inspect 클릭됨")),
            ("Craft",   () => Debug.Log($"{name}: Craft 클릭됨")),
            ("Purchase from Lance", () => Debug.Log($"{name}: Purchase 클릭됨")),
            ("Substitute", () => Debug.Log($"{name}: Substitute 클릭됨")),
        };

        ContextMenuController.Instance.Show(options, eventData.position);
    }
}
