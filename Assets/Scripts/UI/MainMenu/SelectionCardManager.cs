using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages selected cards during the card selection screen
/// Ensures cards can't be in 2 places at once
/// Routes guardians and legend stones to specific containers
/// Manages regular cards in rows (4 cards per row, 2 rows max)
/// </summary>
public class SelectionCardManager : MonoBehaviour
{
    public static SelectionCardManager Instance { get; private set; }

    [Header("Container References")]
    [SerializeField] private Transform regularCardsContainer; // Container for regular cards rows
    [SerializeField] private Transform guardianContainer; // Container for guardians (1 slot)
    [SerializeField] private Transform legendStoneContainer; // Container for legend stones (1 slot)
    [SerializeField] private GameObject cardRowPrefab; // Prefab for card rows
    [SerializeField] private GameObject cardHolderPrefab; // Prefab for card holders (to copy cards)

    private Dictionary<string, SelectionCardHolder> selectedCards = new Dictionary<string, SelectionCardHolder>();
    private Dictionary<SelectionCardHolder, SelectionCardHolder> cardCopies = new Dictionary<SelectionCardHolder, SelectionCardHolder>(); // Maps original -> copy
    private List<Transform> cardRows = new List<Transform>(); // List of row transforms
    private SelectionCardHolder guardianSlot;
    private SelectionCardHolder guardianCopy; // Copy of guardian for display
    private SelectionCardHolder legendStoneSlot;
    private SelectionCardHolder legendStoneCopy; // Copy of legend stone for display

    // Registry of all card holders by their registry name + type
    // Key: "registry_name:type" (e.g., "FireballSpell:card", "Boulder:guardian")
    private Dictionary<string, SelectionCardHolder> cardHolderRegistry = new Dictionary<string, SelectionCardHolder>();

    private const int CARDS_PER_ROW = 4;
    private const int MAX_ROWS = 2;
    private const int MAX_REGULAR_CARDS = CARDS_PER_ROW * MAX_ROWS;

    private bool _deckLoadedOnce = false;  // Guard flag to prevent loading twice

    private void Awake()
    {
        // Initialize singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Subscribe to PlayerStates events as early as possible (before they fire)

    }

