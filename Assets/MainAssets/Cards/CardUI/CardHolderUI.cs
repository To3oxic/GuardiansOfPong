using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardHolderUI : MonoBehaviour
{
    public Image rarityBackground;
    public Image cardImage;

    public Sprite commonBG;
    public Sprite rareBG;
    public Sprite epicBG;

    public TextMeshProUGUI manaCostText;

    private CardDragHandler dragHandler;
    public CardData currentCardData { get; private set; } // Store current card data

    private void Awake()
    {
        // Get or add the drag handler component
        dragHandler = GetComponent<CardDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = gameObject.AddComponent<CardDragHandler>();
        }
    }

    public void Setup(CardData data)
    {
        currentCardData = data; // Store the current card data

        if (data == null)
        {
            // Clear the card
            rarityBackground.sprite = commonBG;
            cardImage.sprite = null;
            cardImage.color = new Color(0, 0, 0, 0); // Make transparent
            if (dragHandler != null)
            {
                dragHandler.Initialize(null);
            }
            return;
        }

        // Set rarity background
        switch (data.rarity)
        {
            case CardRarity.Common:
                rarityBackground.sprite = commonBG;
                break;
            case CardRarity.Rare:
                rarityBackground.sprite = rareBG;
                break;
            case CardRarity.Epic:
                rarityBackground.sprite = epicBG;
                break;
        }

        // Set card image
        cardImage.sprite = data.cardImage;
        cardImage.color = Color.white; // Make visible

        // Set mana cost text
        if (manaCostText != null)
        {
            manaCostText.text = data.manaCost.ToString();
        }

        // Initialize the drag handler with the card data
        if (dragHandler != null)
        {
            dragHandler.Initialize(data);
        }
        else
        {
        }
    }

    /// <summary>
    /// Setup legend card (hide mana cost)
    /// </summary>
    public void SetupLegend(CastingLegendData legendData)
    {
        if (legendData == null)
        {
            cardImage.sprite = null;
            cardImage.color = new Color(0, 0, 0, 0);
            if (manaCostText != null)
                manaCostText.gameObject.SetActive(false);
            return;
        }

        // Set rarity background (legendaries are usually epic/legendary)
        rarityBackground.sprite = epicBG;

        // Set card image
        cardImage.sprite = legendData.legendIcon;
        cardImage.color = Color.white;

        // Hide mana cost for legends
        if (manaCostText != null)
        {
            manaCostText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Setup legend stone card (hide mana cost)
    /// </summary>
    public void SetupLegendStone(LegendStoneData stoneData)
    {
        if (stoneData == null)
        {
            cardImage.sprite = null;
            cardImage.color = new Color(0, 0, 0, 0);
            if (manaCostText != null)
                manaCostText.gameObject.SetActive(false);
            return;
        }

        // Set rarity background (stones are usually legendary)
        rarityBackground.sprite = epicBG;

        // Set card image
        cardImage.sprite = stoneData.stoneIcon;
        cardImage.color = Color.white;

        // Hide mana cost for stones
        if (manaCostText != null)
        {
            manaCostText.gameObject.SetActive(false);
        }
    }
}
