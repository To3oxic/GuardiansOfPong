using UnityEngine;

[CreateAssetMenu(fileName = "NewCastingLegend", menuName = "Legends/Casting Legend")]
public class CastingLegendData : ScriptableObject
{
    public string legendName;
    public string id;
    public GameObject legendAvatarPrefab;
    public Sprite legendIcon;
    public string floorId;
    public string description;
public CardRarity rarity;

    public float damageBoostPercent;
    public float manaCostReduction;
    // Add more buff types here
}
