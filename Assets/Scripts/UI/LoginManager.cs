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
        // Try to get PlayFabBackendManager (handle timing issues)
        backendManager = PlayFabBackendManager.Instance;

        if (backendManager == null)
        {
            // Fallback: Search in scene if singleton not ready yet
            backendManager = FindAnyObjectByType<PlayFabBackendManager>();

            if (backendManager == null)
            {
                return;
            }
        }
    }

    private void Start()
    {
        // Verify backend manager is available
        if (backendManager == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // Show login panel
        if (loginPanel != null) loginPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

        // Hook button events
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginPressed);
        }
        else

        if (registerButton != null)
        {
            registerButton.onClick.AddListener(OnRegisterPressed);
        }
        else

        // Hook backend events
        backendManager.OnLoginSuccess += OnLoginSuccess;
        backendManager.OnLoginError += OnLoginError;
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
                foreach (var card in collection.cards)
                {
                }
            }
            else
            {
            }
        });

        // Load player's decks
        backendManager.LoadAllDecks((decks) =>
        {
        });
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
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
