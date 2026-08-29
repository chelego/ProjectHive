using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 아이템 슬롯 우클릭 시 뜨는 컨텍스트 메뉴 (Remove / Inspect / Craft / Purchase / Substitute 같은 리스트).
/// 씬에 하나만 존재하는 싱글톤으로 두고, 어디서든 ContextMenuController.Instance.Show(...) 로 호출합니다.
///
/// 사용법 예시 (아이템 슬롯 스크립트에서):
///   var options = new List<(string, UnityAction)> {
///       ("Remove", () => RemoveItem()),
///       ("Inspect", () => InspectItem()),
///   };
///   ContextMenuController.Instance.Show(options, Input.mousePosition);
/// </summary>
public class ContextMenuController : MonoBehaviour
{
    public static ContextMenuController Instance { get; private set; }

    [Header("연결 (에디터 스크립트가 자동 생성 후 연결함)")]
    public RectTransform menuPanel;      // 크림색 배경 패널
    public RectTransform itemContainer;  // 메뉴 항목들이 쌓이는 VerticalLayoutGroup 컨테이너
    public GameObject itemTemplate;      // 메뉴 항목 하나의 템플릿 (비활성 상태로 보관)
    public Button blocker;               // 화면 전체를 덮는 투명 버튼, 바깥 클릭 시 메뉴 닫기용
    public Canvas parentCanvas;

    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        if (menuPanel != null) menuPanel.gameObject.SetActive(false);
        if (blocker != null)
        {
            blocker.gameObject.SetActive(false);
            blocker.onClick.AddListener(Hide);
        }
    }

    public void Show(List<(string label, UnityAction action)> options, Vector2 screenPosition)
    {
        ClearItems();

        foreach (var (label, action) in options)
        {
            GameObject itemGO = Instantiate(itemTemplate, itemContainer);
            itemGO.SetActive(true);
            itemGO.name = "Item_" + label;

            TextMeshProUGUI tmp = itemGO.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = label;

            Button btn = itemGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() =>
                {
                    action?.Invoke();
                    Hide();
                });
            }

            _spawnedItems.Add(itemGO);
        }

        menuPanel.gameObject.SetActive(true);
        blocker.gameObject.SetActive(true);
        menuPanel.transform.SetAsLastSibling(); // 항상 맨 위에 그려지도록

        PositionAtScreenPoint(screenPosition);
    }

    public void Hide()
    {
        menuPanel.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
        ClearItems();
    }

    private void ClearItems()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        _spawnedItems.Clear();
    }

    // 마우스 클릭 지점에 메뉴를 띄우되, 화면 밖으로 나가지 않게 보정
    private void PositionAtScreenPoint(Vector2 screenPosition)
    {
        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out Vector2 localPoint);

        menuPanel.anchoredPosition = localPoint;

        // 강제로 레이아웃 갱신 후 크기를 얻어 화면 경계 보정
        Canvas.ForceUpdateCanvases();

        Vector2 pivotOffset = menuPanel.pivot;
        Vector2 size = menuPanel.sizeDelta;
        Vector2 canvasSize = canvasRect.sizeDelta;

        Vector2 pos = menuPanel.anchoredPosition;

        // 오른쪽 경계
        float rightEdge = pos.x + size.x * (1f - pivotOffset.x);
        if (rightEdge > canvasSize.x * 0.5f)
            pos.x -= (rightEdge - canvasSize.x * 0.5f);

        // 왼쪽 경계
        float leftEdge = pos.x - size.x * pivotOffset.x;
        if (leftEdge < -canvasSize.x * 0.5f)
            pos.x += (-canvasSize.x * 0.5f - leftEdge);

        // 아래쪽 경계 (메뉴가 클릭 지점 아래로 펼쳐진다고 가정, pivot이 좌상단(0,1)일 때 기준)
        float bottomEdge = pos.y - size.y * pivotOffset.y;
        if (bottomEdge < -canvasSize.y * 0.5f)
            pos.y += (-canvasSize.y * 0.5f - bottomEdge);

        // 위쪽 경계
        float topEdge = pos.y + size.y * (1f - pivotOffset.y);
        if (topEdge > canvasSize.y * 0.5f)
            pos.y -= (topEdge - canvasSize.y * 0.5f);

        menuPanel.anchoredPosition = pos;
    }
}
