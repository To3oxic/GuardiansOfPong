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
        Debug.Log($"[HeadlessServer] Client connection config set: {_clientConnectionIp}:{_clientConnectionPort}");
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
            Debug.Log("[HeadlessServer] Awake called");

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
                Debug.Log("[HeadlessServer] DontDestroyOnLoad applied");
            }
            else
            {
                Debug.Log("[HeadlessServer] GameObject is not root, skipping DontDestroyOnLoad");
            }

            // Parse command-line arguments
            ParseCommandLineArguments();
            Debug.Log($"[HeadlessServer] Parsed arguments:");
            Debug.Log($"  Match ID: {_matchId}");
            Debug.Log($"  Player 1 ID: {_player1Id}");
            Debug.Log($"  Player 2 ID: {_player2Id}");

            // Validate we have required arguments
            if (string.IsNullOrEmpty(_matchId))
            {
                Debug.LogWarning("[HeadlessServer] No match ID provided - will continue anyway");
            }

            if (string.IsNullOrEmpty(_player1Id) || string.IsNullOrEmpty(_player2Id))
            {
                Debug.LogWarning("[HeadlessServer] Missing player IDs - will wait for clients to send them");
            }

            Debug.Log("[HeadlessServer] Awake completed successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HeadlessServer] CRASH in Awake: {e}\n{e.StackTrace}");
            throw;
        }
    }

    void Start()
    {
        Debug.Log("[HeadlessServer] ===== START() CALLED =====");
        Debug.Log("[HeadlessServer] Checking NetworkManager...");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[HeadlessServer] NetworkManager.Singleton is null!");
            return;
        }

        Debug.Log("[HeadlessServer] NetworkManager found");
        Debug.Log("[HeadlessServer] Checking command-line arguments...");

        // Check if we're explicitly in headless server mode via command-line
        bool isHeadlessServer = System.Array.Exists(System.Environment.GetCommandLineArgs(),
            element => element == "-server");

        Debug.Log($"[HeadlessServer] isHeadlessServer flag: {isHeadlessServer}");

        if (isHeadlessServer)
        {
            Debug.Log("[HeadlessServer] Headless server mode detected - starting server initialization");
            StartCoroutine(ServerInitializationRoutine());
        }
        else
        {
            Debug.Log("[HeadlessServer] Client mode - waiting for MatchmakingManager to call GetClientConnectionRoutine()");
            // Do NOT auto-connect. Wait for MatchmakingManager to explicitly call GetClientConnectionRoutine()
        }

        Debug.Log("[HeadlessServer] ===== START() COMPLETED =====");
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
        Debug.Log("[HeadlessServer] ===== ServerInitializationRoutine START =====");

        // Step 1: Configure transport
        Debug.Log("[HeadlessServer] Step 1: About to call ConfigureTransport()");
        try
        {
            ConfigureTransport();
            Debug.Log("[HeadlessServer] Step 1: ConfigureTransport() completed successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HeadlessServer] Step 1: ConfigureTransport() threw exception: {e}");
            yield break;
        }

        // Step 2: Start server
        Debug.Log("[HeadlessServer] Step 2: About to call StartServerAndRegisterHandlers()");
        yield return StartCoroutine(StartServerAndRegisterHandlers());
        Debug.Log("[HeadlessServer] Step 2: StartServerAndRegisterHandlers() returned");

        if (!_serverStarted)
        {
            Debug.LogError("[HeadlessServer] Failed to start server!");
            yield break;
        }

        // Step 3: Signal backend that server is ready (so clients receive server config)
        Debug.Log("[HeadlessServer] Step 3: Signaling backend that server is ready");
        yield return StartCoroutine(SignalServerReady());

        // Step 4: Wait for both clients to connect
        Debug.Log("[HeadlessServer] Step 4: Waiting for both clients to connect...");
        yield return StartCoroutine(WaitForBothClientsToConnect());

        if (!_bothClientsConnected)
        {
            Debug.LogError("[HeadlessServer] Timeout waiting for clients!");
            yield break;
        }

        // Step 5: Wait for both clients to submit player IDs
        // (Server will continue from SubmitPlayerIdRpc when 2nd ID arrives)
        Debug.Log("[HeadlessServer] Step 5: Waiting for clients to submit player IDs...");
        _initializationStep = 1; // Mark that we're waiting for IDs
        yield break; // Pause here - SubmitPlayerIdRpc will continue when ready
    }

    /// <summary>
    /// Client connection flow: connect to server and submit player ID
    /// </summary>
    private IEnumerator ClientConnectionRoutine()
    {
        Debug.Log("[HeadlessServer] === Client Connection START ===");

        var nm = NetworkManager.Singleton;

        // If NetworkManager is already listening, shut it down first
        if (nm.IsListening)
        {
            Debug.Log("[HeadlessServer] NetworkManager already listening, shutting down...");
            nm.Shutdown();
            yield return new WaitForSeconds(0.5f);
        }

        // Step 1: Configure transport for client connection
        Debug.Log("[HeadlessServer] Step 1: Configuring transport for client");
        ConfigureTransportForClient();

        // Step 2: Register client callbacks before connecting
        Debug.Log("[HeadlessServer] Step 2: Registering client callbacks");
        nm.OnClientConnectedCallback += OnClientConnectedToServer;
        nm.OnClientDisconnectCallback += OnClientDisconnectedFromServer;

        // Register message handler for scene loading
        if (nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(DeckAssignMsg, OnClientReceiveDeckAssignment);
        }

        // Step 3: Start client
        Debug.Log("[HeadlessServer] Step 3: Starting client");
        if (!nm.StartClient())
        {
            Debug.LogError("[HeadlessServer] Failed to start client!");
            yield break;
        }

        // Step 4: Wait for connection
        Debug.Log("[HeadlessServer] Step 4: Waiting for connection to server...");
        yield return StartCoroutine(WaitForClientConnection());

        // Step 5: Submit player ID to server
        Debug.Log("[HeadlessServer] Step 5: Submitting player ID to server");
        // Player ID will be sent after connection is established

        Debug.Log("[HeadlessServer] === Client Connection COMPLETE ===");
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
            Debug.LogError("[HeadlessServer] UnityTransport not found!");
            yield break;
        }

        // Start server
        Debug.Log("[HeadlessServer] Calling NetworkManager.StartServer()");
        bool success = false;
        try
        {
            success = nm.StartServer();
            Debug.Log($"[HeadlessServer] StartServer() returned: {success}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HeadlessServer] Exception calling StartServer(): {e.Message}");
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
                Debug.Log($"[HeadlessServer] ✓ Both clients connected!");
                _bothClientsConnected = true;
                yield break;
            }

            Debug.Log($"[HeadlessServer] Waiting for clients... ({connectedClientCount}/2 connected, {elapsedTime:F1}s)");

            yield return new WaitForSeconds(1f);
            elapsedTime += 1f;
        }

        Debug.LogError($"[HeadlessServer] ✗ Timeout waiting for clients ({clientConnectTimeoutSeconds}s)");
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
            Debug.LogError($"[HeadlessServer] Cannot load decks - only have {playerIds.Count} player IDs");
            _decksLoaded = false;
            yield break;
        }

        // Check if decks are already loaded from command-line args
        if (_playerDecks.ContainsKey(playerIds[0]) && _playerDecks.ContainsKey(playerIds[1]))
        {
            Debug.Log($"[HeadlessServer] ✓ Decks already loaded from command-line args");
            Debug.Log($"[HeadlessServer] Deck 1: Legend='{_playerDecks[playerIds[0]].castingLegendId}', Stone='{_playerDecks[playerIds[0]].legendStoneId}', Cards={_playerDecks[playerIds[0]].cardIds?.Count ?? 0}");
            Debug.Log($"[HeadlessServer] Deck 2: Legend='{_playerDecks[playerIds[1]].castingLegendId}', Stone='{_playerDecks[playerIds[1]].legendStoneId}', Cards={_playerDecks[playerIds[1]].cardIds?.Count ?? 0}");
            _decksLoaded = true;
            yield break;
        }

        // Decks not in args, so fetch from backend
        DeckPayload deck1 = null;
        DeckPayload deck2 = null;

        // Fetch first player's deck
        Debug.Log($"[HeadlessServer] Fetching deck from backend for player 1: {playerIds[0]}");
        yield return StartCoroutine(FetchDeckFromBackend(playerIds[0], (deck) =>
        {
            deck1 = deck;
        }));

        if (deck1 == null)
        {
            Debug.LogError($"[HeadlessServer] Failed to fetch deck for player {playerIds[0]}");
            _decksLoaded = false;
            yield break;
        }

        // Fetch second player's deck
        Debug.Log($"[HeadlessServer] Fetching deck from backend for player 2: {playerIds[1]}");
        yield return StartCoroutine(FetchDeckFromBackend(playerIds[1], (deck) =>
        {
            deck2 = deck;
        }));

        if (deck2 == null)
        {
            Debug.LogError($"[HeadlessServer] Failed to fetch deck for player {playerIds[1]}");
            _decksLoaded = false;
            yield break;
        }

        // Store decks
        _playerDecks[playerIds[0]] = deck1;
        _playerDecks[playerIds[1]] = deck2;

        Debug.Log($"[HeadlessServer] ✓ Successfully loaded both decks from backend");
        _decksLoaded = true;
    }

    private IEnumerator FetchDeckFromBackend(string playerId, System.Action<DeckPayload> onSuccess)
    {
        string url = $"{backendUrl}/api/players/{playerId}/active-deck/";
        Debug.Log($"[HeadlessServer] Fetching deck from: {url}");

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
                    Debug.Log($"[HeadlessServer] Deck response received for {playerId}");
                    Debug.Log($"[HeadlessServer] Raw JSON: {json}");
                    var deck = JsonUtility.FromJson<DeckPayload>(json);
                    Debug.Log($"[HeadlessServer] ✓ Parsed deck - Legend: '{deck.castingLegendId}', Stone: '{deck.legendStoneId}', Cards: {deck.cardIds?.Count ?? 0}");
                    if (deck.cardIds != null && deck.cardIds.Count > 0)
                    {
                        Debug.Log($"[HeadlessServer] Card IDs: {string.Join(", ", deck.cardIds)}");
                    }
                    onSuccess?.Invoke(deck);
                    Debug.Log($"[HeadlessServer] ✓ Successfully parsed deck for {playerId}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[HeadlessServer] Failed to parse deck JSON: {e.Message}");
                    onSuccess?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"[HeadlessServer] HTTP error fetching deck: {request.error} (Status: {request.responseCode})");
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    Debug.LogError($"[HeadlessServer] Response: {request.downloadHandler.text}");
                }
                onSuccess?.Invoke(null);
            }
        }
    }

    private IEnumerator StartGameWithDecks()
    {
        // Step 1: Load the game scene for all connected clients (server controls scene load)
        Debug.Log("[HeadlessServer] Loading GameScene for all clients...");
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
                Debug.Log($"[HeadlessServer] Found GameManager after scene load");
                break;
            }

            Debug.Log($"[HeadlessServer] Waiting for GameManager... ({elapsedTime:F1}s)");
            yield return new WaitForSeconds(0.5f);
            elapsedTime += 0.5f;
        }

        if (gameManager == null)
        {
            Debug.LogError("[HeadlessServer] GameManager not found after loading scene!");
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
                Debug.Log($"[HeadlessServer] Assigned deck to client {clientId} (player {playerId}) - Legend: '{deck.castingLegendId}', Stone: '{deck.legendStoneId}', Cards: {deck.cardIds?.Count ?? 0}");
                if (deck.cardIds != null && deck.cardIds.Count > 0)
                {
                    Debug.Log($"[HeadlessServer] Card IDs for client {clientId}: {string.Join(", ", deck.cardIds)}");
                }
            }
            else
            {
                Debug.LogError($"[HeadlessServer] No deck found for player {playerId}!");
            }
        }

        if (decksByClientId.Count < 2)
        {
            Debug.LogError($"[HeadlessServer] Not enough decks assigned ({decksByClientId.Count}/2)");
            yield break;
        }

        // Step 5: Initialize the game with decks
        Debug.Log("[HeadlessServer] Calling GameManager.InitializeFromDecks()");
        gameManager.SendMessage("InitializeFromDecks", decksByClientId, SendMessageOptions.DontRequireReceiver);

        _gameStarted = true;
        Debug.Log("[HeadlessServer] ✓ Game started successfully!");
    }

    private IEnumerator SignalServerReady()
    {
        // Output a marker FIRST that VPS can detect in stdout (before any HTTP attempts)
        Debug.Log("[READY] HeadlessServer initialized and ready to accept players");

        Debug.Log("[HeadlessServer] Signaling backend that server is ready");

        var serverInfo = new ServerReadyRequest
        {
            status = "ready",
            matchId = _matchId
        };

        string jsonBody = JsonUtility.ToJson(serverInfo);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        Debug.Log($"[HeadlessServer] Sending server ready signal: {jsonBody}");

        using (UnityWebRequest request = new UnityWebRequest($"{backendUrl}/api/server-ready/", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[HeadlessServer] ✓ Server ready signal sent successfully");
            }
            else
            {
                Debug.LogError($"[HeadlessServer] Failed to signal server ready: {request.error}");
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    Debug.LogError($"[HeadlessServer] Response: {request.downloadHandler.text}");
                }
            }
        }
    }

    private void ConfigureTransport()
    {
        Debug.Log("[HeadlessServer] ConfigureTransport() START");

        var nm = NetworkManager.Singleton;
        Debug.Log("[HeadlessServer] Got NetworkManager");

        var transport = nm.GetComponent<UnityTransport>();
        Debug.Log($"[HeadlessServer] Transport retrieved: {(transport == null ? "null, creating new" : "exists")}");

        if (transport == null)
        {
            Debug.Log("[HeadlessServer] Creating new UnityTransport component");
            transport = nm.gameObject.AddComponent<UnityTransport>();
            Debug.Log("[HeadlessServer] UnityTransport component added");
        }

        // Assign transport to NetworkManager's config
        Debug.Log("[HeadlessServer] Assigning transport to NetworkManager config");
        if (nm.NetworkConfig != null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
            Debug.Log("[HeadlessServer] Transport assigned to config");
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

        Debug.Log($"[HeadlessServer] About to set ConnectionData to 0.0.0.0:{port}");
        // Bind to all interfaces - OS will use the correct IP (185.252.233.117)
        // Using ServerListenAddress for server-side binding
        transport.ConnectionData.ServerListenAddress = "0.0.0.0";
        transport.ConnectionData.Port = port;
        Debug.Log("[HeadlessServer] ServerListenAddress set to 0.0.0.0 (all interfaces)");
        Debug.Log($"[HeadlessServer] Port set to {port}");

        // Increase buffer sizes
        transport.MaxPacketQueueSize = 512;
        transport.MaxPayloadSize = 6000;

        Debug.Log($"[HeadlessServer] Server transport configured: 0.0.0.0:{port}");
        Debug.Log("[HeadlessServer] ConfigureTransport() COMPLETE");
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
            Debug.Log($"[HeadlessServer] Removing brackets from IPv6 address: {addressToUse}");
        }

        // Connect to the configured server using direct ConnectionData properties
        transport.ConnectionData.Address = addressToUse;
        transport.ConnectionData.Port = _clientConnectionPort;

        // Increase buffer sizes
        transport.MaxPacketQueueSize = 512;
        transport.MaxPayloadSize = 6000;

        Debug.Log($"[HeadlessServer] Client transport configured: {addressToUse}:{_clientConnectionPort}");
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
                Debug.Log("[HeadlessServer] ✓ Successfully connected to server!");
                yield break;
            }

            Debug.Log($"[HeadlessServer] Waiting for connection... ({elapsedTime:F1}s)");
            yield return new WaitForSeconds(1f);
            elapsedTime += 1f;
        }

        Debug.LogError($"[HeadlessServer] ✗ Failed to connect to server ({timeout}s timeout)");
    }

    private void OnClientReceiveDeckAssignment(ulong senderClientId, FastBufferReader reader)
    {
        Debug.Log("[HeadlessServer] Client received deck assignment from server");

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
            Debug.Log($"[HeadlessServer] Client {clientId} connected. Connected count: {nm.ConnectedClientsIds.Count}");
            return;
        }

        // Client side: we connected to server
        if (nm.IsClient && !nm.IsServer && clientId == nm.LocalClientId)
        {
            Debug.Log($"[HeadlessServer] Connected to server as client {clientId}");

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
            Debug.LogError("[HeadlessServer] Lost connection!");
            yield break;
        }

        // Get player ID from BackendManager or command-line
        string playerId = GetPlayerIdForClient();

        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("[HeadlessServer] Could not determine player ID!");
            yield break;
        }

        Debug.Log($"[HeadlessServer] Client submitting player ID to server: {playerId}");
        Debug.Log($"[HeadlessServer] Client LocalClientId: {nm.LocalClientId}, IsConnectedClient: {nm.IsConnectedClient}");
        SendPlayerIdToServer(playerId);
        Debug.Log($"[HeadlessServer] ✓ Client submitted player ID");
    }

    private string GetPlayerIdForClient()
    {
        PlayerStates playerState = FindFirstObjectByType<PlayerStates>();
        if (playerState != null)
        {
            string playerId = playerState.PlayFabId;
            Debug.Log($"[HeadlessServer] Retrieved player ID from PlayerStates: {playerId}");
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

        Debug.Log($"[HeadlessServer] Client {clientId} disconnected");

        if (_clientIdToPlayerId.ContainsKey(clientId))
        {
            _clientIdToPlayerId.Remove(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerIdServerRpc(string playerId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[HeadlessServer] Server received player ID from client {senderClientId}: {playerId}");

        // Map client ID to player ID
        _clientIdToPlayerId[senderClientId] = playerId;
        Debug.Log($"[HeadlessServer] ✓ Server received player ID from client {senderClientId}: {playerId}");
        Debug.Log($"[HeadlessServer] Total player IDs received: {_clientIdToPlayerId.Count}/2");

        // If we have both IDs, continue server initialization
        if (_clientIdToPlayerId.Count >= 2)
        {
            Debug.Log($"[HeadlessServer] ✓✓✓ ALL PLAYER IDS RECEIVED - CONTINUING SERVER INITIALIZATION! ✓✓✓");
            StartCoroutine(ContinueServerInitializationAfterIds());
        }
    }

    private IEnumerator ContinueServerInitializationAfterIds()
    {
        // Step 6: Fetch decks from backend
        Debug.Log("[HeadlessServer] Step 6: Fetching player decks from backend");
        yield return StartCoroutine(FetchPlayerDecks());

        if (!_decksLoaded)
        {
            Debug.LogError("[HeadlessServer] Failed to load decks!");
            yield break;
        }

        // Step 7: Assign decks to players and start game
        Debug.Log("[HeadlessServer] Step 7: Starting game with decks");
        yield return StartCoroutine(StartGameWithDecks());

        Debug.Log("[HeadlessServer] === Server Initialization COMPLETE ===");
    }

    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg.StartsWith("-matchId="))
            {
                _matchId = arg.Substring("-matchId=".Length);
                Debug.Log($"[HeadlessServer] Match ID: {_matchId}");
            }
            if (arg.StartsWith("-player1Id="))
            {
                _player1Id = arg.Substring("-player1Id=".Length);
                Debug.Log($"[HeadlessServer] Player 1 ID: {_player1Id}");
            }
            if (arg.StartsWith("-player2Id="))
            {
                _player2Id = arg.Substring("-player2Id=".Length);
                Debug.Log($"[HeadlessServer] Player 2 ID: {_player2Id}");
            }
            if (arg.StartsWith("-port="))
            {
                if (ushort.TryParse(arg.Substring("-port=".Length), out ushort port))
                {
                    serverPort = port;
                    Debug.Log($"[HeadlessServer] Port: {serverPort}");
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
                        Debug.Log($"[HeadlessServer] Loaded Player 1 deck: {deck.cardIds?.Count ?? 0} cards");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[HeadlessServer] Failed to parse player 1 deck: {e.Message}");
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
                        Debug.Log($"[HeadlessServer] Loaded Player 2 deck: {deck.cardIds?.Count ?? 0} cards");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[HeadlessServer] Failed to parse player 2 deck: {e.Message}");
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
            Debug.LogError("[HeadlessServer] Not connected to server!");
            return;
        }

        // Call the ServerRpc on the HeadlessServer instance
        var instance = FindFirstObjectByType<HeadlessServer>();
        if (instance != null)
        {
            instance.SubmitPlayerIdServerRpc(playerId);
            Debug.Log($"[HeadlessServer] Client submitted player ID to server: {playerId}");
        }
        else
        {
            Debug.LogError("[HeadlessServer] HeadlessServer instance not found!");
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
