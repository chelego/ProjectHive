using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 이미 존재하는 카테고리 탭 버튼들(방어구/배낭/주무기/부무기/근접무기/투척무기)에
/// 선택 상태 표시를 추가하는 컨트롤러.
///
/// 새로 버튼을 만들지 않고, 기존 EquipSlotPanel 옆 카테고리 버튼들에 이 컴포넌트 하나만
/// 붙이고 Inspector에서 리스트를 채우면 됩니다.
///
/// 선택된 탭은:
///   1) 배경색이 selectedBgColor로 밝아지고
///   2) accentBar(왼쪽 색 바, 있다면)가 활성화됩니다.
///
/// 사용법:
/// 1. 카테고리 버튼들을 감싸는 부모(또는 아무 빈 오브젝트)에 이 컴포넌트 추가
/// 2. Tabs 리스트 크기를 6으로 설정
/// 3. 각 항목에 Button, Background Image, (선택) Accent Bar 오브젝트 연결
/// 4. Play 모드에서 탭 클릭하면 하이라이트 전환 확인
/// </summary>
public class CategoryTabGroup : MonoBehaviour
{
    [System.Serializable]
    public class CategoryTab
    {
        public string categoryName;
        public Button button;
        public Image background;          // 탭 배경 Image (필수)
        public GameObject accentBar;      // 왼쪽 색 바 오브젝트 (없으면 비워둬도 됨)
    }

    [Header("탭 목록")]
    public List<CategoryTab> tabs = new List<CategoryTab>();

    [Header("색상")]
    public Color normalBgColor   = new Color(0.11f, 0.12f, 0.16f, 1f);
    public Color selectedBgColor = new Color(0.18f, 0.19f, 0.24f, 1f);

    [Header("기본 선택 인덱스 (-1이면 아무것도 선택 안 함)")]
    public int defaultSelectedIndex = 0;

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }

        if (defaultSelectedIndex >= 0 && defaultSelectedIndex < tabs.Count)
            SelectTab(defaultSelectedIndex);
    }

    public void SelectTab(int index)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool isSelected = (i == index);

            if (tabs[i].background != null)
                tabs[i].background.color = isSelected ? selectedBgColor : normalBgColor;

            if (tabs[i].accentBar != null)
                tabs[i].accentBar.SetActive(isSelected);
        }

        // 여기서 실제 카테고리 필터링 로직(인벤토리 아이템 필터 등)을 연결하면 됩니다.
        // 예: InventoryManager.Instance.FilterByCategory(tabs[index].categoryName);
    }
}
