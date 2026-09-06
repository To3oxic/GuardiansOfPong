using UnityEngine;

[CreateAssetMenu(fileName = "NewLegendStone", menuName = "Legends/Legend Stone")]
public class LegendStoneData : ScriptableObject
{
    // public bool isTopStone; // Obsolete: Determined at runtime or by paddle's initial position
    // public ulong ownerClientId; // Obsolete: Instance-specific, not part of the data asset

    [Header("Identification & UI")]
    public string stoneName; // Unique identifier
    public Sprite stoneIcon;
    public string id;

    public CardRarity rarity;

    [Header("Prefabs")]
    public GameObject legendStoneVisualPrefab; // The visual prefab for the stone itself (with LegendStoneController)

    // Paddle prefab is now retrieved from the LegendStone prefab itself
    public GameObject AssociatedPaddlePrefab
    {
        get
        {
            if (legendStoneVisualPrefab != null)
            {
                LegendStoneController stoneController = legendStoneVisualPrefab.GetComponent<LegendStoneController>();
                if (stoneController != null)
                {
                    return stoneController.associatedPaddlePrefab;
                }
            }
            return null;
        }
    }

    [Header("Gameplay Stats")]
    public float paddleSpeedBonus;
    public float damageResistance; // Example stat, expand as needed
    // TODO: Add fields for passive abilities description, effects, etc.
}
