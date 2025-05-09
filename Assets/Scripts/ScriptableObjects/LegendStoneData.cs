using UnityEngine;

[CreateAssetMenu(fileName = "NewLegendStone", menuName = "Legends/Legend Stone")]
public class LegendStoneData : ScriptableObject
{
    public bool isTopStone;
    public ulong ownerClientId;
    public string stoneName;
    public Sprite stoneIcon;
    public float paddleSpeedBonus;
    public float damageResistance;

    public GameObject stonePrefab; 
}
