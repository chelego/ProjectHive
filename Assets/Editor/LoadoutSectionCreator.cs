using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 로드아웃 화면의 EQUIPMENT / QUICK USE / SAFE POCKET 세 구역을
/// 레퍼런스 스타일(섹션 헤더 + 슬롯 그리드, 세이프포켓은 금테두리)로 생성하는 에디터 도구.
///
/// 기존에 만들어둔 12x6 백팩 그리드(InventoryGridBuilder)와는 별개로,
/// "장비 착용 슬롯" 영역만 다루는 도구입니다.
///
/// 사용법:
/// 1. Hierarchy에서 이 세 구역이 들어갈 부모(로드아웃 패널) 선택
/// 2. GameObject > UI > Loadout Sections (Equipment/QuickUse/SafePocket) 클릭
/// </summary>
public static class LoadoutSectionCreator
{
    // ===================== CONFIG =====================
    private static readonly Color PanelBgColor       = new Color(0.07f, 0.08f, 0.11f, 1f);   // 진한 남색-검정
    private static readonly Color SlotBgColor        = new Color(0.11f, 0.12f, 0.16f, 1f);
    private static readonly Color SlotBorderColor    = new Color(0.23f, 0.25f, 0.30f, 1f);   // 기본 회색 테두리
    private static readonly Color SafePocketBorder   = new Color(0.79f, 0.64f, 0.29f, 1f);   // 금색
    private static readonly Color HeaderTextColor    = new Color(0.54f, 0.56f, 0.61f, 1f);   // 연회색 라벨
    private static readonly Color CountTextColor     = new Color(0.72f, 0.74f, 0.78f, 1f);

    private const float SmallSlotSize = 90f;
    private const float BigSlotHeight = 160f;   // 큰 무기 슬롯
    private const float SlotSpacing   = 8f;
    private const float SectionSpacing = 20f;
    private const float HeaderFontSize = 14f;
    // ====================================================

    [MenuItem("GameObject/UI/Loadout Sections (Equipment/QuickUse/SafePocket)", false, 12)]
    public static void CreateSections()
    {
        Transform parent = Selection.activeTransform;
        if (parent == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("Canvas가 없습니다. 부모 오브젝트를 먼저 선택하세요.");
                return;
            }
            parent = canvas.transform;
        }

        GameObject root = new GameObject("LoadoutSections", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Loadout Sections");
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(700f, 700f);

        VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = SectionSpacing;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childAlignment = TextAnchor.UpperLeft;

        // --- EQUIPMENT 섹션 ---
        GameObject equipSection = CreateSection(root.transform, "EQUIPMENT", null);
        GameObject equipGrid = CreateSlotGrid(equipSection.transform, "EquipSmallSlots", 4, SmallSlotSize, SmallSlotSize, false);
        CreateSlot(equipSection.transform, "WeaponSlot", 0f, BigSlotHeight, false); // 큰 무기 슬롯 (풀 너비)
        LayoutElement weaponLE = equipSection.transform.Find("WeaponSlot").gameObject.AddComponent<LayoutElement>();
        weaponLE.preferredHeight = BigSlotHeight;
        weaponLE.flexibleWidth = 1;

        // --- QUICK USE 섹션 ---
        GameObject quickUseSection = CreateSection(root.transform, "QUICK USE", "4/4");
        CreateSlotGrid(quickUseSection.transform, "QuickUseSlots", 4, SmallSlotSize, SmallSlotSize, false);

        // --- SAFE POCKET 섹션 ---
        GameObject safePocketSection = CreateSection(root.transform, "SAFE POCKET", null);
        CreateSlotGrid(safePocketSection.transform, "SafePocketSlots", 2, SmallSlotSize, SmallSlotSize, true);

        Selection.activeGameObject = root;
    }

    // 헤더(라벨 + 선택적 카운트) + 콘텐츠 컨테이너를 가진 섹션 생성
    private static GameObject CreateSection(Transform parent, string title, string countText)
    {
        GameObject sectionGO = new GameObject(title.Replace(" ", "") + "_Section", typeof(RectTransform));
        sectionGO.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = sectionGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = sectionGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 헤더 (라벨 + 카운트)
        GameObject headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(sectionGO.transform, false);
        LayoutElement headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 24f;

        HorizontalLayoutGroup headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = false;
        headerLayout.childForceExpandWidth = false;

        TextMeshProUGUI titleTMP = CreateLabel(headerGO.transform, "Title", title, HeaderFontSize, HeaderTextColor);
        titleTMP.characterSpacing = 2f;
        titleTMP.fontStyle = FontStyles.Bold;

        if (!string.IsNullOrEmpty(countText))
        {
            GameObject spacerGO = new GameObject("Spacer", typeof(RectTransform));
            spacerGO.transform.SetParent(headerGO.transform, false);
            LayoutElement spacerLE = spacerGO.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1;

            TextMeshProUGUI countTMP = CreateLabel(headerGO.transform, "Count", countText, HeaderFontSize, CountTextColor);
        }

        return sectionGO;
    }

    // 여러 슬롯을 가로 그리드로 배치
    private static GameObject CreateSlotGrid(Transform parent, string name, int count, float slotW, float slotH, bool goldBorder)
    {
        GameObject gridGO = new GameObject(name, typeof(RectTransform));
        gridGO.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = gridGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = SlotSpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        LayoutElement gridLE = gridGO.AddComponent<LayoutElement>();
        gridLE.preferredHeight = slotH;

        for (int i = 0; i < count; i++)
        {
            CreateSlot(gridGO.transform, name + "_Slot" + i, slotW, slotH, goldBorder);
        }

        return gridGO;
    }

    // 슬롯 하나 (배경 + 테두리)
    private static GameObject CreateSlot(Transform parent, string name, float width, float height, bool goldBorder)
    {
        GameObject slotGO = new GameObject(name, typeof(RectTransform));
        slotGO.transform.SetParent(parent, false);

        RectTransform rect = slotGO.GetComponent<RectTransform>();
        if (width > 0f)
        {
            rect.sizeDelta = new Vector2(width, height);
            LayoutElement le = slotGO.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
        }
        else
        {
            rect.sizeDelta = new Vector2(0f, height);
        }

        // 배경
        Image bg = slotGO.AddComponent<Image>();
        bg.color = SlotBgColor;

        // 테두리 (Outline 컴포넌트로 간단히, 또는 별도 프레임 이미지로 대체 가능)
        Outline outline = slotGO.AddComponent<Outline>();
        outline.effectColor = goldBorder ? SafePocketBorder : SlotBorderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 아이템 아이콘이 들어갈 자식 (비어있는 상태로 생성)
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(slotGO.transform, false);
        RectTransform iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        Image icon = iconGO.AddComponent<Image>();
        icon.color = new Color(1f, 1f, 1f, 0f); // 아이템 없을 때 투명, 아이템 들어오면 스프라이트+alpha 1로 교체
        icon.preserveAspect = true;

        return slotGO;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Color color)
    {
        GameObject labelGO = new GameObject(name, typeof(RectTransform));
        labelGO.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableAutoSizing = false;

        LayoutElement le = labelGO.AddComponent<LayoutElement>();
        le.preferredWidth = tmp.preferredWidth;

        return tmp;
    }
}
