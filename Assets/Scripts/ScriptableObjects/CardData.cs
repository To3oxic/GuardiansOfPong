using System.Data;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card")]
public class CardData : ScriptableObject
{
    public CardRarity rarity;
    public CardType cardType;

    public string id;

    public string cardName;
    public Sprite cardImage;
    public float manaCost;
    public CardEffect effect;

    public Material ballMaterialOverride;
    public AudioClip activationSound;

    [Header("Leveling")]
    public int[] damagePerLevel = new int[] { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28 }; // Index 0 = Level 1
    public int[] hpPerLevel = new int[] { 50, 55, 60, 65, 70, 75, 80, 85, 90, 95 }; // Index 0 = Level 1

    public int[] durationPerLevel = new int[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    /// <summary>
    /// Get the final damage for this card at a given level
    /// </summary>
    public int GetDamageAtLevel(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, damagePerLevel.Length - 1);
        return damagePerLevel[index];
    }

    /// <summary>
    /// Get the final HP for this card at a given level
    /// </summary>
    public int GetHPAtLevel(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, hpPerLevel.Length - 1);
        return hpPerLevel[index];
    }

    /// <summary>
    /// Get max level based on array size
    /// </summary>
    public int GetMaxLevel()
    {
        return damagePerLevel.Length;
    }

    /// <summary>
    /// Get the final duration for this card at a given level
    /// </summary>
    public int GetDurationAtLevel(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, durationPerLevel.Length - 1);
        return durationPerLevel[index];
    }
}