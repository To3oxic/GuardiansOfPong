using UnityEngine;

[CreateAssetMenu(fileName = "NewCastingLegend", menuName = "Legends/Casting Legend")]
public class CastingLegendData : ScriptableObject
{
    public string legendName;
    public GameObject legendAvatarPrefab;
    public string description;

    public float damageBoostPercent;
    public float manaCostReduction;
    // Add more buff types here
}
