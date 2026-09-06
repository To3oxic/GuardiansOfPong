using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Displays player's card collection in the main menu.
/// Shows card names, levels, and quantities.
/// </summary>
public class CardDisplayManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI cardsTitle;
    [SerializeField] private Transform cardListContainer;
    [SerializeField] private GameObject cardDisplayPrefab;

    [Header("Test/Debug")]
    [SerializeField] private Button awardTestCardButton;
    [SerializeField] private Button levelUpTestCardButton;

    private PlayFabBackendManager backendManager;
    private List<GameObject> displayedCards = new List<GameObject>();

    private void Awake()
    {
        backendManager = PlayFabBackendManager.Instance;
        if (backendManager == null)
        {
            return;
        }
    }

    private void Start()
    {
        // Hook button events for testing
        if (awardTestCardButton != null)
            awardTestCardButton.onClick.AddListener(OnAwardTestCard);

        if (levelUpTestCardButton != null)
            levelUpTestCardButton.onClick.AddListener(OnLevelUpTestCard);

        // Refresh display when cards are loaded
        RefreshCardDisplay();
    }

    public void RefreshCardDisplay()
    {
        if (backendManager == null) return;

        // Clear existing displays
        foreach (var display in displayedCards)
        {
            Destroy(display);
        }
        displayedCards.Clear();

        // Load and display cards
        backendManager.LoadPlayerCardCollection((collection) =>
        {
            if (collection == null || collection.cards.Count == 0)
            {
                if (cardsTitle != null)
                    cardsTitle.text = "No cards yet";
                return;
            }

            if (cardsTitle != null)
                cardsTitle.text = $"My Cards ({collection.cards.Count})";

            foreach (var card in collection.cards)
            {
                DisplayCard(card);
            }
        });
    }

    private void DisplayCard(PlayerCardOwnership card)
    {
        if (cardDisplayPrefab == null)
        {
            // Simple text-based display if no prefab
            if (cardListContainer != null)
            {
                var cardText = new GameObject($"Card_{card.cardId}");
                cardText.transform.SetParent(cardListContainer, false);

                var textComponent = cardText.AddComponent<TextMeshProUGUI>();
                textComponent.text = $"{card.cardName} - Lv.{card.level} (x{card.quantity})";
                textComponent.fontSize = 4;

                displayedCards.Add(cardText);
            }
        }
        else
        {
            // Use prefab if provided
            var displayInstance = Instantiate(cardDisplayPrefab, cardListContainer);
            var cardDisplay = displayInstance.GetComponent<CardDisplayUI>();

            if (cardDisplay != null)
            {
                cardDisplay.SetCard(card);
            }
            else
            {
                // Fallback: just set text
                var textComponent = displayInstance.GetComponent<TextMeshProUGUI>();
                if (textComponent != null)
                    textComponent.text = $"{card.cardName} - Lv.{card.level} (x{card.quantity})";
            }

            displayedCards.Add(displayInstance);
        }
    }

    private void OnAwardTestCard()
    {
        // Award a test card named "Boulder Guardian"
        backendManager.AwardCardToPlayer("card_boulder", "Boulder Guardian", 1, (success) =>
        {
            if (success)
            {
                RefreshCardDisplay();
            }
            else
            {
            }
        });
    }

    private void OnLevelUpTestCard()
    {
        // Level up the Boulder Guardian card (costs 100 currency)
        backendManager.LevelUpCard("card_boulder", 100, (success, message) =>
        {
            if (success)
            {
                RefreshCardDisplay();
            }
            else
            {
            }
        });
    }
}

/// <summary>
/// Simple UI component to display a single card (optional).
/// </summary>
public class CardDisplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI cardLevelText;
    [SerializeField] private TextMeshProUGUI cardQuantityText;

    public void SetCard(PlayerCardOwnership card)
    {
        if (cardNameText != null)
            cardNameText.text = card.cardName;

        if (cardLevelText != null)
            cardLevelText.text = $"Lv.{card.level}";

        if (cardQuantityText != null)
            cardQuantityText.text = $"x{card.quantity}";
    }
}
