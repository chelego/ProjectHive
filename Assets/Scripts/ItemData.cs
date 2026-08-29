using UnityEngine;

[CreateAssetMenu(menuName = "Wasteland/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;
    public float weight;
    public bool causesNoise;
}