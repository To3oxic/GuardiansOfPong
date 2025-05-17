using UnityEngine;

[CreateAssetMenu(fileName = "NewLegendStone", menuName = "Legends/Legend Stone")]
public class LegendStoneData : ScriptableObject
{
    // public bool isTopStone; // Obsolete: Determined at runtime or by paddle's initial position
    // public ulong ownerClientId; // Obsolete: Instance-specific, not part of the data asset

    [Header("Identification & UI")]
    public string stoneName; // Unique identifier
    public Sprite stoneIcon;

    [Header("Prefabs")]
    public GameObject legendStoneVisualPrefab; // The visual prefab for the stone itself (with LegendStoneController)
    public GameObject associatedPaddlePrefab; // The Paddle prefab to use when this stone is selected

    [Header("Gameplay Stats")]
    public float paddleSpeedBonus;
    public float damageResistance; // Example stat, expand as needed
    // TODO: Add fields for passive abilities description, effects, etc.
}
