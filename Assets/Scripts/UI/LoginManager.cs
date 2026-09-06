using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages PlayFab login and initialization.
/// Handles email/password authentication and loads player data on login.
/// </summary>
public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;

    private PlayFabBackendManager backendManager;

    private void Awake()
    {
        Debug.Log("[LoginManager] Awake() called");

        // Try to get PlayFabBackendManager (handle timing issues)
        backendManager = PlayFabBackendManager.Instance;
        Debug.Log($"[LoginManager] PlayFabBackendManager.Instance = {(backendManager != null ? "FOUND" : "NULL")}");

        if (backendManager == null)
        {
            Debug.Log("[LoginManager] Trying FindAnyObjectByType...");
            // Fallback: Search in scene if singleton not ready yet
            backendManager = FindAnyObjectByType<PlayFabBackendManager>();
            Debug.Log($"[LoginManager] FindAnyObjectByType result = {(backendManager != null ? "FOUND" : "NULL")}");

            if (backendManager == null)
            {
                Debug.LogError("[LoginManager] PlayFabBackendManager not found! Add it to the scene.");
                return;
            }
        }

        Debug.Log("[LoginManager] Found PlayFabBackendManager in scene");
    }

    private void Start()
    {
        Debug.Log("[LoginManager] Start() called");

        // Verify backend manager is available
        if (backendManager == null)
        {
            Debug.LogError("[LoginManager] PlayFabBackendManager still null in Start()! Scene setup issue.");
            gameObject.SetActive(false);
            return;
        }

        Debug.Log($"[LoginManager] loginPanel={loginPanel != null}, mainMenuPanel={mainMenuPanel != null}");

        // Show login panel
        if (loginPanel != null) loginPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

        Debug.Log("[LoginManager] Panels activated");

        // Hook button events
        if (loginButton != null)
        {
            Debug.Log("[LoginManager] Hooking loginButton");
            loginButton.onClick.AddListener(OnLoginPressed);
        }
        else
            Debug.LogWarning("[LoginManager] loginButton is NULL!");

        if (registerButton != null)
        {
            Debug.Log("[LoginManager] Hooking registerButton");
            registerButton.onClick.AddListener(OnRegisterPressed);
        }
        else
            Debug.LogWarning("[LoginManager] registerButton is NULL!");

        // Hook backend events
        backendManager.OnLoginSuccess += OnLoginSuccess;
        backendManager.OnLoginError += OnLoginError;

        Debug.Log("[LoginManager] Login UI ready");
    }

    private void OnLoginPressed()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowStatus("Please enter email and password", Color.red);
            return;
        }

        ShowStatus("Logging in...", Color.yellow);
        loginButton.interactable = false;
        registerButton.interactable = false;

        backendManager.LoginWithEmailAndPassword(email, password);
    }

    private void OnRegisterPressed()
    {
        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowStatus("Please enter email and password", Color.red);
            return;
        }

        string displayName = email.Split('@')[0]; // Use email prefix as display name

        ShowStatus("Registering...", Color.yellow);
        loginButton.interactable = false;
        registerButton.interactable = false;

        backendManager.RegisterWithEmailAndPassword(email, password, displayName);
    }

    private void OnLoginSuccess()
    {
        ShowStatus("Login successful!", Color.green);
        Debug.Log($"[LoginManager] Logged in as {backendManager.GetPlayerData().DisplayName}");

        // Hide login, show main menu
        if (loginPanel != null) loginPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Load player data
        LoadPlayerData();

    }

    private void OnLoginError(string error)
    {
        ShowStatus($"Login failed: {error}", Color.red);
        loginButton.interactable = true;
        registerButton.interactable = true;
    }

    private void LoadPlayerData()
    {
        // Load player's card collection
        backendManager.LoadPlayerCardCollection((collection) =>
        {
            if (collection != null)
            {
                Debug.Log($"[LoginManager] Loaded {collection.cards.Count} cards");
                foreach (var card in collection.cards)
                {
                    Debug.Log($"[LoginManager] Card:{card} ");
                    Debug.Log($"  - {card.cardName} (Lv.{card.level}, Qty: {card.quantity})");
                }
            }
            else
            {
                Debug.LogWarning("[LoginManager] Failed to load card collection");
            }
        });

        // Load player's decks
        backendManager.LoadAllDecks((decks) =>
        {
            Debug.Log($"[LoginManager] Loaded {decks.Count} decks");
        });
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        Debug.Log($"[LoginManager] {message}");
    }

    private void OnDestroy()
    {
        if (backendManager != null)
        {
            backendManager.OnLoginSuccess -= OnLoginSuccess;
            backendManager.OnLoginError -= OnLoginError;
        }
    }
}
