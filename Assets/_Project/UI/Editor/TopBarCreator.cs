using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 화면 상단에 탭 메뉴(INVENTORY / LOGBOOK / SYSTEM / LOADOUT)와
/// 우측 재화 표시(아이콘 + 숫자 3종)를 생성하는 에디터 도구.
///
/// 사용법:
/// 1. Canvas 선택
/// 2. GameObject > UI > Top Bar (Tabs + Currency) 클릭
/// 3. 생성된 TopBar 오브젝트의 TabBarController 컴포넌트에서
///    각 탭의 button/selectedBorder/linkedPanel을 필요에 맞게 연결
/// 4. 재화 아이콘(CurrencyIcon_0/1/2)에 실제 아이콘 스프라이트 연결, 숫자 텍스트 값 갱신은
///    별도 재화 관리 스크립트에서 CurrencyText 참조해서 처리
/// </summary>
public static class TopBarCreator
{
    // ===================== CONFIG =====================
    private static readonly string[] TabNames = { "INVENTORY", "LOGBOOK", "SYSTEM", "LOADOUT" };
    private const int DefaultSelectedTab = 3; // LOADOUT

    private static readonly Color BarBgColor        = new Color(0.05f, 0.06f, 0.08f, 0.95f);
    private static readonly Color TabTextColor      = new Color(0.75f, 0.77f, 0.82f, 1f);
    private static readonly Color TabSelectedColor  = Color.white;
    private static readonly Color CurrencyTextColor = Color.white;
    private static readonly Color CurrencyPillBg    = new Color(0.12f, 0.13f, 0.17f, 1f);

    private const float BarHeight = 64f;
    private const float TabSpacing = 40f;
    private const float TabFontSize = 18f;
    private const float CurrencyFontSize = 18f;
    private const float CurrencySpacing = 24f;
    // ====================================================

    [MenuItem("GameObject/UI/Top Bar (Tabs + Currency)", false, 13)]
    public static void CreateTopBar()
    {
        Transform parent = Selection.activeTransform;
        if (parent == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogWarning("Canvas가 없습니다."); return; }
            parent = canvas.transform;
        }

        // 루트 바
        GameObject barGO = new GameObject("TopBar", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(barGO, "Create Top Bar");
        barGO.transform.SetParent(parent, false);

        RectTransform barRect = barGO.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(0f, BarHeight);
        barRect.anchoredPosition = Vector2.zero;

        Image barBg = barGO.AddComponent<Image>();
        barBg.color = BarBgColor;

        // 탭 그룹 (가운데 정렬)
        GameObject tabGroupGO = new GameObject("TabGroup", typeof(RectTransform));
        tabGroupGO.transform.SetParent(barGO.transform, false);
        RectTransform tabGroupRect = tabGroupGO.GetComponent<RectTransform>();
        tabGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
        tabGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
        tabGroupRect.pivot = new Vector2(0.5f, 0.5f);
        tabGroupRect.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup tabLayout = tabGroupGO.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = TabSpacing;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;

        ContentSizeFitter tabFitter = tabGroupGO.AddComponent<ContentSizeFitter>();
        tabFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        tabFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TabBarController controller = barGO.AddComponent<TabBarController>();

        foreach (string tabName in TabNames)
        {
            var entry = CreateTab(tabGroupGO.transform, tabName);
            controller.tabs.Add(entry);
        }
        controller.defaultSelectedIndex = DefaultSelectedTab;

        // 재화 그룹 (오른쪽 정렬)
        GameObject currencyGroupGO = new GameObject("CurrencyGroup", typeof(RectTransform));
        currencyGroupGO.transform.SetParent(barGO.transform, false);
        RectTransform currencyRect = currencyGroupGO.GetComponent<RectTransform>();
        currencyRect.anchorMin = new Vector2(1f, 0.5f);
        currencyRect.anchorMax = new Vector2(1f, 0.5f);
        currencyRect.pivot = new Vector2(1f, 0.5f);
        currencyRect.anchoredPosition = new Vector2(-30f, 0f);

        HorizontalLayoutGroup currencyLayout = currencyGroupGO.AddComponent<HorizontalLayoutGroup>();
        currencyLayout.spacing = CurrencySpacing;
        currencyLayout.childAlignment = TextAnchor.MiddleRight;
        currencyLayout.childControlWidth = false;
        currencyLayout.childControlHeight = false;

        ContentSizeFitter currencyFitter = currencyGroupGO.AddComponent<ContentSizeFitter>();
        currencyFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        currencyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < 3; i++)
        {
            CreateCurrencyDisplay(currencyGroupGO.transform, "Currency" + i, "0");
        }

        Selection.activeGameObject = barGO;
    }

    private static TabBarController.TabEntry CreateTab(Transform parent, string label)
    {
        GameObject tabGO = new GameObject(label + "_Tab", typeof(RectTransform));
        tabGO.transform.SetParent(parent, false);

        LayoutElement le = tabGO.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;

        // 투명 클릭 배경 + 버튼
        Image bg = tabGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0);
        Button button = tabGO.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.None;

        HorizontalLayoutGroup innerLayout = tabGO.AddComponent<HorizontalLayoutGroup>();
        innerLayout.padding = new RectOffset(20, 20, 8, 8);
        innerLayout.childAlignment = TextAnchor.MiddleCenter;
        innerLayout.childControlWidth = false;
        innerLayout.childForceExpandWidth = false;

        ContentSizeFitter fitter = tabGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 텍스트
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(tabGO.transform, false);
        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = TabFontSize;
        tmp.color = TabTextColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1f;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.raycastTarget = false;
        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = tmp.preferredWidth;

        // 선택됨 표시용 흰 테두리 박스 (기본 비활성)
        GameObject borderGO = new GameObject("SelectedBorder", typeof(RectTransform));
        borderGO.transform.SetParent(tabGO.transform, false);
        RectTransform borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(0, 0, 0, 0); // 배경은 투명
        Outline outline = borderGO.AddComponent<Outline>();
        outline.effectColor = TabSelectedColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        borderGO.transform.SetAsFirstSibling(); // 텍스트 뒤로
        borderGO.SetActive(false);

        return new TabBarController.TabEntry
        {
            tabName = label,
            button = button,
            selectedBorder = borderGO,
            linkedPanel = null
        };
    }

    private static void CreateCurrencyDisplay(Transform parent, string name, string amount)
    {
        GameObject pillGO = new GameObject(name, typeof(RectTransform));
        pillGO.transform.SetParent(parent, false);

        LayoutElement pillLE = pillGO.AddComponent<LayoutElement>();
        pillLE.preferredHeight = 32f;

        Image pillBg = pillGO.AddComponent<Image>();
        pillBg.color = CurrencyPillBg;

        HorizontalLayoutGroup layout = pillGO.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 14, 4, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;

        ContentSizeFitter fitter = pillGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 아이콘 (스프라이트는 나중에 연결)
        GameObject iconGO = new GameObject("CurrencyIcon", typeof(RectTransform));
        iconGO.transform.SetParent(pillGO.transform, false);
        LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 22f;
        iconLE.preferredHeight = 22f;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;

        // 숫자 텍스트
        GameObject textGO = new GameObject("CurrencyText", typeof(RectTransform));
        textGO.transform.SetParent(pillGO.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = amount;
        tmp.fontSize = CurrencyFontSize;
        tmp.color = CurrencyTextColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement textLE = textGO.AddComponent<LayoutElement>();
        textLE.preferredWidth = 60f;
    }
}
