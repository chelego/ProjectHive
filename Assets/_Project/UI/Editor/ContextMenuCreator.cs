using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 아이템 우클릭 시 뜨는 컨텍스트 메뉴 UI(크림색 배경 + 메뉴 항목 리스트)를 생성하는 에디터 도구.
///
/// 사용법:
/// 1. Canvas 선택
/// 2. GameObject > UI > Context Menu (Right-Click Popup) 클릭
/// 3. 생성된 ContextMenu 오브젝트는 자동으로 ContextMenuController가 연결된 상태
/// 4. 아이템 슬롯 스크립트에서 ContextMenuController.Instance.Show(...) 호출해서 사용
/// </summary>
public static class ContextMenuCreator
{
    // ===================== CONFIG =====================
    private static readonly Color PanelBgColor    = new Color(0.94f, 0.90f, 0.80f, 1f); // 크림색
    private static readonly Color ItemTextColor   = new Color(0.16f, 0.14f, 0.12f, 1f); // 진한 갈색-검정
    private static readonly Color ItemHoverColor  = new Color(0f, 0f, 0f, 0.08f);       // 호버 시 살짝 어둡게
    private const float MenuWidth = 200f;
    private const float ItemHeight = 34f;
    private const float FontSize = 15f;
    // ====================================================

    [MenuItem("GameObject/UI/Context Menu (Right-Click Popup)", false, 14)]
    public static void CreateContextMenu()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogWarning("Canvas가 없습니다."); return; }

        GameObject root = new GameObject("ContextMenuSystem", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Context Menu");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // 전체 화면을 덮는 투명 블로커 (바깥 클릭 시 메뉴 닫기)
        GameObject blockerGO = new GameObject("Blocker", typeof(RectTransform));
        blockerGO.transform.SetParent(root.transform, false);
        RectTransform blockerRect = blockerGO.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        Image blockerImg = blockerGO.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0); // 완전 투명, 클릭 감지용
        Button blockerBtn = blockerGO.AddComponent<Button>();
        blockerBtn.transition = Selectable.Transition.None;
        blockerGO.SetActive(false);

        // 메뉴 패널 (크림색 배경)
        GameObject panelGO = new GameObject("MenuPanel", typeof(RectTransform));
        panelGO.transform.SetParent(root.transform, false);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.pivot = new Vector2(0f, 1f); // 클릭 지점 기준 오른쪽 아래로 펼쳐짐
        panelRect.sizeDelta = new Vector2(MenuWidth, 0f); // 높이는 ContentSizeFitter가 계산
        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = PanelBgColor;

        VerticalLayoutGroup panelLayout = panelGO.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(2, 2, 6, 6);
        panelLayout.spacing = 0f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;

        ContentSizeFitter panelFitter = panelGO.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 아이템 컨테이너 (패널 자체를 컨테이너로 겸용)
        Transform itemContainer = panelGO.transform;

        // 항목 템플릿 (비활성 상태로 보관, Instantiate해서 씀)
        GameObject template = CreateItemTemplate(itemContainer);
        template.SetActive(false);

        // 컨트롤러 연결
        ContextMenuController controller = root.AddComponent<ContextMenuController>();
        controller.menuPanel = panelRect;
        controller.itemContainer = (RectTransform)itemContainer;
        controller.itemTemplate = template;
        controller.blocker = blockerBtn;
        controller.parentCanvas = canvas;

        panelGO.SetActive(false);

        Selection.activeGameObject = root;
    }

    private static GameObject CreateItemTemplate(Transform parent)
    {
        GameObject itemGO = new GameObject("MenuItemTemplate", typeof(RectTransform));
        itemGO.transform.SetParent(parent, false);

        LayoutElement le = itemGO.AddComponent<LayoutElement>();
        le.preferredHeight = ItemHeight;

        Image bg = itemGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0);

        Button btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f); // 살짝 어둡게 (bg가 흰 배경 위 알파이므로)
        btn.colors = colors;

        // 실제 호버 어둡게는 별도 오버레이로 처리 (Selectable 색상은 alpha 기반이라 오차 있어서)
        GameObject hoverOverlay = new GameObject("HoverOverlay", typeof(RectTransform));
        hoverOverlay.transform.SetParent(itemGO.transform, false);
        RectTransform hoverRect = hoverOverlay.GetComponent<RectTransform>();
        hoverRect.anchorMin = Vector2.zero;
        hoverRect.anchorMax = Vector2.one;
        hoverRect.offsetMin = Vector2.zero;
        hoverRect.offsetMax = Vector2.zero;
        Image hoverImg = hoverOverlay.AddComponent<Image>();
        hoverImg.color = ItemHoverColor;
        hoverImg.raycastTarget = false;
        // 참고: 정교한 호버 표시가 필요하면 EventTrigger로 PointerEnter/Exit 이벤트를 걸어
        // hoverOverlay.SetActive(true/false) 하도록 별도 스크립트를 붙이면 됩니다.
        hoverOverlay.SetActive(false);

        // 텍스트
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(itemGO.transform, false);
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 0f);
        labelRect.offsetMax = new Vector2(-14f, 0f);

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Menu Item";
        tmp.fontSize = FontSize;
        tmp.color = ItemTextColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;

        return itemGO;
    }
}
