using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minimal card holder for loot box rewards
/// Displays card image, rarity background, mana cost, and amount text
/// </summary>
public class LootBoxCardHolder : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private TextMeshProUGUI manaCostText;
    [SerializeField] private TextMeshProUGUI amountText; // Shows quantity of this card

    [Header("Sprites")]
    [SerializeField] private Sprite commonBG;
    [SerializeField] private Sprite rareBG;
    [SerializeField] private Sprite epicBG;

    private CardData currentCardData;
    private LegendStoneData currentLegendStoneData;
    private CastingLegendData currentCastingLegendData;

    /// <summary>
    /// Setup the card holder with card data
    /// </summary>
    public void SetupCard(CardData data, int amount = 1)
    {
        if (data == null)
        {
            return;
        }

        currentCardData = data;

        // Display card image
        if (cardImage != null && data.cardImage != null)
        {
            cardImage.sprite = data.cardImage;
        }

        // Display rarity background
        if (rarityBackground != null)
        {
            Sprite raritySprite = GetRaritySprite(data.rarity);
            if (raritySprite != null)
            {
                rarityBackground.sprite = raritySprite;
            }
        }

        // Display mana cost
        if (manaCostText != null)
        {
            manaCostText.text = data.manaCost.ToString();
        }

        // Display amount
        if (amountText != null)
        {
            amountText.text = amount > 1 ? $"x{amount}" : "";
        }
    }
    public void SetupGuardian(CastingLegendData data, int amount = 1)
    {
        if (data == null)
        {
            return;
        }

        currentCastingLegendData = data;

        // Display card image
        if (cardImage != null && data.legendIcon != null)
        {
            cardImage.sprite = data.legendIcon;
        }

        // Display rarity background
        if (rarityBackground != null)
        {
            Sprite raritySprite = GetRaritySprite(data.rarity);
            if (raritySprite != null)
            {
                rarityBackground.sprite = raritySprite;
            }
        }

        // Display amount
        if (amountText != null)
        {
            amountText.text = amount > 1 ? $"x{amount}" : "";
        }
    }
    public void SetupLegendStone(LegendStoneData data, int amount = 1)
    {
        if (data == null)
        {
            return;
        }

        currentLegendStoneData = data;

        // Display card image
        if (cardImage != null && data.stoneIcon != null)
        {
            cardImage.sprite = data.stoneIcon;
        }

        // Display rarity background
        if (rarityBackground != null)
        {
            Sprite raritySprite = GetRaritySprite(data.rarity);
            if (raritySprite != null)
            {
                rarityBackground.sprite = raritySprite;
            }
        }

        // Display amount
        if (amountText != null)
        {
            amountText.text = amount > 1 ? $"x{amount}" : "";
        }
    }

    /// <summary>
    /// Get the rarity sprite based on card rarity
    /// </summary>
    private Sprite GetRaritySprite(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return commonBG;
            case CardRarity.Rare:
                return rareBG;
            case CardRarity.Epic:
                return epicBG;
            default:
                return commonBG;
        }
    }

    /// <summary>
    /// Get the current card data
    /// </summary>
    public CardData GetCardData()
    {
        return currentCardData;
    }
}
