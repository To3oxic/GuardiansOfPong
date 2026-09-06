using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HeadlessServer: Manages the complete headless server flow for matchmaking.
///
/// Flow:
/// 1. Awake: Parse command-line arguments (player IDs, match ID)
/// 2. Start: Validate prerequisites and start server
/// 3. Wait for both clients to connect
/// 4. Receive client player IDs via message
/// 5. Fetch decks from backend for each player
/// 6. Assign decks to players and start game
/// </summary>
public class HeadlessServer : NetworkBehaviour
{
    public static HeadlessServer Instance { get; private set; }

    [Header("Backend Configuration")]
    [SerializeField] private string backendUrl = "http://185.252.233.117:8000";

    [Header("Server Settings")]
    [SerializeField] private ushort serverPort = 7777;
    [SerializeField] private float clientConnectTimeoutSeconds = 30f;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    // Command-line arguments
    private string _matchId = null;
    private string _player1Id = null;
    private string _player2Id = null;

    // Connected clients tracking
    private Dictionary<ulong, string> _clientIdToPlayerId = new Dictionary<ulong, string>(); // Maps client ID to backend player ID
    private Dictionary<string, DeckPayload> _playerDecks = new Dictionary<string, DeckPayload>(); // Maps player ID to deck

    // Server state
    private bool _serverStarted = false;
    private bool _bothClientsConnected = false;
    private bool _decksLoaded = false;
    private bool _gameStarted = false;
    private Coroutine _serverInitializationCoroutine = null;
    private int _initializationStep = 0; // 0 = waiting for IDs, 1 = fetching decks, etc.

    // Client connection settings (for clients connecting to headless server)
    private static string _clientConnectionIp = "127.0.0.1";
    private static ushort _clientConnectionPort = 7777;

    // Messaging
    private const string DeckAssignMsg = "DeckAssign";

    /// <summary>
    /// Called by MatchmakingManager to set the server IP and port for client connection
    /// </summary>
    public static void SetClientConnectionConfig(string serverIp, ushort serverPort)
    {
        _clientConnectionIp = serverIp;
        _clientConnectionPort = serverPort;
    }

    /// <summary>
    /// Public method to get the client connection routine
    /// Called by MatchmakingManager after setting connection config
    /// </summary>
    public IEnumerator GetClientConnectionRoutine()
    {
        yield return StartCoroutine(ClientConnectionRoutine());
    }

