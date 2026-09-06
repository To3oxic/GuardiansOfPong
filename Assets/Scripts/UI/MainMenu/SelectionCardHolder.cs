using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Card holder for the card selection screen
/// Handles selection, deselection, and details popup
/// </summary>
public class SelectionCardHolder : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private TextMeshProUGUI manaCostText;
    [SerializeField] private TextMeshProUGUI levelText; // Display current card level
    [SerializeField] private VerticalLayoutGroup buttonLayout;
    [SerializeField] private Button cardButton; // The existing card button
    [SerializeField] private Button selectButton;
    [SerializeField] private Button unselectButton;
    [SerializeField] private Button detailsButton;
    [SerializeField] private Button levelUpButton; // Button to level up the card

    [Header("Sprites")]
    [SerializeField] private Sprite commonBG;
    [SerializeField] private Sprite rareBG;
    [SerializeField] private Sprite epicBG;

    [Header("Visual Feedback")]
    [SerializeField] private Color selectedColor = new Color(0.7f, 1f, 0.7f, 1f);
    [SerializeField] private Color deselectedColor = Color.white;
    [SerializeField] private Color greyedOutColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private CanvasGroup buttonCanvasGroup;
    private CardData currentCardData;
    private CastingLegendData currentLegendData;
    private LegendStoneData currentStoneData;
    private bool isSelected = false;
    private string cardType; // "card", "legend", "guardian", "legendstone"

    // Reference to the selection manager
    private SelectionCardManager selectionManager;

    // Reference to the original holder (if this is a copy)
    private SelectionCardHolder originalHolder;

    private void Awake()
    {
        // Get or add CanvasGroup for button visibility
        buttonCanvasGroup = buttonLayout.GetComponent<CanvasGroup>();
        if (buttonCanvasGroup == null)
        {
            buttonCanvasGroup = buttonLayout.gameObject.AddComponent<CanvasGroup>();
        }

        // Hide buttons by default
        buttonCanvasGroup.alpha = 0;
        buttonCanvasGroup.interactable = false;
        buttonCanvasGroup.blocksRaycasts = false;

        // Setup button clicks
        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectPressed);
        if (unselectButton != null)
            unselectButton.onClick.AddListener(OnUnselectPressed);
        if (detailsButton != null)
            detailsButton.onClick.AddListener(OnDetailsPressed);
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(OnLevelUpPressed);

        // Add click listener to existing card button
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
            // Remove the default navigation to avoid unwanted interactions
            cardButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }

    private void OnEnable()
    {
        // Find the selection manager if not already set
        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<SelectionCardManager>();
        }
    }

    public void SetSelectionManager(SelectionCardManager manager)
    {
        selectionManager = manager;
    }

    /// <summary>
    /// Setup regular card (spell, unit)
    /// </summary>
    public void SetupCard(CardData data, PlayerCardOwnership playerCard = null)
    {
        currentCardData = data;
        currentLegendData = null;
        currentStoneData = null;
        cardType = "card";
        isSelected = false;

        UpdateVisuals();

        if (data == null)
        {
            cardImage.sprite = null;
            cardImage.color = new Color(0, 0, 0, 0);
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
        cardImage.color = Color.white;

        // Set mana cost
        if (manaCostText != null)
        {
            manaCostText.text = data.manaCost.ToString();
            manaCostText.gameObject.SetActive(true);
        }

        // Set card level from player collection
        if (playerCard != null)
            levelText.text = $"Lv. {playerCard.level}";

        Debug.Log($"[SelectionCardHolder] Setup card: {data.cardName}");
    }


    /// <summary>
    /// Setup guardian card (also CastingLegendData but marked as "guardian")
    /// </summary>
    public void SetupGuardian(CastingLegendData data, PlayerCardOwnership playerCard = null)
    {
        currentCardData = null;
        currentLegendData = data;
        currentStoneData = null;
        cardType = "guardian";
        isSelected = false;
        UpdateVisuals();

        if (data == null)
        {
            cardImage.sprite = null;
            cardImage.color = new Color(0, 0, 0, 0);
            return;
        }
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
        cardImage.sprite = data.legendIcon;
        cardImage.color = Color.white;

        if (manaCostText != null)
            manaCostText.gameObject.SetActive(false);
        // Set card level from player collection
        if (playerCard != null)
            levelText.text = $"Lv. {playerCard.level}";


        Debug.Log($"[SelectionCardHolder] Setup guardian: {data.legendName}");
    }

    /// <summary>
    /// Setup legend stone or guardian
    /// </summary>
    public void SetupLegendStone(LegendStoneData data, PlayerCardOwnership playerCard = null)
    {
        currentCardData = null;
        currentLegendData = null;
        currentStoneData = data;
        cardType = "legendstone";
        isSelected = false;
        UpdateVisuals();

        if (data == null)
        {
            cardImage.sprite = null;
            cardImage.color = new Color(0, 0, 0, 0);
            return;
        }
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
        cardImage.sprite = data.stoneIcon;
        cardImage.color = Color.white;

        if (manaCostText != null)
            manaCostText.gameObject.SetActive(false);
        // Set card level from player collection
        if (playerCard != null)
            levelText.text = $"Lv. {playerCard.level}";

        Debug.Log($"[SelectionCardHolder] Setup legendstone: {data.stoneName}");
    }

    private void OnCardClicked()
    {
        Debug.Log($"[SelectionCardHolder] Card clicked. Selected: {isSelected}, IsOriginal: {originalHolder == null}");

        // If this is a copy, always show unselect button
        bool isCopy = (originalHolder != null);

        if (isCopy)
        {
            // Show buttons for copy (with unselect available)
            buttonCanvasGroup.alpha = 1;
            buttonCanvasGroup.interactable = true;
            buttonCanvasGroup.blocksRaycasts = true;

            if (selectButton != null)
                selectButton.gameObject.SetActive(false);
            if (unselectButton != null)
                unselectButton.gameObject.SetActive(true);
            if (detailsButton != null)
                detailsButton.gameObject.SetActive(true);
        }
        else
        {
            // Show/hide buttons based on selection state for original
            bool shouldShow = !isSelected;
            buttonCanvasGroup.alpha = shouldShow ? 1 : 0;
            buttonCanvasGroup.interactable = shouldShow;
            buttonCanvasGroup.blocksRaycasts = shouldShow;

            // Show/hide specific buttons based on selection state
            if (isSelected)
            {
                // Show only unselect button
                if (selectButton != null)
                    selectButton.gameObject.SetActive(false);
                if (unselectButton != null)
                    unselectButton.gameObject.SetActive(true);
                if (detailsButton != null)
                    detailsButton.gameObject.SetActive(true);
            }
            else
            {
                // Show select and details buttons
                if (selectButton != null)
                    selectButton.gameObject.SetActive(true);
                if (unselectButton != null)
                    unselectButton.gameObject.SetActive(false);
                if (detailsButton != null)
                    detailsButton.gameObject.SetActive(true);
            }
        }
    }

    private void OnSelectPressed()
    {
        if (isSelected)
        {
            Debug.LogWarning("[SelectionCardHolder] Card already selected!");
            return;
        }

        Debug.Log($"[SelectionCardHolder] Select button pressed for {GetCardName()}");

        // Check if card is already in another holder
        if (selectionManager != null && selectionManager.IsCardAlreadySelected(GetCardId()))
        {
            Debug.LogWarning($"[SelectionCardHolder] Card {GetCardId()} is already selected elsewhere!");
            return;
        }

        // Try to add to selected area - only mark as selected if successful
        bool success = false;
        if (selectionManager != null)
        {
            success = selectionManager.SelectCard(this, GetCardData());
        }

        if (success)
        {
            // Only mark as selected if SelectCard succeeded
            isSelected = true;
            UpdateVisuals();

            // Update button display
            OnCardClicked();
        }
        else
        {
            Debug.LogWarning("[SelectionCardHolder] Failed to select card - container is full!");
        }
    }

    private void OnUnselectPressed()
    {
        if (!isSelected && originalHolder == null)
        {
            Debug.LogWarning("[SelectionCardHolder] Card is not selected!");
            return;
        }

        Debug.Log($"[SelectionCardHolder] Unselect button pressed for {GetCardName()}");

        // If this is a copy, unselect the original
        if (originalHolder != null)
        {
            // Call deselect on the original holder (this will delete the copy and reset the original)
            originalHolder.UnselectCard();
            return;
        }

        // Mark as deselected
        isSelected = false;
        UpdateVisuals();

        // Remove from selected area
        if (selectionManager != null)
        {
            selectionManager.DeselectCard(this, GetCardId());
        }

        // Hide buttons
        buttonCanvasGroup.alpha = 0;
        buttonCanvasGroup.interactable = false;
        buttonCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Public method to unselect this card (called from copies)
    /// </summary>
    public void UnselectCard()
    {
        OnUnselectPressed();
    }

    private void OnDetailsPressed()
    {
        Debug.Log($"[SelectionCardHolder] Details button pressed for {GetCardName()}");

        // TODO: Show details popup
        // This will be implemented later
    }

    private void UpdateVisuals()
    {
        // Change card color based on selection state
        if (isSelected)
        {
            cardImage.color = greyedOutColor;
        }
        else
        {
            cardImage.color = deselectedColor;
        }
    }

    public void SetCardGreyedOut(bool greyed)
    {
        isSelected = greyed;
        UpdateVisuals();
    }

    /// <summary>
    /// Copy card data from another holder
    /// </summary>
    public void CopyFrom(SelectionCardHolder source)
    {
        if (source == null)
            return;

        originalHolder = source; // Store reference to original
        string sourceCardType = source.GetCardType();

        if (sourceCardType == "card")
        {
            // Copy regular card data directly
            if (source.currentCardData != null)
            {
                currentCardData = source.currentCardData;
                currentLegendData = null;
                currentStoneData = null;
                cardType = "card";
                levelText.text = source.levelText.text;
                SetupCard(source.currentCardData);
            }
        }

        else if (sourceCardType == "guardian")
        {
            if (source.currentLegendData != null)
            {
                currentCardData = null;
                currentLegendData = source.currentLegendData;
                currentStoneData = null;
                cardType = "guardian";
                levelText.text = source.levelText.text;
                SetupGuardian(source.currentLegendData);
            }
        }
        else if (sourceCardType == "legendstone")
        {
            if (source.currentStoneData != null)
            {
                currentCardData = null;
                currentLegendData = null;
                currentStoneData = source.currentStoneData;
                cardType = "legendstone";
                levelText.text = source.levelText.text;
                SetupLegendStone(source.currentStoneData);
            }
        }

        // Mark as NOT selected (copy should be normal color, not greyed)
        // Buttons will show when clicked
        isSelected = false;
        UpdateVisuals();
    }

    /// <summary>
    /// Get the original holder if this is a copy
    /// </summary>
    public SelectionCardHolder GetOriginalHolder()
    {
        return originalHolder;
    }

    /// <summary>
    /// Update the level text display based on player's card ownership
    /// </summary>
    private void UpdateCardLevel(string cardId)
    {
        if (levelText == null)
            return;

        // Get the card level from player's card collection
        int cardLevel = 1; // Default level
        if (PlayFabBackendManager.Instance != null)
        {
            cardLevel = PlayFabBackendManager.Instance.GetPlayerCardLevel(cardId);
            if (cardLevel == 0) // Not owned yet, default to 1
                cardLevel = 1;
        }

        levelText.text = $"Lv. {cardLevel}";
        Debug.Log($"[SelectionCardHolder] Updated level text for {cardId}: Lv. {cardLevel}");
    }

    /// <summary>
    /// Handle level up button press - calls PlayFab level up function
    /// </summary>
    private void OnLevelUpPressed()
    {
        string cardId = GetCardId();
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogError("[SelectionCardHolder] Cannot level up - card ID is empty!");
            return;
        }

        Debug.Log($"[SelectionCardHolder] Level up button pressed for card: {cardId}");

        if (PlayFabBackendManager.Instance == null)
        {
            Debug.LogError("[SelectionCardHolder] PlayFabBackendManager not found!");
            return;
        }

        // Call PlayFab level up function
        PlayFabBackendManager.Instance.LevelUpCard(cardId, 100, (success, message) =>
        {
            if (success)
            {
                Debug.Log($"[SelectionCardHolder] Successfully leveled up card {cardId}");
                // Update the level text after successful level up
                UpdateCardLevel(cardId);
            }
            else
            {
                Debug.LogError($"[SelectionCardHolder] Failed to level up card: {message}");
            }
        });
    }

    private string GetCardName()
    {
        switch (cardType)
        {
            case "guardian":
                return currentLegendData?.legendName ?? "Unknown";

            case "legendstone":
                return currentStoneData?.stoneName ?? "Unknown";
            default:
                return currentCardData?.cardName ?? "Unknown";
        }
    }

    private string GetCardId()
    {
        switch (cardType)
        {
            case "guardian":
                return currentLegendData?.id.ToLower() ?? "";
            case "legendstone":
                return currentStoneData?.id.ToLower() ?? "";
            default:
                return currentCardData?.id.ToLower() ?? "";
        }
    }

    private object GetCardData()
    {
        switch (cardType)
        {
            case "guardian":
                return currentLegendData;
            case "legendstone":
                return currentStoneData;
            default:
                return currentCardData;
        }
    }

    /// <summary>
    /// Public method to get card data for external use
    /// </summary>
    public object GetPublicCardData()
    {
        return GetCardData();
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    public string GetCardType()
    {
        return cardType;
    }
}
