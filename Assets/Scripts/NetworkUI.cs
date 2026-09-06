// NetworkUI: quick-match flow with detailed logs.
// Client tries to connect; if none found, becomes host. Decks are sent via Named Messages.
// When server has both decks, it loads GameScene. After load completes, it invokes GameManager to start.
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;

public class NetworkUI : MonoBehaviour
{
    public static NetworkUI Instance { get; private set; }

    [Header("UI (optional)")]
    public GameObject networkButtonsPanel;
    public Button hostButton;
    public Button clientButton;
    public TextMeshProUGUI statusText;

    [Header("Quick Match")]
    public bool autoQuickMatch = true;
    [SerializeField] string hostAddress = "127.0.0.1";
    [SerializeField] ushort port = 7777;
    [SerializeField] float clientConnectTimeoutSeconds = 4f;

    [Header("Scenes")]
    [SerializeField] string gameSceneName = "GameScene";

    [Header("Backend Configuration")]
    [SerializeField] string backendUrl = "http://localhost:8000";

    // Temporary store on server (clientId -> DeckPayload)
    public static Dictionary<ulong, DeckPayload> TempPlayerDecks = new Dictionary<ulong, DeckPayload>();

    private DeckPayload _pendingDeckPayload;
    private Coroutine _quickMatchCo;
    private bool _isMatching;
    private bool _matchStartTriggered;
    private string _player1Id = null;
    private string _player2Id = null;
    private bool _decksLoadedFromBackend = false;

