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
        JoinQueue();
    }

    private void JoinQueue()
    {
        if (!PlayFabBackendManager.Instance.IsLoggedIn())
        {
            return;
        }

        queueButton.interactable = false;

        // Start matchmaking with callbacks
        PlayFabBackendManager.Instance.StartMatchmaking(
            matchData =>
            {
            },
            error =>
            {
                queueButton.interactable = true;
            }
        );

        // Subscribe to match found event
        PlayFabBackendManager.Instance.OnMatchFound += OnMatchFound;
        PlayFabBackendManager.Instance.OnMatchError += OnMatchError;
    }

    private void OnMatchFound()
    {
        var matchData = PlayFabBackendManager.Instance.CurrentMatchData;
        if (matchData != null)
        {
            // Connection to server is already complete, now load game scene
            SceneManager.LoadScene("GameScene");
        }
    }

    private void OnMatchError(string error)
    {
        queueButton.interactable = true;
    }
}

