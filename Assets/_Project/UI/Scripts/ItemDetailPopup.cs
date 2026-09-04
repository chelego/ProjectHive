using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPopup : MonoBehaviour
{
    public static ItemDetailPopup Instance { get; private set; }

    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text itemDescription;
    [SerializeField] private TMP_Text itemWeight;
    [SerializeField] private GameObject noiseWarningBadge;

    private void Awake()
    {
        Instance = this;
        popupRoot.SetActive(false);
    }

    public void Show(ItemData data)
    {
        itemIcon.sprite = data.icon;
        itemName.text = data.itemName;
        itemDescription.text = data.description;
        itemWeight.text = $"{data.weight}kg";
        noiseWarningBadge.SetActive(data.causesNoise);

        popupRoot.SetActive(true);
    }

    public void Hide()
    {
        popupRoot.SetActive(false);
    }
}