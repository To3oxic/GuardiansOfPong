using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels (optional)")]
    public GameObject loginPanel;
    public GameObject mainMenuPanel;

    [Header("Buttons")]
    public Button startGameButton;
    public Button deckButton;
    public Button shopButton;
    public Button settingsButton;
    public Button startServerButton;
    public Button stopServerButton;

    [Header("Server Status")]
    public TextMeshProUGUI serverStatusText;

    [Header("Read-only selections (optional visuals)")]
    public RawImage guardianDisplayImage;
    public TextMeshProUGUI guardianNameText;
    public RawImage stoneDisplayImage;
    public TextMeshProUGUI stoneNameText;

    [Header("Navigation")]
    public ScreenRouter router; // fallback if UIManager not used

    private const string MainMenuScreenName = "MainMenuScreen";
    private const string DeckScreenName = "DeckScreen";
    private const string ShopScreenName = "ShopScreen";
    private const string SettingsScreenName = "SettingsScreen";

    void Start()
    {
        // PERFORMANCE FIX: Cap framerate and enable VSync for menus
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;

        Debug.Log("[MainMenuController] Set target framerate to 60 FPS and enabled VSync");

        if (startGameButton) startGameButton.onClick.AddListener(OnStartGame);
        if (deckButton) deckButton.onClick.AddListener(() => NavigateToScreen(DeckScreenName));
        if (shopButton) shopButton.onClick.AddListener(() => NavigateToScreen(ShopScreenName));
        if (settingsButton) settingsButton.onClick.AddListener(() => NavigateToScreen(SettingsScreenName));
        if (startServerButton) startServerButton.onClick.AddListener(OnStartServerClicked);
        if (stopServerButton) stopServerButton.onClick.AddListener(OnStopServerClicked);

        // Initial state
        if (loginPanel) loginPanel.SetActive(false);
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }

    public void OnStartGame()
    {
        // Build payload from the DeckSelectionState (set on the Deck screen)
        DeckPayload payload = DeckSelectionState.BuildPayloadFromCurrent();

        bool valid = payload.cardIds != null
                     && payload.cardIds.Count >= 6
                     && !string.IsNullOrEmpty(payload.castingLegendId)
                     && !string.IsNullOrEmpty(payload.legendStoneId);

        if (!valid)
        {
            Debug.LogWarning("[MainMenuController] Deck invalid. Go select 6–8 cards, 1 legend, 1 stone.");
            ShowScreen(DeckScreenName);
            return;
        }

        if (NetworkUI.Instance == null)
        {
            Debug.LogError("[MainMenuController] NetworkUI not present. Add a root GameObject with NetworkObject + NetworkUI.");
            return;
        }

        NetworkUI.Instance.InitiateMatchmakingAndLoadScene(payload);
    }



    public void NavigateToScreen(string screenName) => ShowScreen(screenName);



    void OnStartServerClicked()
    {
        NetworkUI.Instance?.StartDedicatedServer();
        UpdateServerStatus();
    }

    void OnStopServerClicked()
    {
        NetworkUI.Instance?.StopDedicatedServer();
        UpdateServerStatus();
    }

    void UpdateServerStatus()
    {
        if (serverStatusText != null && Unity.Netcode.NetworkManager.Singleton != null)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            serverStatusText.text = nm.IsServer ? "Server: Running" : "Server: Stopped";
        }
    }

    void ShowScreen(string screen)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowScreen(screen);
        }
        else if (router != null)
        {
            router.Show(screen switch
            {
                DeckScreenName => "Deck",
                ShopScreenName => "Shop",
                SettingsScreenName => "Settings",
                _ => "Main"
            });
        }
        else
        {
            Debug.LogWarning("[MainMenuController] No UIManager or ScreenRouter assigned.");
        }
    }
}