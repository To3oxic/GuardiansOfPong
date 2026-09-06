using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// MatchmakingUIManager: Simple queue button to join matchmaking
/// </summary>
public class MatchmakingUIManager : MonoBehaviour
{
    public static MatchmakingUIManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button queueButton;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (queueButton != null)
            queueButton.onClick.AddListener(OnQueueButtonClicked);

        Debug.Log("[Matchmaking] MatchmakingUIManager initialized");
    }

    void OnDestroy()
    {
        if (queueButton != null)
            queueButton.onClick.RemoveListener(OnQueueButtonClicked);
    }

    // ============================================================
    // BUTTON CALLBACKS
    // ============================================================

    private void OnQueueButtonClicked()
    {
        Debug.Log("[Matchmaking] Queue button clicked");
        JoinQueue();
    }

    private void JoinQueue()
    {
        if (!PlayFabBackendManager.Instance.IsLoggedIn())
        {
            Debug.LogError("[Matchmaking] Not logged in");
            return;
        }

        Debug.Log("[Matchmaking] Joining queue...");
        queueButton.interactable = false;

        // Start matchmaking with callbacks
        PlayFabBackendManager.Instance.StartMatchmaking(
            matchData =>
            {
                Debug.Log("[Matchmaking] Matchmaking initiated, polling started");
            },
            error =>
            {
                Debug.LogError($"[Matchmaking] Failed to start matchmaking: {error}");
                queueButton.interactable = true;
            }
        );

        // Subscribe to match found event
        PlayFabBackendManager.Instance.OnMatchFound += OnMatchFound;
        PlayFabBackendManager.Instance.OnMatchError += OnMatchError;
    }

    private void OnMatchFound()
    {
        Debug.Log("[Matchmaking] Match found and connected to server!");
        var matchData = PlayFabBackendManager.Instance.CurrentMatchData;
        if (matchData != null)
        {
            Debug.Log($"[Matchmaking] Loading game scene with match: {matchData.MatchId}");
            // Connection to server is already complete, now load game scene
            SceneManager.LoadScene("GameScene");
        }
    }

    private void OnMatchError(string error)
    {
        Debug.LogError($"[Matchmaking] Match error: {error}");
        queueButton.interactable = true;
    }
}


