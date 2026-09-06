using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviour
{
    [Header("Cube References")]
    [SerializeField] private MenuCubeRotator menuCubeRotator;
    [SerializeField] private GameBoardZone gameBoardZone;
    [SerializeField] private CardSelectionZone cardSelectionZone;
    [SerializeField] private ShopZone shopZone;

    [Header("UI Elements")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Text playerInfoDisplay; // Level, Rank, Coins

    private static MainMenuManager instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Initialize player data
        LoadPlayerData();
        UpdatePlayerInfo();

        // Setup button
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGamePressed);
        }

        // Initialize all cube faces
        gameBoardZone.Initialize();
        cardSelectionZone.Initialize();
        shopZone.Initialize();

        Debug.Log("[MainMenuManager] MainMenu initialized with rotating cube system");
    }

    private void LoadPlayerData()
    {
        // TODO: Query database for:
        // - Player account info (username, level, rank, coins)
        // - Player's saved decks
        // - Player's unlocked units/guardians

        Debug.Log("[MainMenuManager] Loading player data from database...");
    }

    private void UpdatePlayerInfo()
    {
        // TODO: Display player info in UI
        // playerInfoDisplay.text = $"Level: {playerLevel}\nRank: {playerRank}\nCoins: {playerCoins}";
    }

    private void OnStartGamePressed()
    {
        Debug.Log("[MainMenuManager] Start Game pressed - connecting to matchmaking...");
        // TODO: Connect to backend matchmaking service
        // TODO: Show "Searching for opponent..." UI
    }

    public static MainMenuManager Instance => instance;
}
