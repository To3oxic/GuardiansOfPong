using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card")]
public class CardData : ScriptableObject
{
    public CardRarity rarity;

    public string cardName;
    public Sprite cardImage;
    public float manaCost;
    public CardEffect effect;

    public Material ballMaterialOverride;
    public AudioClip activationSound;
}