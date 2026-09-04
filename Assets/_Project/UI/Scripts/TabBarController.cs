using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 상단 탭바(INVENTORY / LOGBOOK / SYSTEM / LOADOUT)의 선택 상태를 관리합니다.
/// 탭 클릭 시 해당 탭에만 흰 테두리 박스가 표시되고 나머지는 사라집니다.
/// 각 탭에 연결된 패널 오브젝트를 켜고 끄는 것도 함께 처리합니다 (선택 사항).
/// </summary>
public class TabBarController : MonoBehaviour
{
    [System.Serializable]
    public class TabEntry
    {
        public string tabName;
        public Button button;
        public GameObject selectedBorder; // 탭 안에 미리 넣어둔 "선택됨" 테두리 오브젝트
        public GameObject linkedPanel;    // 선택 시 활성화할 화면 패널 (없으면 비워둬도 됨)
    }

    public List<TabEntry> tabs = new List<TabEntry>();
    public int defaultSelectedIndex = 3; // 레퍼런스처럼 LOADOUT이 기본 선택이면 3

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i; // 클로저 캡처용
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }

        SelectTab(defaultSelectedIndex);
    }

    public void SelectTab(int index)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool isSelected = (i == index);
            if (tabs[i].selectedBorder != null)
                tabs[i].selectedBorder.SetActive(isSelected);
            if (tabs[i].linkedPanel != null)
                tabs[i].linkedPanel.SetActive(isSelected);
        }
    }
}