    private void OnEnable()
    {
        // Reset deck load flag when scene is enabled
        _deckLoadedOnce = false;

        Debug.Log("[SelectionCardManager] OnEnable called");

        // Check if data is already loaded (happens on initial login before this scene loads)
        if (PlayerStates.Instance != null && PlayerStates.Instance.IsLoginComplete() && PlayerStates.Instance.IsActiveDeckLoaded())
        {
            Debug.Log("[SelectionCardManager] Data already loaded during Awake, populating deck now");
            PopulateDeckFromPlayerStates();
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from events when scene is disabled
        if (PlayerStates.Instance != null)
        {
            PlayerStates.Instance.OnPlayerDataLoaded -= PopulateDeckFromPlayerStates;

            Debug.Log("[SelectionCardManager] Unsubscribed from PlayerStates events");
        }
    }

    /// <summary>
    /// Called after PlayerStates is fully populated with all data
    /// Triggers deck population when it's safe to do so
    /// </summary>
    /// <summary>
    /// Called when active deck is loaded or changed (e.g., after saving a new deck)
    /// </summary>

    private void PopulateDeckFromPlayerStates()
    {
        if (PlayerStates.Instance == null)
        {
            Debug.LogError("[SelectionCardManager] PlayerStates is null!");
            return;
        }

        Debug.Log($"[SelectionCardManager] PopulateDeckFromPlayerStates called. IsActiveDeckLoaded: {PlayerStates.Instance.IsActiveDeckLoaded()}, _deckLoadedOnce: {_deckLoadedOnce}");

        if (PlayerStates.Instance.IsActiveDeckLoaded())
        {
            PlayFabBackendManager.SavedDeck? activeDeck = PlayerStates.Instance.ActiveDeck;
            Debug.Log($"[SelectionCardManager] Active deck value: {(activeDeck != null ? activeDeck.Value.DeckName : "null")}");

            if (activeDeck != null && !_deckLoadedOnce)
            {
                _deckLoadedOnce = true;
                Debug.Log($"[SelectionCardManager] PopulateDeckFromPlayerStates: Found active deck '{activeDeck.Value.DeckName}' with {activeDeck.Value.Cards.Count} cards, waiting for card holders");
                StartCoroutine(WaitForCardHoldersAndSelect(activeDeck.Value));
            }
            else if (activeDeck == null)
            {
                Debug.LogWarning("[SelectionCardManager] Active deck is null even though IsActiveDeckLoaded returned true!");
            }
            else if (_deckLoadedOnce)
            {
                Debug.Log("[SelectionCardManager] Deck already loaded once, skipping");
            }
        }
        else
        {
            Debug.LogWarning("[SelectionCardManager] Active deck not loaded yet!");
        }
    }

    /// <summary>
    /// Wait for card holders to be populated in the registry, then select deck cards
    /// </summary>
    private System.Collections.IEnumerator WaitForCardHoldersAndSelect(PlayFabBackendManager.SavedDeck activeDeck)
    {
        // Wait for card holders to be registered
        float timeout = 5f;
        float elapsed = 0f;

        while (cardHolderRegistry.Count == 0 && elapsed < timeout)
        {
            Debug.Log($"[SelectionCardManager] Waiting for card holders to populate... ({elapsed:F1}s)");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (cardHolderRegistry.Count == 0)
        {
            Debug.LogError("[SelectionCardManager] Timeout waiting for card holders! Registry is still empty");
            yield break;
        }

        Debug.Log($"[SelectionCardManager] Card holders populated! Registry has {cardHolderRegistry.Count} entries. Starting selection...");

        // Now select the deck cards
        yield return SelectDeckCardsAfterDelay(activeDeck, 0.2f);
    }

    /// <summary>
    /// Coroutine to select deck cards after a delay to ensure holders are initialized
    /// </summary>
    private System.Collections.IEnumerator SelectDeckCardsAfterDelay(PlayFabBackendManager.SavedDeck activeDeck, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        // Verify PlayerStates is ready
        if (!PlayerStates.Instance.IsLoginComplete())
        {
            Debug.Log("[SelectionCardManager] PlayerStates login not yet complete, waiting...");
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log($"[SelectionCardManager] Starting deck auto-selection after {delaySeconds}s delay");

        // Auto-select all cards from the active deck
        ClearAllSelections();

        int selectedCount = 0;
        foreach (var cardDto in activeDeck.Cards)
        {
            Debug.Log($"[SelectionCardManager] Attempting to select card: {cardDto.id} (type: {cardDto.card_type})");

            // Find and select the card based on its ID and type
            if (SelectCardByIdAndType(cardDto.id, cardDto.card_type))
            {
                selectedCount++;
            }
        }

        Debug.Log($"[SelectionCardManager] Active deck auto-loaded: selected {selectedCount}/{activeDeck.Cards.Count} cards");
    }

    /// <summary>
    /// Clear all current selections (for loading a new deck)
    /// </summary>
    private void ClearAllSelections()
    {
        // Deselect all regular cards
        List<string> cardsToRemove = new List<string>(selectedCards.Keys);
        foreach (var cardId in cardsToRemove)
        {
            var holder = selectedCards[cardId];
            if (holder != null)
            {
                DeselectCard(holder, cardId);
            }
        }

        // Deselect guardian
        if (guardianSlot != null)
        {
            string guardianId = GetCardIdFromHolder(guardianSlot);
            DeselectCard(guardianSlot, guardianId);
        }

        // Deselect legend stone
        if (legendStoneSlot != null)
        {
            string stoneId = GetCardIdFromHolder(legendStoneSlot);
            DeselectCard(legendStoneSlot, stoneId);
        }
    }

    /// <summary>
    /// Select a card by its ID and type (used for auto-loading saved deck)
    /// Returns true if card was found and selected, false otherwise
    /// </summary>
    private bool SelectCardByIdAndType(string cardId, string cardType)
    {
        Debug.Log($"[SelectionCardManager] Looking for card: {cardId} of type: {cardType}, registry has {cardHolderRegistry.Count} entries");

        // Search through all card holders to find the one matching this ID and type
        foreach (var kvp in cardHolderRegistry)
        {
            var holder = kvp.Value;
            if (holder == null) continue;

            string holderId = GetCardIdFromHolder(holder);


            Debug.Log($"[SelectionCardManager]   Comparing: {holderId} vs {cardId}");

            if (holderId.Equals(cardId, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[SelectionCardManager] ✓ Found matching holder for {cardId}, selecting it");
                object cardData = holder.GetPublicCardData();
                SelectCard(holder, cardData);
                return true;
            }
        }

        Debug.LogWarning($"[SelectionCardManager] ✗ Could not find card holder for {cardId} of type {cardType}. Searched {cardHolderRegistry.Count} holders");
        return false;
    }

    /// <summary>
    /// Get card ID from a holder
    /// </summary>
    private string GetCardIdFromHolder(SelectionCardHolder holder)
    {
        object cardData = holder.GetPublicCardData();
        string cardType = holder.GetCardType();

        if (cardType == "guardian" && cardData is CastingLegendData guardianData)
            return guardianData.id;
        else if (cardType == "legendstone" && cardData is LegendStoneData stoneData)
            return stoneData.id;
        else if (cardType == "card" && cardData is CardData cardData_obj)
            return cardData_obj.id;

        return "Unknown";
    }

    public bool SelectCard(SelectionCardHolder holder, object cardData)
    {
        string cardType = holder.GetCardType(); // Get type from holder instead of cardData
        string cardId = GetCardIdFromType(holder, cardType); // Use holder's type to get ID

        Debug.Log($"[SelectionCardManager] SelectCard called - Type: {cardType}, ID: {cardId}, CardData: {cardData?.GetType().Name ?? "null"}");

        if (string.IsNullOrEmpty(cardId) || cardId == "Unknown")
        {
            Debug.LogError($"[SelectionCardManager] Could not get valid ID for card! Type: {cardType}, CardData: {cardData}");
            return false;
        }

        if (selectedCards.ContainsKey(cardId))
        {
            Debug.LogWarning($"[SelectionCardManager] Card {cardId} is already selected!");
            return false;
        }

        // Handle special containers for guardian and legend stone
        if (cardType == "guardian")
        {
            if (guardianSlot != null)
            {
                Debug.LogWarning($"[SelectionCardManager] Guardian slot already filled!");
                return false;
            }
            guardianSlot = holder;
            // Create display copy for guardian container
            if (guardianContainer != null && cardHolderPrefab != null)
            {
                guardianCopy = CreateGuardianCopy(holder, guardianContainer);
                cardCopies[holder] = guardianCopy;
                holder.SetCardGreyedOut(true);
            }
            Debug.Log($"[SelectionCardManager] Guardian selected: {cardId}");
            return true;
        }
        else if (cardType == "legendstone")
        {
            if (legendStoneSlot != null)
            {
                Debug.LogWarning($"[SelectionCardManager] Legend Stone slot already filled!");
                return false;
            }
            legendStoneSlot = holder;
            // Create display copy for legend stone container
            if (legendStoneContainer != null && cardHolderPrefab != null)
            {
                legendStoneCopy = CreateLegendStoneCopy(holder, legendStoneContainer);
                cardCopies[holder] = legendStoneCopy;
                holder.SetCardGreyedOut(true);
            }
            Debug.Log($"[SelectionCardManager] Legend Stone selected: {cardId}");
            return true;
        }

        else
        {
            // Regular cards - max 8 cards
            int totalCards = selectedCards.Count;
            int maxCards = MAX_REGULAR_CARDS;

            if (totalCards >= maxCards)
            {
                Debug.LogWarning($"[SelectionCardManager] All card slots are full! (Max {maxCards})");
                return false;
            }

            selectedCards.Add(cardId, holder);
            // Grey out the card in all-cards container
            holder.SetCardGreyedOut(true);
            AddCardToRow(holder); // Add copy to selected rows
            Debug.Log($"[SelectionCardManager] Card selected: {cardId}. Regular cards: {selectedCards.Count}/{maxCards}");
            return true;
        }
    }

    private SelectionCardHolder CreateGuardianCopy(SelectionCardHolder originalHolder, Transform targetContainer)
    {
        if (targetContainer == null || cardHolderPrefab == null)
            return null;

        // Create a copy of the card holder
        GameObject cardHolderCopy = Instantiate(cardHolderPrefab, targetContainer);
        SelectionCardHolder copiedHolder = cardHolderCopy.GetComponent<SelectionCardHolder>();

        if (copiedHolder != null)
        {
            // Copy the card data
            copiedHolder.CopyFrom(originalHolder);

            // Position at center
            RectTransform rectTransform = copiedHolder.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
            }

            Debug.Log($"[SelectionCardManager] Created guardian copy");
        }

        return copiedHolder;
    }

    private SelectionCardHolder CreateLegendStoneCopy(SelectionCardHolder originalHolder, Transform targetContainer)
    {
        if (targetContainer == null || cardHolderPrefab == null)
            return null;

        // Create a copy of the card holder
        GameObject cardHolderCopy = Instantiate(cardHolderPrefab, targetContainer);
        SelectionCardHolder copiedHolder = cardHolderCopy.GetComponent<SelectionCardHolder>();

        if (copiedHolder != null)
        {
            // Copy the card data
            copiedHolder.CopyFrom(originalHolder);

            // Position at center
            RectTransform rectTransform = copiedHolder.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
            }

            Debug.Log($"[SelectionCardManager] Created legend stone copy");
        }

        return copiedHolder;
    }
    private void AddCardToRow(SelectionCardHolder originalHolder)
    {
        if (regularCardsContainer == null || cardRowPrefab == null || cardHolderPrefab == null)
        {
            Debug.LogError("[SelectionCardManager] regularCardsContainer, cardRowPrefab, or cardHolderPrefab not assigned!");
            return;
        }

        // Find the row that needs the card
        Transform targetRow = null;

        // Check existing rows for space
        for (int i = 0; i < cardRows.Count; i++)
        {
            int rowCount = cardRows[i].childCount;
            Debug.Log($"[SelectionCardManager] Row {i + 1} has {rowCount} cards (max {CARDS_PER_ROW})");

            if (rowCount < CARDS_PER_ROW)
            {
                targetRow = cardRows[i];
                Debug.Log($"[SelectionCardManager] Using existing row {i + 1}");
                break;
            }
        }

        // If no existing row has space, create a new one
        if (targetRow == null)
        {
            if (cardRows.Count < MAX_ROWS)
            {
                // Create new row
                GameObject newRowObj = Instantiate(cardRowPrefab, regularCardsContainer);
                cardRows.Add(newRowObj.transform);
                targetRow = newRowObj.transform;
                Debug.Log($"[SelectionCardManager] Created new row {cardRows.Count}");
            }
            else
            {
                Debug.LogError($"[SelectionCardManager] No available rows for card! Current rows: {cardRows.Count}/{MAX_ROWS}, Total cards: {selectedCards.Count}/{MAX_REGULAR_CARDS}");
                return;
            }
        }

        // Create a copy of the card holder for the selected area
        GameObject cardHolderCopy = Instantiate(cardHolderPrefab, targetRow);
        SelectionCardHolder copiedHolder = cardHolderCopy.GetComponent<SelectionCardHolder>();

        // Copy the card data to the new holder
        if (copiedHolder != null && originalHolder != null)
        {
            string cardType = originalHolder.GetCardType();

            // Use reflection or public method to copy card data
            if (cardType == "card")
            {
                // Get the card data through a method we'll add to SelectionCardHolder
                copiedHolder.CopyFrom(originalHolder);
            }

            // Track the copy
            cardCopies[originalHolder] = copiedHolder;
        }

        Debug.Log($"[SelectionCardManager] Card copy added to row {cardRows.IndexOf(targetRow) + 1}, row now has {targetRow.childCount} cards");
    }

    public void DeselectCard(SelectionCardHolder holder, string cardId)
    {
        string cardType = holder.GetCardType();

        if (cardType == "guardian")
        {
            if (guardianSlot == holder)
            {
                guardianSlot = null;
                // Destroy the copy
                if (guardianCopy != null)
                {
                    Destroy(guardianCopy.gameObject);
                    guardianCopy = null;
                }
                cardCopies.Remove(holder);
                Debug.Log($"[SelectionCardManager] Guardian deselected: {cardId}");
            }
        }
        else if (cardType == "legendstone")
        {
            if (legendStoneSlot == holder)
            {
                legendStoneSlot = null;
                // Destroy the copy
                if (legendStoneCopy != null)
                {
                    Destroy(legendStoneCopy.gameObject);
                    legendStoneCopy = null;
                }
                cardCopies.Remove(holder);
                Debug.Log($"[SelectionCardManager] Legend Stone deselected: {cardId}");
            }
        }
        else
        {
            if (selectedCards.ContainsKey(cardId))
            {
                selectedCards.Remove(cardId);
                // Remove grey from the card in all-cards container
                holder.SetCardGreyedOut(false);

                // Find and destroy the copy
                if (cardCopies.ContainsKey(holder))
                {
                    SelectionCardHolder copy = cardCopies[holder];
                    cardCopies.Remove(holder);
                    if (copy != null)
                    {
                        Destroy(copy.gameObject);
                        Debug.Log($"[SelectionCardManager] Destroyed copy of {cardId}");
                    }
                }

                // Cleanup rows if needed
                RemoveCardFromRows();

                Debug.Log($"[SelectionCardManager] Card deselected: {cardId}. Regular cards: {selectedCards.Count}/{MAX_REGULAR_CARDS}");
            }
        }
    }

    private void RemoveCardFromRows()
    {
        // Remove the last card from rows and rebalance if needed
        if (cardRows.Count > 0)
        {
            // Find last card in the last row
            Transform lastRow = cardRows[cardRows.Count - 1];
            if (lastRow.childCount > 0)
            {
                Transform lastCard = lastRow.GetChild(lastRow.childCount - 1);
                Destroy(lastCard.gameObject);
                Debug.Log($"[SelectionCardManager] Removed card from row {cardRows.Count}");

                // Remove empty last row
                if (lastRow.childCount == 0)
                {
                    Destroy(lastRow.gameObject);
                    cardRows.RemoveAt(cardRows.Count - 1);
                    Debug.Log($"[SelectionCardManager] Removed empty row");
                }
            }
        }
    }

    public bool IsCardAlreadySelected(string cardId)
    {
        return selectedCards.ContainsKey(cardId);
    }

    public bool IsGuardianSelected()
    {
        return guardianSlot != null;
    }

    public bool IsLegendStoneSelected()
    {
        return legendStoneSlot != null;
    }

    public Dictionary<string, SelectionCardHolder> GetSelectedRegularCards()
    {
        return new Dictionary<string, SelectionCardHolder>(selectedCards);
    }

    public SelectionCardHolder GetGuardian()
    {
        return guardianSlot;
    }

    public SelectionCardHolder GetLegendStone()
    {
        return legendStoneSlot;
    }

    public void ClearSelection()
    {
        selectedCards.Clear();
        cardCopies.Clear();
        guardianSlot = null;
        legendStoneSlot = null;
        Debug.Log("[SelectionCardManager] Selection cleared");
    }

    /// <summary>
    /// Register a card holder in the registry for fast lookup
    /// Call this when card holders are created/populated
    /// </summary>
    public void RegisterCardHolder(SelectionCardHolder holder)
    {
        if (holder == null)
            return;

        string registryName = GetRegistryNameFromHolder(holder);
        string cardType = holder.GetCardType();
        string key = $"{registryName}:{cardType}";

        cardHolderRegistry[key] = holder;
        Debug.Log($"[SelectionCardManager] Registered card holder: {key}");
    }

    /// <summary>
    /// Extract registry name from a card holder
    /// </summary>
    private string GetRegistryNameFromHolder(SelectionCardHolder holder)
    {
        if (holder == null)
            return null;

        string cardType = holder.GetCardType();
        object cardData = holder.GetPublicCardData();

        if (cardType == "card" && cardData is CardData cardDataObj)
            return cardDataObj.cardName;
        else if (cardType == "legend" && cardData is CastingLegendData legendData)
            return legendData.legendName;
        else if (cardType == "guardian" && cardData is CastingLegendData guardianData)
            return guardianData.legendName;
        else if (cardType == "legendstone" && cardData is LegendStoneData stoneData)
            return stoneData.stoneName;

        return null;
    }

    /// <summary>
    /// Auto-select a card by registry name (as if select button was pressed)
    /// Used to populate the deck from saved/loaded data
    /// </summary>
    public bool AutoSelectCardByRegistryName(string registryName, string cardType)
    {
        Debug.Log($"[SelectionCardManager] AutoSelect called - Registry: {registryName}, Type: {cardType}");

        // Find the card holder with this registry name
        SelectionCardHolder targetHolder = FindCardHolderByRegistryName(registryName, cardType);
        if (targetHolder == null)
        {
            Debug.LogError($"[SelectionCardManager] AutoSelect failed - Card not found: {registryName} ({cardType})");
            return false;
        }

        // Call SelectCard to select it (simulates button press)
        bool success = SelectCard(targetHolder, targetHolder.GetPublicCardData());
        if (success)
        {
            // Mark as selected visually
            targetHolder.SetCardGreyedOut(true);
            Debug.Log($"[SelectionCardManager] AutoSelect succeeded: {registryName}");
        }
        else
        {
            Debug.LogError($"[SelectionCardManager] AutoSelect SelectCard failed: {registryName}");
        }

        return success;
    }

    /// <summary>
    /// Find a card holder UI element by its registry name and type
    /// First checks the registry, then searches the scene if not found
    /// </summary>
    private SelectionCardHolder FindCardHolderByRegistryName(string registryName, string cardType)
    {
        // First try to find in registry (fast path)
        string key = $"{registryName}:{cardType}";
        if (cardHolderRegistry.TryGetValue(key, out SelectionCardHolder holder))
        {
            Debug.Log($"[SelectionCardManager] Found card holder in registry: {key}");
            return holder;
        }

        Debug.LogWarning($"[SelectionCardManager] Card holder not in registry: {key}. Searching scene...");

        // Fallback: search all card holders in the scene
        SelectionCardHolder[] allHolders = FindObjectsByType<SelectionCardHolder>(FindObjectsSortMode.None);

        foreach (SelectionCardHolder candidate in allHolders)
        {
            // Check if this holder matches the registry name and type
            if (candidate.GetCardType() == cardType)
            {
                object cardData = candidate.GetPublicCardData();

                // Check each card type's registry name match
                if (cardType == "card" && cardData is CardData cardDataObj)
                {
                    if (cardDataObj.cardName == registryName)
                        return candidate;
                }
                else if (cardType == "legend" && cardData is CastingLegendData legendData)
                {
                    if (legendData.legendName == registryName)
                        return candidate;
                }
                else if (cardType == "guardian" && cardData is CastingLegendData guardianData)
                {
                    if (guardianData.legendName == registryName)
                        return candidate;
                }
                else if (cardType == "legendstone" && cardData is LegendStoneData stoneData)
                {
                    if (stoneData.stoneName == registryName)
                        return candidate;
                }
            }
        }

        return null;
    }

    public int GetRegularCardCount()
    {
        return selectedCards.Count;
    }

    public int GetMaxRegularCards()
    {
        return MAX_REGULAR_CARDS; // 8 cards total
    }

    /// <summary>
    /// Get the IDs of all selected cards (regular cards only, for deck submission)
    /// </summary>
    public List<string> GetSelectedCardIds()
    {
        List<string> cardIds = new List<string>();
        foreach (var cardId in selectedCards.Keys)
        {
            cardIds.Add(cardId);
        }
        return cardIds;
    }

    /// <summary>
    /// Get guardian ID if selected
    /// </summary>
    public string GetSelectedGuardianId()
    {
        Debug.Log($"[SelectionCardManager] GetSelectedGuardianId called. guardianSlot: {(guardianSlot != null ? "SET" : "NULL")}");

        if (guardianSlot != null)
        {
            string cardType = guardianSlot.GetCardType();
            object cardData = guardianSlot.GetPublicCardData();

            Debug.Log($"[SelectionCardManager] Guardian slot cardType: {cardType}, cardData type: {cardData?.GetType().Name ?? "null"}");

            if (cardType == "guardian" && cardData is CastingLegendData legendData)
            {
                Debug.Log($"[SelectionCardManager] Guardian found: {legendData.id}");
                return legendData.id;
            }

            Debug.LogWarning($"[SelectionCardManager] Guardian slot has wrong type: {cardType}");
            return null;
        }

        Debug.LogWarning($"[SelectionCardManager] No guardian selected!");
        return null;
    }

    /// <summary>
    /// Get legend stone ID if selected
    /// </summary>
    public string GetSelectedLegendStoneId()
    {
        if (legendStoneSlot != null)
        {
            string cardType = legendStoneSlot.GetCardType();
            object cardData = legendStoneSlot.GetPublicCardData();

            if (cardType == "legendstone" && cardData is LegendStoneData stoneData)
            {
                return stoneData.id;
            }

            Debug.LogWarning($"[SelectionCardManager] Legend stone slot has wrong type: {cardType}");
            return null;
        }
        return null;
    }

    /// <summary>
    /// Submit the selected deck to the backend
    /// Supports both old BackendManager and new PlayFabBackendManager
    /// </summary>
    public void SubmitDeck()
    {
        PlayFabBackendManager playfabBackend = PlayFabBackendManager.Instance;

        if (playfabBackend == null)
        {
            Debug.LogError("[SelectionCardManager] PlayFabBackendManager not found!");
            return;
        }

        // Get the IDs
        List<string> cardIds = GetSelectedCardIds();
        string guardianId = GetSelectedGuardianId();
        string legendStoneId = GetSelectedLegendStoneId();

        Debug.Log($"[SelectionCardManager] Selected deck:");
        Debug.Log($"  Cards: {string.Join(", ", cardIds)}");
        Debug.Log($"  Guardian: {guardianId}");
        Debug.Log($"  Legend Stone: {legendStoneId}");

        // Validate deck
        if (cardIds.Count == 0)
        {
            Debug.LogWarning("[SelectionCardManager] No cards selected!");
            return;
        }

        // Build CardDataDto list for SaveActiveDeck
        List<DeckPayload.CardDataDto> cardDtos = new List<DeckPayload.CardDataDto>();

        // Add regular cards
        foreach (var cardId in cardIds)
        {
            cardDtos.Add(new DeckPayload.CardDataDto
            {
                id = cardId,
                registry_name = cardId,
                card_type = "card",
                level = 1
            });
        }

        // Add guardian
        if (!string.IsNullOrEmpty(guardianId))
        {
            cardDtos.Add(new DeckPayload.CardDataDto
            {
                id = guardianId,
                registry_name = guardianId,
                card_type = "guardian",
                level = 1
            });
        }

        // Add legend stone
        if (!string.IsNullOrEmpty(legendStoneId))
        {
            cardDtos.Add(new DeckPayload.CardDataDto
            {
                id = legendStoneId,
                registry_name = legendStoneId,
                card_type = "legend_stone",
                level = 1
            });
        }

        Debug.Log($"[SelectionCardManager] Saving active deck with {cardDtos.Count} cards");

        // Save as active deck (the one selected for the match)
        playfabBackend.SaveActiveDeck("My Deck", cardDtos, (success) =>
        {
            if (success)
            {
                Debug.Log("[SelectionCardManager] Active deck saved successfully to PlayFab!");
                SubmitDeckSuccess();
            }
            else
            {
                Debug.LogError("[SelectionCardManager] Failed to save active deck to PlayFab!");
                SubmitDeckFailed();
            }
        });
    }

    private void SubmitDeckSuccess()
    {
        Debug.Log("[SelectionCardManager] Deck submitted successfully");
        // Add any UI feedback here
    }

    private void SubmitDeckFailed()
    {
        Debug.LogError("[SelectionCardManager] Deck submission failed");
        // Add any UI feedback here
    }

    /// <summary>
    /// Start matchmaking with the currently selected deck
    /// </summary>
    public void StartMatchmaking()
    {
        Debug.Log("[SelectionCardManager] Starting matchmaking");

        // Start matchmaking (backend will use player's active deck from DB)
        //    if (MatchmakingManager.Instance != null)
        //   {
        //         MatchmakingManager.Instance.StartMatchmaking();
        //     }
        //     else
        //      {
        //          Debug.LogError("[SelectionCardManager] MatchmakingManager not found!");
        //}
    }

    /// <summary>
    /// Get card ID using the holder's known type instead of guessing from object type
    /// </summary>
    private string GetCardIdFromType(SelectionCardHolder holder, string cardType)
    {
        object cardData = holder.GetPublicCardData();

        if (cardType == "guardian" && cardData is CastingLegendData guardianData)
            return guardianData.id;
        else if (cardType == "legendstone" && cardData is LegendStoneData stoneData)
            return stoneData.id;
        else if (cardType == "card" && cardData is CardData cardData_obj)
            return cardData_obj.id;
        else
        {
            Debug.LogError($"[SelectionCardManager] GetCardIdFromType: Type mismatch! CardType: {cardType}, CardData: {cardData?.GetType().Name ?? "null"}");
            return "Unknown";
        }
    }

    private string GetCardType(object cardData)
    {
        // This method is only called with cardData from SelectionCardHolder
        // We need to determine the type based on what the holder knows
        // Since guardians and legends are both CastingLegendData, we can't distinguish here
        // Instead, we should use the holder's cardType

        if (cardData is CardData)
            return "card";
        else if (cardData is CastingLegendData)
            return "legend"; // This will be overridden by caller
        else if (cardData is LegendStoneData)
            return "legendstone";
        else
            return "unknown";
    }
}
