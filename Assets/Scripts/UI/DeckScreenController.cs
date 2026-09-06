using UnityEngine;
using UnityEngine.UI;
using TMPro; // If you use TextMeshPro for button text
using System.Collections.Generic;
using System.Linq; // Required for Select

public class DeckScreenController : MonoBehaviour
{
    [Header("UI References - Selected Items Display")]
    public Transform selectedCardsContainer; // Assign the GameObject with the LayoutGroup
    public GameObject selectedCardDisplayPrefab; // Assign your new prefab for displaying a single selected card
    public Image selectedGuardianSlotImage; // Added back
    public TextMeshProUGUI selectedGuardianSlotName; // Added back
    public Image selectedStoneSlotImage; // Added back
    public TextMeshProUGUI selectedStoneSlotName; // Added back

    [Header("UI References - Item Browsing Lists (Scroll Views)")]
    public Transform allCardsContainer; // Content parent in a ScrollView
    public Transform allGuardiansContainer; // Added back
    public Transform allStonesContainer; // Added back

    [Header("UI References - Prefabs for List Items")]
    public GameObject cardListItemPrefab; // Prefab for items in allCardsContainer
    public GameObject guardianListItemPrefab; // Added back
    public GameObject stoneListItemPrefab; // Added back

    [Header("UI References - Action Buttons")]
    public Button confirmDeckButton;
    public Button backButton;

    [Header("UI References - Feedback")]
    public TextMeshProUGUI feedbackText; // For displaying messages like "Card slots full", "Deck saved"

    // Store current selections
    private List<CardData> currentSelectedCards = new List<CardData>();
    private CastingLegendData currentSelectedGuardian; // Added back
    private LegendStoneData currentSelectedLegendStone; // Added back

    private const string MainMenuScreenName = "MainMenuScreen";
    public const int MAX_SELECTED_CARDS = 8; // Made public for MainMenuController
    public const int MIN_SELECTED_CARDS = 6; // Made public for MainMenuController

    // --- Unity Lifecycle Methods ---
    void OnEnable()
    {
        InitializeDeckScreen();
    }

    void OnDisable()
    {
        if (confirmDeckButton != null) confirmDeckButton.onClick.RemoveAllListeners();
        if (backButton != null) backButton.onClick.RemoveAllListeners();
        // No specific slot listeners to remove here now, they are on prefabs

        // Make selected Guardian/Stone slots clickable for deselection (optional)
        Button guardianSlotButton = selectedGuardianSlotImage?.GetComponent<Button>();
        if (guardianSlotButton != null) guardianSlotButton.onClick.RemoveAllListeners();
        Button stoneSlotButton = selectedStoneSlotImage?.GetComponent<Button>();
        if (stoneSlotButton != null) stoneSlotButton.onClick.RemoveAllListeners();
    }

    // --- Initialization ---
    void InitializeDeckScreen()
    {
        SetupButtonListeners();
        SetupSelectedSlotDeselection(); // Add listeners for Guardian/Stone slots
        LoadAndDisplayCurrentDeck();
        ClearFeedbackText();
    }

