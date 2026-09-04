using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StorageThemeApplier : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] private Image background;

    [Header("장착 슬롯 6개 (드래그로 순서 상관없이 넣기)")]
    [SerializeField] private Image[] equipSlots;

    [Header("안전포켓 슬롯 2개")]
    [SerializeField] private Image[] safePocketSlots;

    [Header("인벤토리 셀 32개 (부모만 넣으면 자식 전부 찾음)")]
    [SerializeField] private Transform inventoryGridParent;

    [Header("한글 폰트")]
    [SerializeField] private TMP_FontAsset koreanFont;

    [ContextMenu("테마 적용하기")]
    public void ApplyTheme()
    {
        if (background != null)
            background.color = UITheme.Background;

        foreach (var slot in equipSlots)
        {
            if (slot == null) continue;
            slot.color = UITheme.SlotBg;
            SetOutline(slot.gameObject, UITheme.Border);
        }

        foreach (var slot in safePocketSlots)
        {
            if (slot == null) continue;
            slot.color = UITheme.SlotBg;
            SetOutline(slot.gameObject, UITheme.SafePocketBorder);
        }

        if (inventoryGridParent != null)
        {
            foreach (Transform child in inventoryGridParent)
            {
                Image img = child.GetComponent<Image>();
                if (img == null) continue;
                img.color = UITheme.SlotBg;
                SetOutline(child.gameObject, UITheme.Border);
            }
        }

        Debug.Log("테마 적용 완료!");
    }

    [ContextMenu("한글 폰트만 일괄 적용")]
    public void ApplyKoreanFontToAll()
    {
        if (koreanFont == null)
        {
            Debug.LogWarning("Korean Font가 연결 안 됨");
            return;
        }

        TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        foreach (var text in allTexts)
        {
            text.font = koreanFont;
        }

        Debug.Log($"{allTexts.Length}개 텍스트에 한글 폰트 적용 완료");
    }

    private void SetOutline(GameObject go, Color color)
    {
        Outline outline = go.GetComponent<Outline>();
        if (outline == null)
        {
            outline = go.AddComponent<Outline>();
        }
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1, -1);
    }
}