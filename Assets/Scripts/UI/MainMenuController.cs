using UnityEngine;
using UnityEngine.UI; // For Button, Text
using TMPro; // For TextMeshProUGUI if you use it
using System.Collections.Generic;
using System.Linq;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject loginPanel; // Panel with just a login button
    public GameObject mainMenuPanel; // The actual main menu content after login

    [Header("Main Menu Elements")]
    public Button loginButton;
    public Button findMatchButton;
    public Button deckButton;
    public Button shopButton;
    public Button settingsButton;
    public TextMeshProUGUI userNameText; // Or public Text userNameText;
    public TextMeshProUGUI userLevelText;
    public TextMeshProUGUI userRankText;

    [Header("Guardian Display (Read-Only)")]
    public RawImage guardianDisplayImage; // Assign the RawImage using RT_GuardianDisplay from your UI
    public Transform guardianSpawnPoint;   // Assign the GuardianSpawnPoint from your GuardianDisplayPod
    public TextMeshProUGUI guardianNameText; // Optional: To display the name of the selected guardian
    private GameObject currentGuardianInstance;

    [Header("Legend Stone Display (Read-Only)")]
    public RawImage stoneDisplayImage;    // Assign the RawImage using RT_StoneDisplay from your UI
    public Transform stoneSpawnPoint;      // Assign the StoneSpawnPoint from your StoneDisplayPod
    public TextMeshProUGUI stoneNameText; // Optional: To display the name of the selected stone
    private GameObject currentStoneInstance;

    // Screen names used by UIManager
    private const string DeckScreenName = "DeckScreen";
    private const string ShopScreenName = "ShopScreen";
    private const string SettingsScreenName = "SettingsScreen";
    private const string LobbyScreenName = "LobbyScreen"; // Or your NetworkUI screen
    private const string MainMenuScreenName = "MainMenuScreen"; // The panel this controller is on
    private const string LoginPromptScreenName = "LoginPromptScreen"; // Panel that contains login button

    // Card count constants - for validation before calling NetworkUI
    private const int MIN_CARDS = DeckScreenController.MIN_SELECTED_CARDS; // Get from DeckScreenController
    private const int MAX_CARDS = DeckScreenController.MAX_SELECTED_CARDS; // Get from DeckScreenController


    void Start()
    {
        // Ensure UIManager is ready
        if (UIManager.Instance == null)
        {
            Debug.LogError("[MainMenuController] UIManager.Instance is null. Ensure UIManager is in the scene and initialized before MainMenuController.");
            this.enabled = false; // Disable this controller if UIManager is missing
            return;
        }

        if (loginButton != null) loginButton.onClick.AddListener(OnLoginClicked);
        if (findMatchButton != null) findMatchButton.onClick.AddListener(OnFindMatchClicked);
        if (deckButton != null) deckButton.onClick.AddListener(OnDeckClicked);
        if (shopButton != null) shopButton.onClick.AddListener(OnShopClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);

        CheckLoginState();
    }

    void OnEnable()
    {
        // Refresh display when this panel becomes active (e.g., returning from DeckScreen)
        if (GameSession.IsLoggedIn && mainMenuPanel != null && mainMenuPanel.activeSelf)
        {
            RefreshSelectedModelsDisplay();
        }
    }

    void RefreshSelectedModelsDisplay()
    {
        PlayerDeckData currentDeck = DeckManager.Instance?.GetSelectedDeck();
        CastingLegendData guardianToDisplay = null;
        LegendStoneData stoneToDisplay = null;

        if (currentDeck != null)
        {
            guardianToDisplay = currentDeck.selectedCastingLegend;
            stoneToDisplay = currentDeck.selectedLegendStone;
        }
        else
        {
            Debug.LogWarning("[MainMenuController] No deck data found in DeckManager. Displays will be empty.");
        }

        DisplayGuardian(guardianToDisplay);
        DisplayLegendStone(stoneToDisplay);
    }

    void DisplayGuardian(CastingLegendData guardianData)
    {
        if (guardianSpawnPoint == null) { /* Debug.LogError("GuardianSpawnPoint not assigned!"); */ return; } // Be less noisy if not set up
        if (currentGuardianInstance != null) Destroy(currentGuardianInstance);

        if (guardianData != null && guardianData.legendAvatarPrefab != null)
        {
            currentGuardianInstance = Instantiate(guardianData.legendAvatarPrefab, guardianSpawnPoint.position, guardianSpawnPoint.rotation, guardianSpawnPoint);
            currentGuardianInstance.layer = LayerMask.NameToLayer("GuardianDisplayLayer");
            foreach (Transform child in currentGuardianInstance.transform) child.gameObject.layer = currentGuardianInstance.layer;

            Animator anim = currentGuardianInstance.GetComponentInChildren<Animator>(); // Get Animator from children too
            if (anim != null) anim.Play("Idle"); // Assuming an "Idle" state exists
            else Debug.LogWarning($"[MainMenuController] Guardian prefab {guardianData.legendName} has no Animator to play Idle.");

            if (guardianNameText != null) guardianNameText.text = guardianData.legendName;
        }
        else
        {
            if (guardianNameText != null) guardianNameText.text = "Guardian: None";
        }
    }

    void DisplayLegendStone(LegendStoneData stoneData)
    {
        if (stoneSpawnPoint == null) { /* Debug.LogError("StoneSpawnPoint not assigned!"); */ return; } // Be less noisy
        if (currentStoneInstance != null) Destroy(currentStoneInstance);

        if (stoneData != null && stoneData.legendStoneVisualPrefab != null)
        {
            currentStoneInstance = Instantiate(stoneData.legendStoneVisualPrefab, stoneSpawnPoint.position, stoneSpawnPoint.rotation, stoneSpawnPoint);
            currentStoneInstance.layer = LayerMask.NameToLayer("StoneDisplayLayer");
            foreach (Transform child in currentStoneInstance.transform) child.gameObject.layer = currentStoneInstance.layer;

            Animator anim = currentStoneInstance.GetComponentInChildren<Animator>();
            if (anim != null) anim.Play("Idle");
            // else Debug.LogWarning($"[MainMenuController] Stone prefab {stoneData.stoneName} has no Animator to play Idle.");

            if (stoneNameText != null) stoneNameText.text = stoneData.stoneName;
        }
        else
        {
            if (stoneNameText != null) stoneNameText.text = "Stone: None";
        }
    }

    void CheckLoginState()
    {
        if (GameSession.IsLoggedIn)
        {
            ShowMainMenuContent();
        }
        else
        {
            // Show only login prompt. MainMenuController's game object should be structured
            // such that loginPanel and mainMenuPanel are children that can be toggled.
            if (loginPanel != null) loginPanel.SetActive(true);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            // Alternatively, UIManager.ShowScreen(LoginPromptScreenName);
            // For this example, assuming loginPanel and mainMenuPanel are direct children managed here.
        }
    }

    void OnLoginClicked()
    {
        GameSession.SimulateLogin(); // Simulate a successful login
        ShowMainMenuContent();
    }

    void ShowMainMenuContent()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        // UIManager.Instance.ShowScreen(MainMenuScreenName); // If main menu is a full screen managed by UIManager

        UpdateUserInfoDisplay();
        // Re-load and display selections as the panel might be re-enabled
        RefreshSelectedModelsDisplay();
    }

    void UpdateUserInfoDisplay()
    {
        if (userNameText != null) userNameText.text = $"User: {GameSession.UserName}";
        if (userLevelText != null) userLevelText.text = $"Level: {GameSession.UserLevel}";
        if (userRankText != null) userRankText.text = $"Rank: {GameSession.UserRank}";
    }

    void OnFindMatchClicked()
    {
        Debug.Log("[MainMenuController] Find Match clicked.");

        PlayerDeckData currentDeck = DeckManager.Instance?.GetSelectedDeck();

        if (currentDeck == null)
        {
            Debug.LogError("[MainMenuController] No deck data found in DeckManager!");
            // TODO: Show UI Feedback - "Please configure your deck first."
            return;
        }
        if (currentDeck.selectedCastingLegend == null || currentDeck.selectedLegendStone == null)
        {
            Debug.LogError("[MainMenuController] Guardian or Legend Stone not selected in the deck!");
            // TODO: Show UI Feedback - "Please select a Guardian and Stone in your deck."
            return;
        }
        if (currentDeck.selectedCards == null || currentDeck.selectedCards.Count < MIN_CARDS || currentDeck.selectedCards.Count > MAX_CARDS)
        {
            Debug.LogError("[MainMenuController] Invalid card selection in the deck. Please go to Deck screen.");
            // TODO: Show UI Feedback - "Please select {MIN_CARDS}-{MAX_CARDS} cards in your deck."
            return;
        }

        DeckPayload payload = new DeckPayload
        {
            cardNames = currentDeck.selectedCards.Select(c => c.cardName).ToList(),
            castingLegendName = currentDeck.selectedCastingLegend.legendName,
            legendStoneName = currentDeck.selectedLegendStone.stoneName
        };

        Debug.Log($"[MainMenuController] Preparing to find match with Payload: Cards({payload.cardNames.Count}), Guardian({payload.castingLegendName}), Stone({payload.legendStoneName})");

        NetworkUI networkUIInstance = FindObjectOfType<NetworkUI>(true); // true to include inactive
        if (networkUIInstance != null)
        {
            if (!networkUIInstance.gameObject.activeSelf) networkUIInstance.gameObject.SetActive(true);
            networkUIInstance.InitiateMatchmakingAndLoadScene(payload); // Expecting this method in NetworkUI
        }
        else
        {
            Debug.LogError("[MainMenuController] NetworkUI instance not found!");
        }
    }

    void OnDeckClicked()
    {
        Debug.Log("[MainMenuController] Deck clicked.");
        UIManager.Instance.ShowScreen(DeckScreenName);
    }

    void OnShopClicked()
    {
        Debug.Log("[MainMenuController] Shop clicked.");
        UIManager.Instance.ShowScreen(ShopScreenName);
    }

    void OnSettingsClicked()
    {
        Debug.Log("[MainMenuController] Settings clicked.");
        UIManager.Instance.ShowScreen(SettingsScreenName);
    }
}