    void SetupButtonListeners()
    {
        if (confirmDeckButton != null) confirmDeckButton.onClick.AddListener(OnConfirmDeckClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
    }

    void SetupSelectedSlotDeselection() // For Guardian and Stone selected slots
    {
        if (selectedGuardianSlotImage != null)
        {
            Button slotButton = selectedGuardianSlotImage.GetComponent<Button>();
            if (slotButton == null) slotButton = selectedGuardianSlotImage.gameObject.AddComponent<Button>();
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnGuardianSlotClicked);
        }
        if (selectedStoneSlotImage != null)
        {
            Button slotButton = selectedStoneSlotImage.GetComponent<Button>();
            if (slotButton == null) slotButton = selectedStoneSlotImage.gameObject.AddComponent<Button>();
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnStoneSlotClicked);
        }
    }

    void ClearFeedbackText()
    {
        if (feedbackText != null) feedbackText.text = "";
    }

    void ShowFeedback(string message, bool isError = false)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isError ? Color.red : Color.white;
        }
    }

    // --- Deck Loading and Display ---
    void LoadAndDisplayCurrentDeck()
    {
        PlayerDeckData savedDeck = DeckManager.Instance?.GetSelectedDeck();
        if (savedDeck != null)
        {
            currentSelectedCards = new List<CardData>(savedDeck.selectedCards ?? new List<CardData>());
            currentSelectedGuardian = savedDeck.selectedCastingLegend;
            currentSelectedLegendStone = savedDeck.selectedLegendStone;
            Debug.Log($"[DeckScreen] Loaded deck. Cards: {currentSelectedCards.Count}, Guardian: {currentSelectedGuardian?.legendName}, Stone: {currentSelectedLegendStone?.stoneName}");
        }
        else
        {
            currentSelectedCards.Clear();
            currentSelectedGuardian = null;
            currentSelectedLegendStone = null;
            Debug.Log("[DeckScreen] No saved deck. Starting fresh.");
        }
        RefreshAllSelectedDisplays();
        PopulateAllBrowseLists();
    }

    void RefreshAllSelectedDisplays()
    {
        UpdateSelectedCardsDisplay();
        UpdateSelectedGuardianDisplay();
        UpdateSelectedLegendStoneDisplay();
    }

    void UpdateSelectedCardsDisplay()
    {
        if (selectedCardsContainer == null || selectedCardDisplayPrefab == null)
        {
            Debug.LogError("[DeckScreen] selectedCardsContainer or selectedCardDisplayPrefab is not assigned!");
            return;
        }

        // Clear previous selected card instances
        foreach (Transform child in selectedCardsContainer)
        {
            Destroy(child.gameObject);
        }

        // Instantiate new prefabs for currently selected cards
        foreach (CardData card in currentSelectedCards)
        {
            if (card == null) continue;
            GameObject cardInstance = Instantiate(selectedCardDisplayPrefab, selectedCardsContainer);
            SelectedCardDisplayItem displayItem = cardInstance.GetComponent<SelectedCardDisplayItem>();
            if (displayItem != null)
            {
                displayItem.Initialize(card, OnDeselectCardFromDisplay); // Pass the callback
            }
            else
            {
                Debug.LogError($"[DeckScreen] selectedCardDisplayPrefab is missing SelectedCardDisplayItem script on its root!");
            }
        }
    }

    void UpdateSelectedGuardianDisplay()
    {
        if (selectedGuardianSlotImage != null)
        {
            selectedGuardianSlotImage.sprite = currentSelectedGuardian?.legendIcon;
            selectedGuardianSlotImage.enabled = currentSelectedGuardian != null;
            Button slotButton = selectedGuardianSlotImage.GetComponent<Button>();
            if (slotButton != null) slotButton.interactable = currentSelectedGuardian != null;
        }
        if (selectedGuardianSlotName != null)
        {
            selectedGuardianSlotName.text = currentSelectedGuardian?.legendName ?? "None";
            selectedGuardianSlotName.enabled = currentSelectedGuardian != null;
        }
    }

    void UpdateSelectedLegendStoneDisplay()
    {
        if (selectedStoneSlotImage != null)
        {
            selectedStoneSlotImage.sprite = currentSelectedLegendStone?.stoneIcon;
            selectedStoneSlotImage.enabled = currentSelectedLegendStone != null;
            Button slotButton = selectedStoneSlotImage.GetComponent<Button>();
            if (slotButton != null) slotButton.interactable = currentSelectedLegendStone != null;
        }
        if (selectedStoneSlotName != null)
        {
            selectedStoneSlotName.text = currentSelectedLegendStone?.stoneName ?? "None";
            selectedStoneSlotName.enabled = currentSelectedLegendStone != null;
        }
    }

    void OnDeselectCardFromDisplay(CardData cardToDeselect)
    {
        if (cardToDeselect != null && currentSelectedCards.Contains(cardToDeselect))
        {
            currentSelectedCards.Remove(cardToDeselect);
            RefreshAllSelectedDisplays();
            UpdateBrowseListItemVisuals();
            ShowFeedback($"Removed {cardToDeselect.cardName}", false);
        }
    }

    void OnGuardianSlotClicked() // New: For deselecting Guardian by clicking its slot
    {
        if (currentSelectedGuardian != null)
        {
            ShowFeedback($"Deselected Guardian: {currentSelectedGuardian.legendName}", false);
            currentSelectedGuardian = null;
            RefreshAllSelectedDisplays();
            UpdateBrowseListItemVisuals();
        }
    }

    void OnStoneSlotClicked() // New: For deselecting Stone by clicking its slot
    {
        if (currentSelectedLegendStone != null)
        {
            ShowFeedback($"Deselected Stone: {currentSelectedLegendStone.stoneName}", false);
            currentSelectedLegendStone = null;
            RefreshAllSelectedDisplays();
            UpdateBrowseListItemVisuals();
        }
    }

    // --- Populating Browse Lists ---
    void PopulateAllBrowseLists()
    {
        PopulateBrowseList<CardData>(allCardsContainer, DeckRegistry.Instance?.allCards.ToList(), cardListItemPrefab, HandleCardSelectionFromBrowseList);
        PopulateBrowseList<CastingLegendData>(allGuardiansContainer, DeckRegistry.Instance?.allGuardians.ToList(), guardianListItemPrefab, HandleGuardianSelectionFromBrowseList);
        PopulateBrowseList<LegendStoneData>(allStonesContainer, DeckRegistry.Instance?.allLegendStones.ToList(), stoneListItemPrefab, HandleStoneSelectionFromBrowseList);
        UpdateBrowseListItemVisuals();
    }

    void PopulateBrowseList<T>(Transform container, List<T> items, GameObject prefab, System.Action<T, GameObject> onSelectAction) where T : class
    {
        if (container == null || prefab == null || items == null)
        {
            Debug.LogWarning("[DeckScreen] Missing references for populating a browse list.");
            return;
        }
        ClearListContainer(container);

        foreach (T item in items)
        {
            if (item == null) continue;

            GameObject listItemInstance = Instantiate(prefab, container);

            string displayName = "Unknown";
            Sprite displayIcon = null;

            if (item is CardData card) { displayName = card.id; displayIcon = card.cardImage; }
            else if (item is CastingLegendData legend) { displayName = legend.id; displayIcon = legend.legendIcon; }
            else if (item is LegendStoneData stone) { displayName = stone.id; displayIcon = stone.stoneIcon; }
            else
            {
                Debug.LogWarning($"[DeckScreen] PopulateBrowseList encountered an unknown item type: {item.GetType().Name}");
                Destroy(listItemInstance);
                continue;
            }
            listItemInstance.name = $"Item_{displayName}";

            Image itemImage = listItemInstance.transform.Find("ItemIcon")?.GetComponent<Image>();
            TextMeshProUGUI itemNameText = listItemInstance.transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();

            if (itemNameText != null) itemNameText.text = displayName;
            if (itemImage != null && displayIcon != null) { itemImage.sprite = displayIcon; itemImage.enabled = true; }
            else if (itemImage != null) { itemImage.enabled = false; }

            Button itemButton = listItemInstance.GetComponent<Button>();
            if (itemButton != null)
            {
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(() => onSelectAction(item, listItemInstance));
            }
            else
            {
                Debug.LogWarning($"[DeckScreen] Prefab {prefab.name} for {typeof(T).Name} is missing a Button component at its root.");
            }
        }
    }

    void ClearListContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);
    }

    // --- Handling Selections from Browse Lists ---
    void HandleCardSelectionFromBrowseList(CardData card, GameObject buttonObj)
    {
        if (card == null) return;
        ClearFeedbackText();

        if (currentSelectedCards.Contains(card))
        {
            currentSelectedCards.Remove(card);
            ShowFeedback($"Deselected {card.cardName}", false);
        }
        else if (currentSelectedCards.Count < MAX_SELECTED_CARDS)
        {
            currentSelectedCards.Add(card);
            ShowFeedback($"Selected {card.cardName}", false);
        }
        else
        {
            ShowFeedback($"Card slots full ({MAX_SELECTED_CARDS} max). Cannot add {card.cardName}.", true);
        }
        RefreshAllSelectedDisplays();
        UpdateBrowseListItemVisuals();
    }

    void HandleGuardianSelectionFromBrowseList(CastingLegendData guardian, GameObject buttonObj) // Added back
    {
        if (guardian == null) return;
        ClearFeedbackText();
        currentSelectedGuardian = guardian;
        ShowFeedback($"Selected Guardian: {guardian.legendName}", false);
        RefreshAllSelectedDisplays();
        UpdateBrowseListItemVisuals();
    }

    void HandleStoneSelectionFromBrowseList(LegendStoneData stone, GameObject buttonObj) // Added back
    {
        if (stone == null) return;
        ClearFeedbackText();
        currentSelectedLegendStone = stone;
        ShowFeedback($"Selected Stone: {stone.stoneName}", false);
        RefreshAllSelectedDisplays();
        UpdateBrowseListItemVisuals();
    }

    void UpdateBrowseListItemVisuals()
    {
        UpdateBrowseListVisualsForContainer<CardData>(allCardsContainer, currentSelectedCards.Cast<object>().ToList(), MAX_SELECTED_CARDS, false);
        UpdateBrowseListVisualsForContainer<CastingLegendData>(allGuardiansContainer, new List<object> { currentSelectedGuardian }, 1, true);
        UpdateBrowseListVisualsForContainer<LegendStoneData>(allStonesContainer, new List<object> { currentSelectedLegendStone }, 1, true);
    }

    void UpdateBrowseListVisualsForContainer<T>(Transform container, List<object> selectedItemsAsObjects, int maxSelection, bool singleSelectionMode) where T : class
    {
        if (container == null) return;

        List<T> currentSelectionsTyped = selectedItemsAsObjects.Where(s => s != null).OfType<T>().ToList();

        foreach (Transform child in container)
        {
            Button itemButton = child.GetComponent<Button>();
            if (itemButton == null) continue;

            TextMeshProUGUI itemNameTextCmp = child.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
            if (itemNameTextCmp == null) continue;

            T itemDataInRegistry = default(T);
            if (typeof(T) == typeof(CardData)) itemDataInRegistry = DeckRegistry.Instance.allCards.FirstOrDefault(c => c.cardName == itemNameTextCmp.text) as T;
            else if (typeof(T) == typeof(CastingLegendData)) itemDataInRegistry = DeckRegistry.Instance.allGuardians.FirstOrDefault(l => l.legendName == itemNameTextCmp.text) as T;
            else if (typeof(T) == typeof(LegendStoneData)) itemDataInRegistry = DeckRegistry.Instance.allLegendStones.FirstOrDefault(s => s.stoneName == itemNameTextCmp.text) as T;
            if (itemDataInRegistry == null) continue;

            bool isSelected = currentSelectionsTyped.Contains(itemDataInRegistry);
            CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>() ?? child.gameObject.AddComponent<CanvasGroup>();

            if (isSelected)
            {
                canvasGroup.alpha = 0.5f;
                itemButton.interactable = !singleSelectionMode; // If single selection, selected item in browse list is not interactable to deselect. Click slot instead.
            }
            else
            {
                if (!singleSelectionMode && currentSelectionsTyped.Count >= maxSelection)
                {
                    canvasGroup.alpha = 0.7f;
                    itemButton.interactable = false;
                }
                else
                {
                    canvasGroup.alpha = 1.0f;
                    itemButton.interactable = true;
                }
            }
        }
    }

    // --- Action Button Handlers ---
    void OnConfirmDeckClicked()
    {
        ClearFeedbackText();
        if (currentSelectedCards.Count < MIN_SELECTED_CARDS || currentSelectedCards.Count > MAX_SELECTED_CARDS)
        {
            ShowFeedback($"Please select between {MIN_SELECTED_CARDS} and {MAX_SELECTED_CARDS} cards. You have {currentSelectedCards.Count}.", true);
            return;
        }
        if (currentSelectedGuardian == null) { ShowFeedback("Select a Guardian.", true); return; }
        if (currentSelectedLegendStone == null) { ShowFeedback("Select a Legend Stone.", true); return; }

        PlayerDeckData currentDeckData = DeckManager.Instance?.GetSelectedDeck();
        if (currentDeckData == null)
        {
            Debug.LogWarning("[DeckScreen] DeckManager.GetSelectedDeck() returned null. Creating a temporary PlayerDeckData.");
            currentDeckData = ScriptableObject.CreateInstance<PlayerDeckData>();
        }

        currentDeckData.selectedCards = new List<CardData>(currentSelectedCards);
        currentDeckData.selectedCastingLegend = currentSelectedGuardian;
        currentDeckData.selectedLegendStone = currentSelectedLegendStone;

        DeckManager.Instance?.LoadDeck(currentDeckData);

        Debug.Log($"[DeckScreen] Cards confirmed and saved to DeckManager. Count: {currentSelectedCards.Count}");
        ShowFeedback($"Selected cards saved! ({currentSelectedCards.Count})", false);

        UIManager.Instance.ShowScreen(MainMenuScreenName);
    }

    void OnBackClicked()
    {
        ClearFeedbackText();
        Debug.Log("[DeckScreen] Back button clicked. Returning to Main Menu.");
        UIManager.Instance.ShowScreen(MainMenuScreenName);
    }
}