    // Message name for deck submission
    private const string DeckSubmitMsg = "DeckSubmit";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Parse command line arguments for player IDs
        ParseCommandLineArguments();
    }

    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        EnsureTransport();

        var nm = NetworkManager.Singleton;

        // Approve everyone if approval is enabled (prevents instant kick)
        if (nm.NetworkConfig != null && nm.NetworkConfig.ConnectionApproval)
        {
            nm.ConnectionApprovalCallback = OnApprovalCheck;
        }

        nm.OnClientConnectedCallback += OnClientConnectedToServer;

        // Register server handler for deck submissions (server only)
        if (nm.IsServer && nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(DeckSubmitMsg, OnServerReceiveDeckMessage);
        }

        // NGO scene event (only valid when scene management enabled)
        if (nm.NetworkConfig != null && nm.NetworkConfig.EnableSceneManagement && nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }
        else
        {
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        if (clientButton) clientButton.onClick.AddListener(() => StartCoroutine(StartClientAndInitiateCo()));

        // Don't start server here - let HeadlessServer handle it
        // HeadlessServer.cs will handle the -server flag
        if (System.Array.Exists(System.Environment.GetCommandLineArgs(), element => element == "-server"))
        {
            // Hide any UI panels for headless
            if (networkButtonsPanel) networkButtonsPanel.SetActive(false);
        }
        else
        {
            // Show UI for client mode
            if (networkButtonsPanel != null && !autoQuickMatch)
            {
                networkButtonsPanel.SetActive(true);
            }
        }
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnectedToServer;

            if (nm.SceneManager != null)
                nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;

            // Unregister on server
            if (nm.IsServer && nm.CustomMessagingManager != null)
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(DeckSubmitMsg);

            // clear approval callback
            if (nm.NetworkConfig != null && nm.NetworkConfig.ConnectionApproval)
                nm.ConnectionApprovalCallback = null;

            // Only unsubscribe if we were using Unity's scene events (not NGO)
            if (nm.NetworkConfig == null || !nm.NetworkConfig.EnableSceneManagement || nm.SceneManager == null)
            {
                SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            }
        }
        else
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        }
    }

    // Called by MainMenuController
    public void InitiateMatchmakingAndLoadScene(DeckPayload payload)
    {
        _pendingDeckPayload = payload;
        if (_isMatching) return;

        if (!autoQuickMatch && networkButtonsPanel != null && (hostButton != null || clientButton != null))
        {
            networkButtonsPanel.SetActive(true);
            return;
        }

        if (_quickMatchCo != null) StopCoroutine(_quickMatchCo);
        _quickMatchCo = StartCoroutine(QuickMatchFlow());
    }

    // Called by MainMenuController to start a dedicated server
    public void StartDedicatedServer()
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        StartCoroutine(StartDedicatedServerRoutine());
    }

    // Called by MainMenuController to stop the dedicated server
    public void StopDedicatedServer()
    {
        if (NetworkManager.Singleton?.IsListening == true)
        {
            NetworkManager.Singleton.Shutdown();
            TempPlayerDecks.Clear();
            _matchStartTriggered = false;
            _isMatching = false;
        }
    }

    // Called after a match ends to reset for next match (for dedicated servers)
    public void ResetForNextMatch()
    {
        TempPlayerDecks.Clear();
        _matchStartTriggered = false;
        _isMatching = false;
    }

    private IEnumerator QuickMatchFlow()
    {
        _isMatching = true;
        _matchStartTriggered = false;
        TempPlayerDecks.Clear();

        yield return EnsureStopped();

        ConfigureTransport(hostAddress, port);

        bool connected = false;
        bool failed = false;
        void onConnect(ulong id) { if (id == NetworkManager.Singleton.LocalClientId) connected = true; }
        void onDisconnect(ulong id) { if (id == NetworkManager.Singleton.LocalClientId) failed = true; }

        var nm = NetworkManager.Singleton;
        nm.OnClientConnectedCallback += onConnect;
        nm.OnClientDisconnectCallback += onDisconnect;

        if (!nm.StartClient())
        {
            failed = true;
        }

        float t = 0f;
        while (!connected && !failed && t < clientConnectTimeoutSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        nm.OnClientConnectedCallback -= onConnect;
        nm.OnClientDisconnectCallback -= onDisconnect;

        if (connected && nm.IsClient && !nm.IsServer)
        {
            SendDeckToServerViaMessage(_pendingDeckPayload);
            _isMatching = false;
            yield break;
        }

        _isMatching = false;
    }

    private IEnumerator StartDedicatedServerRoutine()
    {
        yield return EnsureStopped();

        // Configure transport to bind on all interfaces
        ConfigureTransport("0.0.0.0", port);

        // Start server-only (not host, just pure server)
        bool serverStarted = false;
        try
        {
            serverStarted = NetworkManager.Singleton.StartServer();
        }
        catch (System.Exception e)
        {
            yield break;
        }

        if (serverStarted)
        {
            // Register server message handlers
            RegisterServerMessageHandlers();

            // Clear any existing player decks
            TempPlayerDecks.Clear();

            // Reset match flags for new matches
            _matchStartTriggered = false;
            _isMatching = false;

            // Parse command-line arguments for player IDs
            ParseCommandLineArguments();

            // Don't fetch decks here - wait for clients to connect first
            // They will be fetched in OnClientConnectedToServer when both clients are connected
        }
        else
        {
        }

        // Hide network buttons panel if showing
        if (networkButtonsPanel) networkButtonsPanel.SetActive(false);
    }

    private IEnumerator EnsureStopped()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
        }

        float t = 0f;
        while (nm != null && nm.IsListening && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ConfigureTransport(string address, ushort p)
    {
        var utp = EnsureTransport();
        if (utp == null)
        {
            return;
        }

        // Configure connection data
        utp.SetConnectionData(address, p);

        // Increase buffer sizes to prevent packet overflow
        utp.MaxPacketQueueSize = 512;  // Increased from default 128
        utp.MaxPayloadSize = 6000;     // Increased payload size
    }

    private UnityTransport EnsureTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        // Ensure NetworkManager persists across scenes
        if (nm.gameObject.scene.name != "DontDestroyOnLoad")
        {
            DontDestroyOnLoad(nm.gameObject);
        }

        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null)
        {
            utp = nm.gameObject.AddComponent<UnityTransport>();
        }

        if (nm.NetworkConfig.NetworkTransport != utp)
        {
            nm.NetworkConfig.NetworkTransport = utp;
        }

        return utp;
    }

    private IEnumerator StartClientAndInitiateCo()
    {
        yield return EnsureStopped();

        ConfigureTransport(hostAddress, port);
        if (!NetworkManager.Singleton.StartClient())
        {
            yield break;
        }

        if (networkButtonsPanel) networkButtonsPanel.SetActive(false);
    }

    private void OnClientConnectedToServer(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (clientId == nm.LocalClientId && nm.IsClient && !nm.IsServer)
        {
            StartCoroutine(SendDeckAfterHandshake());
            return;
        }

        // Server side: a client connected
        if (nm.IsServer && clientId != NetworkManager.ServerClientId)
        {
            // Check if both players are now connected (server + 1 client = 2)
            if (nm.ConnectedClientsIds.Count >= 2 && !_decksLoadedFromBackend)
            {
                StartCoroutine(FetchPlayerDecksFromBackendWhenReady());
            }
        }
    }

    private IEnumerator FetchPlayerDecksFromBackendWhenReady()
    {
        // Wait a moment for all connections to stabilize
        yield return new WaitForSeconds(0.5f);

        var nm = NetworkManager.Singleton;

        // Verify we still have 2 connected clients
        if (nm.ConnectedClientsIds.Count < 2)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(_player1Id) && !string.IsNullOrEmpty(_player2Id))
        {
            yield return StartCoroutine(FetchPlayerDecksFromBackend());
        }
        else
        {
        }
    }

    private IEnumerator SendDeckAfterHandshake()
    {
        // Wait a couple of frames to avoid sending too early
        yield return null; yield return null;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && nm.IsConnectedClient)
        {
            SendDeckToServerViaMessage(_pendingDeckPayload);
            // Resend until scene switches or timeout (robust against message loss)
            StartCoroutine(ResendDeckUntilSceneChange(5f, 0.5f));
        }
        else
        {
        }
    }

    private IEnumerator ResendDeckUntilSceneChange(float timeoutSeconds, float intervalSeconds)
    {
        float t = 0f;
        while (SceneManager.GetActiveScene().name != gameSceneName &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsClient &&
               NetworkManager.Singleton.IsConnectedClient &&
               t < timeoutSeconds)
        {
            yield return new WaitForSecondsRealtime(intervalSeconds);
            t += intervalSeconds;

            SendDeckToServerViaMessage(_pendingDeckPayload);
        }
    }

    // CLIENT -> SERVER: send deck via named message (UTF8 bytes with explicit length)
    private void SendDeckToServerViaMessage(DeckPayload payload)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null)
        {
            return;
        }

        string json = JsonUtility.ToJson(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        using (var writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp))
        {
            writer.WriteValueSafe(bytes.Length);
            writer.WriteBytesSafe(bytes);
            nm.CustomMessagingManager.SendNamedMessage(DeckSubmitMsg, NetworkManager.ServerClientId, writer);
        }
    }

    // SERVER: receive deck from client (reads explicit length + UTF8 bytes)
    private void OnServerReceiveDeckMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (!reader.TryBeginRead(sizeof(int)))
        {
            return;
        }

        reader.ReadValueSafe(out int len);
        if (len <= 0 || !reader.TryBeginRead(len))
        {
            return;
        }

        byte[] bytes = new byte[len];
        reader.ReadBytesSafe(ref bytes, len);
        string json = Encoding.UTF8.GetString(bytes);

        var payload = JsonUtility.FromJson<DeckPayload>(json);
        TempPlayerDecks[senderClientId] = payload;

        EvaluateStartCondition("OnServerReceiveDeckMessage");
    }

    // SERVER: check if we have both host and one client deck, then load scene (or start directly if already in it)
    private void EvaluateStartCondition(string where)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var nm = NetworkManager.Singleton;
        bool isDedicatedServer = nm.IsServer && !nm.IsHost && !nm.IsClient;

        bool haveEnoughPlayers = false;

        if (isDedicatedServer)
        {
            // Dedicated server: need 2 client decks (no host deck)
            int clientDecks = 0;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (TempPlayerDecks.ContainsKey(id))
                {
                    clientDecks++;
                }
            }
            haveEnoughPlayers = clientDecks >= 2;
        }
        else
        {
            // Host mode: need host deck + 1 client deck
            bool haveHost = TempPlayerDecks.ContainsKey(NetworkManager.ServerClientId);
            bool haveOther = false;
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id != NetworkManager.ServerClientId && TempPlayerDecks.ContainsKey(id))
                {
                    haveOther = true;
                    break;
                }
            }
            haveEnoughPlayers = haveHost && haveOther;
        }

        if (!haveEnoughPlayers) return;

        if (_matchStartTriggered) return;
        _matchStartTriggered = true;

        var activeScene = SceneManager.GetActiveScene().name;
        if (activeScene == gameSceneName)
        {
            StartMatchOnServer();
        }
        else
        {
            LoadGameScene();
        }
    }

    // SERVER: load the game scene for everyone
    private void LoadGameScene()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            _matchStartTriggered = false;
            return;
        }

        if (!IsSceneInBuild(gameSceneName))
        {
            _matchStartTriggered = false;
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    // NGO Scene load complete (all clients)
    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (sceneName != gameSceneName) return;

        StartMatchOnServer();
    }

    // Fallback if NGO event doesn’t fire (or scene mgmt off)
    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (scene.name != gameSceneName) return;

        StartMatchOnServer();
    }

    // SERVER: find GameManager in GameScene and kick off match
    private void StartMatchOnServer()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
        {
            return;
        }
        StartCoroutine(StartMatchServerRoutine());
    }

    private System.Collections.IEnumerator StartMatchServerRoutine()
    {
        // Wait for GameManager to exist and be network-spawned
        const float timeout = 5f;
        float t = 0f;
        GameManager gm = null;

        // Also ensure NetworkManager is alive on the server
        while (t < timeout)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                yield break;
            }
            else
            {
                gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                if (gm != null)
                {
                    // If there is a NetworkObject, prefer to wait until spawned
                    var no = gm.GetComponent<Unity.Netcode.NetworkObject>();
                    if (no == null)
                    {
                        break;
                    }
                    else if (no.IsSpawned)
                    {
                        break;
                    }
                    else
                    {
                    }
                }
                else
                {
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            yield break;
        }

        gm = gm ?? UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            yield break;
        }

        // Extra diagnostics to catch the "client ignored" case
        if (!gm.IsServer)
        {
            yield break;
        }

        gm.SendMessage("InitializeFromDecks", TempPlayerDecks, SendMessageOptions.DontRequireReceiver);
    }

    private bool IsSceneInBuild(string nameOrPath)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var fileNoExt = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileNoExt, nameOrPath, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, nameOrPath, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // Approve-all callback (prevents instant disconnects when Connection Approval is enabled)
    private void OnApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                                 NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false; // we spawn our own objects
        response.Pending = false;
    }

    private void RegisterServerMessageHandlers()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (nm.CustomMessagingManager != null)
        {
            // Safe to unregister first in case it was already set
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(DeckSubmitMsg);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(DeckSubmitMsg, OnServerReceiveDeckMessage);
        }
    }

    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg.StartsWith("-player1Id="))
            {
                _player1Id = arg.Substring("-player1Id=".Length);
            }
            if (arg.StartsWith("-player2Id="))
            {
                _player2Id = arg.Substring("-player2Id=".Length);
            }
        }
    }

    private IEnumerator FetchPlayerDecksFromBackend()
    {
        if (string.IsNullOrEmpty(_player1Id) || string.IsNullOrEmpty(_player2Id))
        {
            yield break;
        }

        DeckPayload deck1 = null;
        DeckPayload deck2 = null;

        // Fetch player 1 deck
        yield return StartCoroutine(FetchDeckFromBackend(_player1Id, (deck) =>
        {
            deck1 = deck;
        }));

        // Fetch player 2 deck
        yield return StartCoroutine(FetchDeckFromBackend(_player2Id, (deck) =>
        {
            deck2 = deck;
        }));

        if (deck1 != null && deck2 != null)
        {
            // Store in TempPlayerDecks using actual connected client IDs
            // The headless server (host) has ServerClientId
            // The first connected client will have their own client ID

            var connectedIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            connectedIds.Sort((a, b) =>
            {
                // Ensure ServerClientId comes first
                if (a == NetworkManager.ServerClientId && b != NetworkManager.ServerClientId) return -1;
                if (b == NetworkManager.ServerClientId && a != NetworkManager.ServerClientId) return 1;
                return a.CompareTo(b);
            });

            if (connectedIds.Count >= 2)
            {
                // Player 1 (server/host) gets deck1
                TempPlayerDecks[connectedIds[0]] = deck1;

                // Player 2 (connected client) gets deck2
                TempPlayerDecks[connectedIds[1]] = deck2;
            }
            else
            {
                yield break;
            }

            _decksLoadedFromBackend = true;

            // Signal backend that server is ready
            yield return StartCoroutine(SignalServerReady());

            // Trigger start condition check
            EvaluateStartCondition("BackendDecksLoaded");
        }
        else
        {
        }
    }

    private IEnumerator SignalServerReady()
    {
        string backendUrl = "http://localhost:8000";
        string url = $"{backendUrl}/api/matchmaking/server-ready/";

        // Get match ID from first queue entry (all matched players have same match_id)
        // For now, we'll create a simple request body
        string jsonBody = JsonUtility.ToJson(new { status = "ready" });
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
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
            }
        }
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
}