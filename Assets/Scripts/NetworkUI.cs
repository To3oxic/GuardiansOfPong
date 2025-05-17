// NetworkUI.cs (Updated to support dedicated server-client model)
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // For LoadSceneMode
using System.Collections.Generic; // For Dictionary
using TMPro; // If using TextMeshPro

public class NetworkUI : NetworkBehaviour
{
    public static NetworkUI Instance { get; private set; }

    [Header("Network Control Panel")]
    public Button hostButton;
    public Button clientButton;
    // public Button serverButton; // Uncomment if you want a dedicated server button from UI
    public GameObject networkButtonsPanel; // Panel with Host/Client buttons. Assign in Inspector.
    public TextMeshProUGUI statusText;     // Assign in Inspector.

    private const string GameSceneName = "GameScene"; // Ensure this matches your game scene's name

    private DeckPayload _pendingDeckPayload;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // If this GameObject is not already persistent via another script (like DontDestroyOnLoadHelper)
            // and you want NetworkUI to persist (e.g., if RPCs are here and it's not on NetworkManager_Persistent),
            // then uncomment the line below. 
            // DontDestroyOnLoad(gameObject); 
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (hostButton != null) hostButton.onClick.AddListener(StartHostAndInitiate);
        if (clientButton != null) clientButton.onClick.AddListener(StartClientAndInitiate);
        // if (serverButton != null) serverButton.onClick.AddListener(StartDedicatedServer);

        // This panel should be initially inactive, activated by MainMenuController
        if (networkButtonsPanel != null) networkButtonsPanel.SetActive(false);
        gameObject.SetActive(false); // The root NetworkUI object itself starts inactive
    }

    void OnDestroy() // Clean up callbacks
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToServer;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromServer;
        }
    }

    public void InitiateMatchmakingAndLoadScene(DeckPayload payload)
    {
        _pendingDeckPayload = payload;
        gameObject.SetActive(true); // Show this NetworkUI manager
        if (networkButtonsPanel != null) networkButtonsPanel.SetActive(true); // Show Host/Client buttons

        if (statusText != null)
        {
            statusText.text = "Select Host or Client to proceed.";
            statusText.gameObject.SetActive(true);
        }
        Debug.Log("[NetworkUI] Initiating matchmaking. Payload received.");
    }

    void StartHostAndInitiate()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("[NetworkUI] Host started successfully.");
            if (statusText != null) statusText.text = "Host started. Submitting deck & loading game...";

            // Host acts as a client too, so its deck submission goes through the same RPC path
            // The RPC should be called by the client part of the host after connection is established locally.
            // For a host, OnClientConnectedToServer will be called immediately with its own ClientId.
            // So, we can register for that, or for simplicity for host, call RPC directly then load scene.
            // Let's register for callback for consistency.
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToServer;
            // LoadGameScene(); // Scene load will be triggered after deck submission confirmation (implicitly via OnClientConnectedToServer for host)
        }
        else
        {
            Debug.LogError("[NetworkUI] Failed to start host.");
            if (statusText != null) statusText.text = "Failed to start host.";
        }
        if (networkButtonsPanel != null) networkButtonsPanel.SetActive(false);
    }

    void StartClientAndInitiate()
    {
        if (networkButtonsPanel != null) networkButtonsPanel.SetActive(false);
        if (statusText != null) statusText.text = "Attempting to connect to server...";
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToServer;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedFromServer;
        NetworkManager.Singleton.StartClient();
    }

    private void OnClientConnectedToServer(ulong clientId)
    {
        // This callback handles both true clients and the host acting as a client.
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"[NetworkUI] Local client (ID: {clientId}) connected to server. Submitting deck info.");
            if (statusText != null) statusText.text = "Connected! Submitting deck...";
            SubmitDeckInfoServerRpc(_pendingDeckPayload);
            // Client-side does not load the scene directly; server dictates scene transitions.
        }
        // Only remove for the specific client that connected, or it might affect other late joiners if any
        // For this simple setup, removing it once the local client is handled is fine.
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToServer;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromServer; // Also unregister disconnect here if solely tied to this connection attempt
        }

        // If this is the server (host) getting its own connection callback, it can now load the scene.
        if (IsServer && clientId == NetworkManager.ServerClientId) // ServerClientId is LocalClientId for host
        {
            LoadGameScene();
        }
    }

    private void OnClientDisconnectedFromServer(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogError("[NetworkUI] Failed to connect to the server or disconnected.");
            if (statusText != null) statusText.text = "Failed to connect or disconnected. Please try again.";
            // TODO: Show main menu or an error panel more gracefully
            // UIManager.Instance.ShowScreen("MainMenuScreen"); 
            // gameObject.SetActive(false); // Hide NetworkUI
            if (networkButtonsPanel != null) networkButtonsPanel.SetActive(true); // Show buttons again to retry
        }
        // Unsubscribe to prevent multiple calls if re-attempting connection
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToServer;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromServer;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitDeckInfoServerRpc(DeckPayload payload, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkUI - Server] Received deck from Client ID: {senderClientId}. Legend: {payload.castingLegendName}, Cards: {payload.cardNames.Count}");
        StorePayloadForGameManager(senderClientId, payload);

        // Server logic after receiving a deck. For example, check if all expected players submitted.
        // If this is the host submitting its own deck (clientId == NetworkManager.ServerClientId),
        // and it's the first/only one, it might then load the scene (if not already triggered by OnClientConnectedToServer for host).
        // This example assumes the host's OnClientConnectedToServer callback triggers scene load for simplicity.
    }

    // This needs to be a robust, server-authoritative way for GameManager to get this data.
    // Static dictionary is a placeholder for simplicity. A proper GameState manager is better.
    public static Dictionary<ulong, DeckPayload> TempPlayerDecks = new Dictionary<ulong, DeckPayload>();
    private void StorePayloadForGameManager(ulong clientId, DeckPayload payload)
    {
        TempPlayerDecks[clientId] = payload;
        Debug.Log($"[NetworkUI - Server] Stored payload for client {clientId}. Total stored: {TempPlayerDecks.Count}");
    }

    void LoadGameScene()
    {
        if (IsServer)
        {
            Debug.Log("[NetworkUI] Server is loading GameScene for all clients.");
            if (statusText != null) statusText.text = "Loading game...";
            // Ensure all necessary data is ready before loading (e.g., all players submitted decks if that's a requirement)
            NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        }
    }
}
