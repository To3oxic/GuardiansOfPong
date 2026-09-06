using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Populates the card selection UI with all player cards
/// Arranges cards in rows (3 cards per row with horizontal layout)
/// </summary>
public class CardSelectionPopulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform allCardsContainer; // Vertical layout container
    [SerializeField] private GameObject cardHolderPrefab; // SelectionCardHolder prefab
    [SerializeField] private GameObject rowPrefab; // Horizontal layout row prefab
    [SerializeField] private DeckRegistry deckRegistry;
    [SerializeField] private SelectionCardManager selectionManager; // Selection card manager

    [Header("Settings")]
    [SerializeField] private int cardsPerRow = 3;

    private void OnEnable()
    {
        // Populate cards from PlayerStates when this component is enabled
        if (PlayerStates.Instance != null)
        {
            var cardCollection = PlayerStates.Instance.CardCollection;
            if (cardCollection != null && cardCollection.cards.Count > 0)
            {
                PopulateFromPlayFab(cardCollection);
            }
        }
    }

    /// <summary>
    /// Load cards from PlayFab backend (new system)
    /// </summary>
    private void PopulateFromPlayFab(PlayerCardCollection cardCollection)
    {
        if (cardCollection == null || cardCollection.cards.Count == 0)
        {
            return;
        }

        // Clear existing cards
        foreach (Transform child in allCardsContainer)
        {
            Destroy(child.gameObject);
        }
        Transform emptyrow = Instantiate(rowPrefab, allCardsContainer).transform;

        // Group cards into rows
        int cardIndex = 0;
        Transform currentRow = null;
        int cardsInCurrentRow = 0;

        foreach (var playerCard in cardCollection.cards)
        {
            // Create new row if needed
            if (cardsInCurrentRow == 0)
            {
                currentRow = Instantiate(rowPrefab, allCardsContainer).transform;
                cardsInCurrentRow = 0;
            }

            // Create card holder
            var cardHolderInstance = Instantiate(cardHolderPrefab, currentRow);
            var selectionCardHolder = cardHolderInstance.GetComponent<SelectionCardHolder>();

            if (selectionCardHolder != null && selectionManager != null)
            {
                selectionCardHolder.SetSelectionManager(selectionManager);
                SetupPlayFabCard(selectionCardHolder, playerCard);
            }

            cardsInCurrentRow++;
            cardIndex++;

            // Move to next row if current row is full
            if (cardsInCurrentRow >= cardsPerRow)
            {
                cardsInCurrentRow = 0;
            }
        }
        Transform emptyrow1 = Instantiate(rowPrefab, allCardsContainer).transform;
    }

    /// <summary>
    /// Setup card from PlayFab card ownership data
    /// </summary>
    private void SetupPlayFabCard(SelectionCardHolder holder, PlayerCardOwnership playerCard)
    {
        // Try to find the card in registry by ID
        CardData cardData = GetCardDataById(playerCard.cardId);
        if (cardData != null)
        {
            holder.SetupCard(cardData, playerCard);

            if (selectionManager != null)
                selectionManager.RegisterCardHolder(holder);
            return;
        }

        // Try guardians by ID
        CastingLegendData guardianData = GetGuardianById(playerCard.cardId);
        if (guardianData != null)
        {
            holder.SetupGuardian(guardianData, playerCard);

            if (selectionManager != null)
                selectionManager.RegisterCardHolder(holder);
            return;
        }

        // Try legend stones by ID
        LegendStoneData stoneData = GetLegendStoneById(playerCard.cardId);
        if (stoneData != null)
        {
            holder.SetupLegendStone(stoneData, playerCard);

            if (selectionManager != null)
                selectionManager.RegisterCardHolder(holder);
            return;
        }
    }

    /// <summary>
    /// Find CardData in registry by card ID
    /// </summary>
    private CardData GetCardDataById(string cardId)
    {
        if (deckRegistry == null)
            deckRegistry = Resources.Load<DeckRegistry>("DeckRegistry");

        if (deckRegistry == null)
        {
            return null;
        }

        return deckRegistry.GetCardById(cardId);
    }

    /// <summary>
    /// Find CastingLegendData by ID
    /// </summary>
    private CastingLegendData GetGuardianById(string guardianId)
    {
        if (deckRegistry == null)
            deckRegistry = Resources.Load<DeckRegistry>("DeckRegistry");

        if (deckRegistry == null)
        {
            return null;
        }

        return deckRegistry.GetCastingLegendById(guardianId);
    }

    /// <summary>
    /// Find LegendStoneData by ID
    /// </summary>
    private LegendStoneData GetLegendStoneById(string stoneId)
    {
        if (deckRegistry == null)
            deckRegistry = Resources.Load<DeckRegistry>("DeckRegistry");

        if (deckRegistry == null)
        {
            return null;
        }

        return deckRegistry.GetLegendStoneById(stoneId);
    }

    /// <summary>
    /// Find CardData in registry by card NAME (not registry_name)
    /// </summary>
    private CardData GetCardDataByName(string cardName)
    {
        if (deckRegistry == null || deckRegistry.allCards == null)
        {
            return null;
        }

        foreach (var cardData in deckRegistry.allCards)
        {
            if (cardData.cardName == cardName)
                return cardData;
        }

        return null;
    }

    /// <summary>
    /// Find CastingLegendData by NAME (not registry_name)
    /// </summary>
    private CastingLegendData GetGuardianByName(string guardianName)
    {
        if (deckRegistry == null || deckRegistry.allGuardians == null)
            return null;

        foreach (var guardian in deckRegistry.allGuardians)
        {
            if (guardian.legendName == guardianName)
                return guardian;
        }

        return null;
    }

    /// <summary>
    /// Find LegendStoneData by NAME (not registry_name)
    /// </summary>
    private LegendStoneData GetLegendStoneByName(string stoneName)
    {
        if (deckRegistry == null || deckRegistry.allLegendStones == null)
            return null;

        foreach (var stone in deckRegistry.allLegendStones)
        {
            if (stone.stoneName == stoneName)
                return stone;
        }

        return null;
    }

    /// <summary>
    /// Populate all cards from player data
    /// </summary>

    /// <summary>
    /// Find CardData in registry by registry_name
    /// </summary>
    private CardData GetCardDataByRegistry(string registryName)
    {
        if (deckRegistry == null || deckRegistry.allCards == null)
        {
            return null;
        }

        foreach (var cardData in deckRegistry.allCards)
        {
            if (cardData.cardName == registryName)
            {
                return cardData;
            }
        }

        return null;
    }

    /// <summary>
    /// Find CastingLegendData in registry by registry_name (from allCastingLegends)
    /// </summary>
    private CastingLegendData GetCastingLegendByRegistry(string registryName)
    {
        if (deckRegistry == null || deckRegistry.allGuardians == null)
        {
            return null;
        }

        foreach (var legendData in deckRegistry.allGuardians)
        {
            if (legendData.legendName == registryName)
            {
                return legendData;
            }
        }

        return null;
    }

    /// <summary>
    /// Find Guardian (CastingLegendData) in registry by registry_name (from allGuardians)
    /// </summary>
    private CastingLegendData GetGuardianByRegistry(string registryName)
    {
        if (deckRegistry == null || deckRegistry.allGuardians == null)
        {
            return null;
        }

        foreach (var guardianData in deckRegistry.allGuardians)
        {
            if (guardianData.legendName == registryName)
            {
                return guardianData;
            }
        }

        return null;
    }

    /// <summary>
    /// Find LegendStoneData in registry by registry_name
    /// </summary>
    private LegendStoneData GetLegendStoneByRegistry(string registryName)
    {
        if (deckRegistry == null || deckRegistry.allLegendStones == null)
        {
            return null;
        }

        foreach (var stoneData in deckRegistry.allLegendStones)
        {
            if (stoneData.stoneName == registryName)
            {
                return stoneData;
            }
        }

        return null;
    }
}