    void Awake()
    {
        try
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Only call DontDestroyOnLoad if this is a root GameObject
            if (gameObject.transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
            }

            // Parse command-line arguments
            ParseCommandLineArguments();

            // Validate we have required arguments
            if (string.IsNullOrEmpty(_matchId))
            {
            }

            if (string.IsNullOrEmpty(_player1Id) || string.IsNullOrEmpty(_player2Id))
            {
            }
        }
        catch (System.Exception e)
        {
            throw;
        }
    }

    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        // Check if we're explicitly in headless server mode via command-line
        bool isHeadlessServer = System.Array.Exists(System.Environment.GetCommandLineArgs(),
            element => element == "-server");

        if (isHeadlessServer)
        {
            StartCoroutine(ServerInitializationRoutine());
        }
        else
        {
            // Do NOT auto-connect. Wait for MatchmakingManager to explicitly call GetClientConnectionRoutine()
        }
    }

    public override void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnectedToServer;
            nm.OnClientDisconnectCallback -= OnClientDisconnectedFromServer;
        }
    }

    /// <summary>
    /// Main server initialization flow
    /// </summary>
    private IEnumerator ServerInitializationRoutine()
    {
        // Step 1: Configure transport
        try
        {
            ConfigureTransport();
        }
        catch (System.Exception e)
        {
            yield break;
        }

        // Step 2: Start server
        yield return StartCoroutine(StartServerAndRegisterHandlers());

        if (!_serverStarted)
        {
            yield break;
        }

        // Step 3: Signal backend that server is ready (so clients receive server config)
        yield return StartCoroutine(SignalServerReady());

        // Step 4: Wait for both clients to connect
        yield return StartCoroutine(WaitForBothClientsToConnect());

        if (!_bothClientsConnected)
        {
            yield break;
        }

        // Step 5: Wait for both clients to submit player IDs
        // (Server will continue from SubmitPlayerIdRpc when 2nd ID arrives)
        _initializationStep = 1; // Mark that we're waiting for IDs
        yield break; // Pause here - SubmitPlayerIdRpc will continue when ready
    }

    /// <summary>
    /// Client connection flow: connect to server and submit player ID
    /// </summary>
    private IEnumerator ClientConnectionRoutine()
    {
        var nm = NetworkManager.Singleton;

        // If NetworkManager is already listening, shut it down first
        if (nm.IsListening)
        {
            nm.Shutdown();
            yield return new WaitForSeconds(0.5f);
        }

        // Step 1: Configure transport for client connection
        ConfigureTransportForClient();

        // Step 2: Register client callbacks before connecting
        nm.OnClientConnectedCallback += OnClientConnectedToServer;
        nm.OnClientDisconnectCallback += OnClientDisconnectedFromServer;

        // Register message handler for scene loading
        if (nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(DeckAssignMsg, OnClientReceiveDeckAssignment);
        }

        // Step 3: Start client
        if (!nm.StartClient())
        {
            yield break;
        }

        // Step 4: Wait for connection
        yield return StartCoroutine(WaitForClientConnection());

        // Step 5: Submit player ID to server
        // Player ID will be sent after connection is established
    }

    private IEnumerator StartServerAndRegisterHandlers()
    {
        var nm = NetworkManager.Singleton;

        // Register callbacks
        nm.OnClientConnectedCallback += OnClientConnectedToServer;
        nm.OnClientDisconnectCallback += OnClientDisconnectedFromServer;

        // Ensure transport is set
        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            yield break;
        }

        // Start server
        bool success = false;
        try
        {
            success = nm.StartServer();
        }
        catch (System.Exception e)
        {
            yield break;
        }

        _serverStarted = success;
        yield return null;
    }

    private IEnumerator WaitForBothClientsToConnect()
    {
        var nm = NetworkManager.Singleton;
        float elapsedTime = 0f;

        while (elapsedTime < clientConnectTimeoutSeconds)
        {
            // Count connected clients (excluding server itself)
            int connectedClientCount = nm.ConnectedClientsIds.Count;

            if (connectedClientCount >= 2)
            {
                _bothClientsConnected = true;
                yield break;
            }

            yield return new WaitForSeconds(1f);
            elapsedTime += 1f;
        }

        _bothClientsConnected = false;
    }

    private IEnumerator FetchPlayerDecks()
    {
        // Use player IDs from clients (received via RPC) or fall back to command-line args
        var playerIds = new List<string>();

        // Get player IDs from the clients that connected
        foreach (var clientId in _clientIdToPlayerId.Keys)
        {
            playerIds.Add(_clientIdToPlayerId[clientId]);
        }

        // If no clients have sent IDs yet, try to use command-line args
        if (playerIds.Count == 0)
        {
            if (!string.IsNullOrEmpty(_player1Id)) playerIds.Add(_player1Id);
            if (!string.IsNullOrEmpty(_player2Id)) playerIds.Add(_player2Id);
        }

        // We need both player IDs
        if (playerIds.Count < 2)
        {
            _decksLoaded = false;
            yield break;
        }

        // Check if decks are already loaded from command-line args
        if (_playerDecks.ContainsKey(playerIds[0]) && _playerDecks.ContainsKey(playerIds[1]))
        {
            _decksLoaded = true;
            yield break;
        }

        // Decks not in args, so fetch from backend
        DeckPayload deck1 = null;
        DeckPayload deck2 = null;

        // Fetch first player's deck
        yield return StartCoroutine(FetchDeckFromBackend(playerIds[0], (deck) =>
        {
            deck1 = deck;
        }));

        if (deck1 == null)
        {
            _decksLoaded = false;
            yield break;
        }

        // Fetch second player's deck
        yield return StartCoroutine(FetchDeckFromBackend(playerIds[1], (deck) =>
        {
            deck2 = deck;
        }));

        if (deck2 == null)
        {
            _decksLoaded = false;
            yield break;
        }

        // Store decks
        _playerDecks[playerIds[0]] = deck1;
        _playerDecks[playerIds[1]] = deck2;

        _decksLoaded = true;
    }

    private IEnumerator FetchDeckFromBackend(string playerId, System.Action<DeckPayload> onSuccess)
    {
        string url = $"{backendUrl}/api/players/{playerId}/active-deck/";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var deck = JsonUtility.FromJson<DeckPayload>(json);
                    if (deck.cardIds != null && deck.cardIds.Count > 0)
                    {
                    }
                    onSuccess?.Invoke(deck);
                }
                catch (System.Exception e)
                {
                    onSuccess?.Invoke(null);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                }
                onSuccess?.Invoke(null);
            }
        }
    }

    private IEnumerator StartGameWithDecks()
    {
        // Step 1: Load the game scene for all connected clients (server controls scene load)
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

        // Step 2: Wait for GameManager to be initialized after scene load
        const float timeout = 5f;
        float elapsedTime = 0f;
        GameManager gameManager = null;

        while (elapsedTime < timeout)
        {
            gameManager = Object.FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsedTime += 0.5f;
        }

        if (gameManager == null)
        {
            yield break;
        }

        // Step 3: Give the scene a moment to fully initialize
        yield return new WaitForSeconds(1f);

        // Step 4: Build the deck mapping by client ID
        var decksByClientId = new Dictionary<ulong, DeckPayload>();

        foreach (var kvp in _clientIdToPlayerId)
        {
            ulong clientId = kvp.Key;
            string playerId = kvp.Value;

            if (_playerDecks.TryGetValue(playerId, out var deck))
            {
                decksByClientId[clientId] = deck;
                if (deck.cardIds != null && deck.cardIds.Count > 0)
                {
                }
            }
            else
            {
            }
        }

        if (decksByClientId.Count < 2)
        {
            yield break;
        }

        // Step 5: Initialize the game with decks
        gameManager.SendMessage("InitializeFromDecks", decksByClientId, SendMessageOptions.DontRequireReceiver);

        _gameStarted = true;
    }

    private IEnumerator SignalServerReady()
    {
        // Output a marker FIRST that VPS can detect in stdout (before any HTTP attempts)

        var serverInfo = new ServerReadyRequest
        {
            status = "ready",
            matchId = _matchId
        };

        string jsonBody = JsonUtility.ToJson(serverInfo);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest($"{backendUrl}/api/server-ready/", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
            }
            else
            {
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                }
            }
        }
    }

    private void ConfigureTransport()
    {
        var nm = NetworkManager.Singleton;

        var transport = nm.GetComponent<UnityTransport>();

        if (transport == null)
        {
            transport = nm.gameObject.AddComponent<UnityTransport>();
        }

        // Assign transport to NetworkManager's config
        if (nm.NetworkConfig != null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
        }

        // Use port from command-line args if provided, otherwise use serverPort
        ushort port = serverPort;
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg.StartsWith("-port="))
            {
                if (ushort.TryParse(arg.Substring("-port=".Length), out ushort parsedPort))
                {
                    port = parsedPort;
                    break;
                }
            }
        }

        // Bind to all interfaces - OS will use the correct IP (185.252.233.117)
        // Using ServerListenAddress for server-side binding
        transport.ConnectionData.ServerListenAddress = "0.0.0.0";
        transport.ConnectionData.Port = port;

        // Increase buffer sizes
        transport.MaxPacketQueueSize = 512;
        transport.MaxPayloadSize = 6000;
    }

    private void ConfigureTransportForClient()
    {
        var nm = NetworkManager.Singleton;
        var transport = nm.GetComponent<UnityTransport>();

        if (transport == null)
        {
            transport = nm.gameObject.AddComponent<UnityTransport>();
        }

        // Assign transport to NetworkManager's config
        if (nm.NetworkConfig != null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
        }

        // Remove brackets from IPv6 if present (UnityTransport handles IPv6 natively)
        string addressToUse = _clientConnectionIp;
        if (addressToUse.StartsWith("[") && addressToUse.EndsWith("]"))
        {
            addressToUse = addressToUse.Substring(1, addressToUse.Length - 2);
        }

        // Connect to the configured server using direct ConnectionData properties
        transport.ConnectionData.Address = addressToUse;
        transport.ConnectionData.Port = _clientConnectionPort;

        // Increase buffer sizes
        transport.MaxPacketQueueSize = 512;
        transport.MaxPayloadSize = 6000;
    }

    private IEnumerator WaitForClientConnection()
    {
        var nm = NetworkManager.Singleton;
        float elapsedTime = 0f;
        float timeout = 15f;

        while (elapsedTime < timeout)
        {
            if (nm.IsConnectedClient)
            {
                yield break;
            }

            yield return new WaitForSeconds(1f);
            elapsedTime += 1f;
        }
    }

    private void OnClientReceiveDeckAssignment(ulong senderClientId, FastBufferReader reader)
    {
        // This would be called if server sends deck assignment to client
        // For now, just log it
    }

    private void OnClientConnectedToServer(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Server side: a client connected
        if (nm.IsServer && clientId != NetworkManager.ServerClientId)
        {
            return;
        }

        // Client side: we connected to server
        if (nm.IsClient && !nm.IsServer && clientId == nm.LocalClientId)
        {
            // We're a client connecting to headless server
            // Wait a moment for network to stabilize, then submit our player ID
            StartCoroutine(SubmitPlayerIdAfterDelay());
        }
    }

    private IEnumerator SubmitPlayerIdAfterDelay()
    {
        // Wait for connection to fully stabilize
        yield return new WaitForSeconds(0.5f);

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsConnectedClient)
        {
            yield break;
        }

        // Get player ID from BackendManager or command-line
        string playerId = GetPlayerIdForClient();

        if (string.IsNullOrEmpty(playerId))
        {
            yield break;
        }

        SendPlayerIdToServer(playerId);
    }

    private string GetPlayerIdForClient()
    {
        PlayerStates playerState = FindFirstObjectByType<PlayerStates>();
        if (playerState != null)
        {
            string playerId = playerState.PlayFabId;
            return playerId;
        }
        return null;
    }

    private void OnClientDisconnectedFromServer(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId)
        {
            return;
        }

        if (_clientIdToPlayerId.ContainsKey(clientId))
        {
            _clientIdToPlayerId.Remove(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerIdServerRpc(string playerId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        // Map client ID to player ID
        _clientIdToPlayerId[senderClientId] = playerId;

        // If we have both IDs, continue server initialization
        if (_clientIdToPlayerId.Count >= 2)
        {
            StartCoroutine(ContinueServerInitializationAfterIds());
        }
    }

    private IEnumerator ContinueServerInitializationAfterIds()
    {
        // Step 6: Fetch decks from backend
        yield return StartCoroutine(FetchPlayerDecks());

        if (!_decksLoaded)
        {
            yield break;
        }

        // Step 7: Assign decks to players and start game
        yield return StartCoroutine(StartGameWithDecks());
    }

    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg.StartsWith("-matchId="))
            {
                _matchId = arg.Substring("-matchId=".Length);
            }
            if (arg.StartsWith("-player1Id="))
            {
                _player1Id = arg.Substring("-player1Id=".Length);
            }
            if (arg.StartsWith("-player2Id="))
            {
                _player2Id = arg.Substring("-player2Id=".Length);
            }
            if (arg.StartsWith("-port="))
            {
                if (ushort.TryParse(arg.Substring("-port=".Length), out ushort port))
                {
                    serverPort = port;
                }
            }
            // Parse deck arguments passed as JSON
            if (arg.StartsWith("-player1Deck="))
            {
                try
                {
                    string deckJson = arg.Substring("-player1Deck=".Length);
                    var deck = JsonUtility.FromJson<DeckPayload>(deckJson);
                    if (deck != null)
                    {
                        _playerDecks[_player1Id] = deck;
                    }
                }
                catch (System.Exception e)
                {
                }
            }
            if (arg.StartsWith("-player2Deck="))
            {
                try
                {
                    string deckJson = arg.Substring("-player2Deck=".Length);
                    var deck = JsonUtility.FromJson<DeckPayload>(deckJson);
                    if (deck != null)
                    {
                        _playerDecks[_player2Id] = deck;
                    }
                }
                catch (System.Exception e)
                {
                }
            }
        }
    }

    /// <summary>
    /// Client method to submit player ID to server
    /// Called by client after connecting
    /// </summary>
    public static void SendPlayerIdToServer(string playerId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsConnectedClient)
        {
            return;
        }

        // Call the ServerRpc on the HeadlessServer instance
        var instance = FindFirstObjectByType<HeadlessServer>();
        if (instance != null)
        {
            instance.SubmitPlayerIdServerRpc(playerId);
        }
        else
        {
        }
    }
}

// ==================== Data Classes ====================

[System.Serializable]
public class ServerReadyRequest
{
    public string status;
    public string matchId;
}
