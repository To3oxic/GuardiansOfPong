using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public EndGameUI endGameUI;
    public TextMeshProUGUI gameTimerText;
    public TextMeshProUGUI playerScoreText;
    public TextMeshProUGUI opponentScoreText;

    [Header("Mobile Input Buttons")]
    public Button leftMoveButton;  // Left movement button for mobile
    public Button rightMoveButton; // Right movement button for mobile

    [Header("Player Stats UI")]
    public Text playerManaText;  // Changed to regular Text for compatibility
    public Text playerHPText;
    public ManaVisualizer playerManaVisualizer;

    [Header("HP Bars")]
    public RectTransform playerHPBar;   // The HP bar parent object for local player (bottom bar)
    public RectTransform opponentHPBar; // The HP bar parent object for opponent (top bar)
    public RectTransform playerHPBarComponent;   // The actual BAR component for local player (bottom bar - shrinks with local player's HP)
    public RectTransform opponentHPBarComponent; // The actual BAR component for opponent (top bar - shrinks with opponent's HP)

    [Header("Spawning")]
    public GameObject ballPrefab;

    [Header("Environment Bounds")]
    public Transform leftWall;
    public Transform rightWall;
    public Transform topWall;
    public Transform bottomWall;
    public Transform player1Goal;
    public Transform player2Goal;

    [Header("Floor Renderers")]
    public MeshRenderer client1FloorRenderer;
    public MeshRenderer client2FloorRenderer;

    [Header("Placement Bounds")]
    public Transform player1PlacementZone; // Where player 1 can place cards
    public Transform player2PlacementZone; // Where player 2 can place cards

    // Simple boundary properties
    public float LeftWallX => leftWall != null ? leftWall.position.x : -25f;
    public float RightWallX => rightWall != null ? rightWall.position.x : 25f;
    public float Player1GoalZ => player1Goal != null ? player1Goal.position.z : -40f;
    public float Player2GoalZ => player2Goal != null ? player2Goal.position.z : 50f;
    public Transform ballSpawnPoint;
    public Transform client1SpawnPoint;      // Spawn point for Client 1 paddles
    public Transform client2SpawnPoint;      // Spawn point for Client 2 paddles
    public Transform client1LegendSpawnPoint;   // Spawn point for Client 1 legend avatars
    public Transform client2LegendSpawnPoint;
    public Transform client1LegendStoneSpawnPoint;   // Spawn point for Client 1 legend avatars
    public Transform client2LegendStoneSpawnPoint;  // Spawn point for Client 2 legend avatars

    [Header("Legend Stone Safe Points")]
    public Transform client1LegendSafePoint;     // Safe transformation point for Client 1 (Host player)
    public Transform client2LegendSafePoint;     // Safe transformation point for Client 2 (Client player)

    [Header("Arena bounds")]
    public float arenaMinX = -25f;
    public float arenaMaxX = 25f;
    public float arenaMinZ = -50f;
    public float arenaMaxZ = 50f;

    [Header("Game Start Animation")]
    public GameStartAnimationController gameStartAnimationController;

    // State
    private readonly NetworkVariable<float> syncedGameTime =
        new NetworkVariable<float>(180f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> playerScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> opponentScore = new NetworkVariable<int>(0);

    private bool isSuddenDeath = false;
    private bool matchEnded = false;
    private readonly NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool gameTimerStarted = false; // Separate flag for when timer should actually start
    private bool animationCompleted = false; // Flag to prevent multiple animation completion calls

    private float flashTimer = 0f;
    private bool flashOn = false;

    // Player/deck mappings
    private readonly Dictionary<ulong, int> playerIndexMap = new Dictionary<ulong, int>();             // clientId -> 0/1
    private readonly Dictionary<ulong, DeckPayload> playerDecks = new Dictionary<ulong, DeckPayload>(); // clientId -> payload
    private readonly Dictionary<ulong, List<CardData>> playerCardDecks = new Dictionary<ulong, List<CardData>>(); // clientId -> resolved cards
    private readonly Dictionary<ulong, GameObject> spawnedLegends = new Dictionary<ulong, GameObject>();
    private readonly Dictionary<ulong, GameObject> spawnedPaddles = new Dictionary<ulong, GameObject>(); // clientId -> spawned paddle instance
    private Dictionary<ulong, DeckPayload> _decksByClient; // received from NetworkUI after scene load

    private static bool IsClientOneInternal = false;
    public static bool IsClientOne => IsClientOneInternal;

    // Tracks which clients reported "ready"
    private readonly HashSet<ulong> readyClients = new HashSet<ulong>();

    // Local player paddle reference for UI updates
    private PaddleController localPlayerPaddle;
    private bool hasTriedCreatingFallbackUI = false;

    // HP Bar tracking
    private float originalPlayerHPBarWidth = 0f;
    private float originalOpponentHPBarWidth = 0f;
    private bool hpBarsInitialized = false;

    // Mana generation
    private float manaGenerationTimer = 0f;
    private const float MANA_GENERATION_INTERVAL = 2f; // Generate 1 mana every 3 seconds



    private void Awake()
    {
        Application.targetFrameRate = 60;
        // GameManager is a scene-based NetworkObject, don't use DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {

            Destroy(gameObject);
            return;
        }

        Instance = this;

        var reg = Resources.Load<DeckRegistry>("DeckRegistry");
        if (reg != null)
        {
            reg.InitializeRegistry();
        }
    }

    public override void OnNetworkSpawn()
    {

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

    }

    public override void OnDestroy()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // Check if we already have 2 players (including host)
        if (NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
        {
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }
        // Player indices will be assigned later in InitializeFromDecks() when we have deck information
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        readyClients.Remove(clientId);
        playerDecks.Remove(clientId);
        spawnedLegends.Remove(clientId);
        playerIndexMap.Remove(clientId);
    }

    private void Update()
    {
        // Server updates timer (only after animation completes and timer officially starts)
        if (IsServer && gameTimerStarted && !matchEnded)
        {
            syncedGameTime.Value -= Time.deltaTime;
            if (syncedGameTime.Value <= 0f && !isSuddenDeath)
            {
                EvaluateEndByScore();
            }
        }

        // Server: Generate mana for all players every 3 seconds
        if (IsServer && gameStarted.Value)
        {
            manaGenerationTimer += Time.deltaTime;
            if (manaGenerationTimer >= MANA_GENERATION_INTERVAL)
            {
                manaGenerationTimer = 0f;
                GenerateManaForAllPlayers();
            }
        }

        // Clients render timer
        if (IsClient && gameTimerText != null)
        {
            float displayTime = Mathf.Max(0f, syncedGameTime.Value);
            int minutes = Mathf.FloorToInt(displayTime / 60f);
            int seconds = Mathf.FloorToInt(displayTime % 60f);
            gameTimerText.text = $"{minutes:00}:{seconds:00}";

            if (displayTime <= 0f)
            {
                gameTimerText.color = Color.red;
            }
            else if (displayTime <= 10f)
            {
                FlashColor(0.5f);
            }
            else if (displayTime <= 30f)
            {
                FlashColor(1f);
            }
            else
            {
                gameTimerText.color = Color.white;
            }
        }

        // Simple score UI
        if (IsClient)
        {
            if (!IsClientOne)
            {
                if (playerScoreText) playerScoreText.text = playerScore.Value.ToString();
                if (opponentScoreText) opponentScoreText.text = opponentScore.Value.ToString();
            }
            else
            {
                if (playerScoreText) playerScoreText.text = opponentScore.Value.ToString();
                if (opponentScoreText) opponentScoreText.text = playerScore.Value.ToString();
            }
        }
    }

    private void FlashColor(float interval)
    {
        if (!gameTimerText) return;
        flashTimer += Time.deltaTime;
        if (flashTimer >= interval)
        {
            flashTimer = 0f;
            flashOn = !flashOn;
            gameTimerText.color = flashOn ? Color.red : Color.white;
        }
    }

    /// <summary>
    /// Server generates 1 mana for each player and sends ClientRpc to trigger animation
    /// </summary>
    private void GenerateManaForAllPlayers()
    {
        // Find all paddles and add mana
        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
        foreach (var paddle in allPaddles)
        {
            if (paddle != null && paddle.IsSpawned)
            {
                // Add 1 mana on server
                float newMana = paddle.currentMana.Value + 1f;
                newMana = Mathf.Min(newMana, paddle.maxMana); // Clamp to max mana
                paddle.currentMana.Value = newMana;


                // Send ClientRpc to trigger animation only for this player
                OnManaGeneratedClientRpc(paddle.OwnerClientId, newMana, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { paddle.OwnerClientId } }
                });
            }
        }
    }

    /// <summary>
    /// ClientRpc called when mana is generated - triggers visualizer animation
    /// </summary>
    [ClientRpc]
    private void OnManaGeneratedClientRpc(ulong paddleOwnerClientId, float newManaAmount, ClientRpcParams clientRpcParams = default)
    {
        // Find the local player's paddle and update visualizer
        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
        foreach (var paddle in allPaddles)
        {
            if (paddle != null && paddle.IsOwner)
            {
                // This is our local paddle, update visualizer with animation
                if (playerManaVisualizer != null)
                {
                    // Update display and trigger animation for the new filled cell
                    playerManaVisualizer.UpdateManaDisplay(newManaAmount);
                    playerManaVisualizer.AnimateManaFill();
                }
                break;
            }
        }
    }

    // Called by PaddleController when HP changes via RPC
    public void OnPaddleHPChanged(ulong paddleOwnerClientId, float newHP, float maxHP, float damageDealt)
    {

        // Initialize HP bars if not done yet
        if (!hpBarsInitialized)
        {
            InitializeHPBars();
        }

        // Don't update HP bar sizes if game hasn't started
        if (!gameStarted.Value) return;

        // Get all paddles to find which one changed
        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);

        PaddleController localPlayerPaddle = null;   // This client's paddle (bottom HP bar)
        PaddleController opponentPlayerPaddle = null; // Opponent's paddle (top HP bar)

        // Identify paddles from current client's perspective
        foreach (var paddle in allPaddles)
        {
            if (paddle.IsOwner)
            {
                localPlayerPaddle = paddle;
            }
            else
            {
                opponentPlayerPaddle = paddle;
            }
        }

        // Determine which HP bar to update based on the paddle owner
        bool isLocalPlayerDamaged = (localPlayerPaddle != null && localPlayerPaddle.OwnerClientId == paddleOwnerClientId);
        bool isOpponentDamaged = (opponentPlayerPaddle != null && opponentPlayerPaddle.OwnerClientId == paddleOwnerClientId);

        if (isLocalPlayerDamaged && playerHPBarComponent != null)
        {
            // Update local player's HP bar (bottom bar)
            float hpPercentage = maxHP > 0 ? newHP / maxHP : 0f;
            UpdateHPBarSize(playerHPBarComponent, hpPercentage, originalPlayerHPBarWidth);
        }
        else if (isOpponentDamaged && opponentHPBarComponent != null)
        {
            // Update opponent's HP bar (top bar)
            float hpPercentage = maxHP > 0 ? newHP / maxHP : 0f;
            UpdateHPBarSize(opponentHPBarComponent, hpPercentage, originalOpponentHPBarWidth);
        }
        else
        {
        }
    }

    // Called when a paddle's HP drops below 50% and legend stone needs to transform
    public void HandleLegendStoneTransformation(ulong paddleOwnerClientId)
    {
        if (!IsServer) return;


        // Get assigned player index from playerIndexMap
        if (!playerIndexMap.TryGetValue(paddleOwnerClientId, out int playerIndex))
        {
            return;
        }

        // Determine safe point position based on player index
        Vector3 safePointPosition;
        if (playerIndex == 0 && client1LegendSafePoint != null)
        {
            safePointPosition = client1LegendSafePoint.position;
        }
        else if (playerIndex == 1 && client2LegendSafePoint != null)
        {
            safePointPosition = client2LegendSafePoint.position;
        }
        else
        {

            return;
        }

        // SERVER: Disable losing condition for this player (server-side gameplay logic)

        // TODO: Implement the actual losing condition disable logic in BallController or wherever ball collision is handled

        // Server activates the passive ability (server-side gameplay effect)
        ActivatePassiveAbilityForClient(paddleOwnerClientId);

        // Handle legend stone movement on server side (NetworkTransform will sync to clients)
        MoveLegendStoneOnServer(paddleOwnerClientId, safePointPosition);
    }

    private void ActivatePassiveAbilityForClient(ulong clientId)
    {


        // Find the client's paddle
        if (spawnedPaddles.TryGetValue(clientId, out GameObject paddleGO))
        {
            var paddleController = paddleGO.GetComponent<PaddleController>();
            if (paddleController != null)
            {
                // TODO: Implement specific passive abilities here
                // For example: increase paddle speed, add shield, etc.


                // Example passive effects:
                // paddleController.ActivateSpeedBoost();
                // paddleController.ActivateShield();
                // paddleController.ActivateDamageBoost();
            }
        }
        else
        {

        }
    }



    private void MoveLegendStoneOnServer(ulong playerId, Vector3 safePointPosition)
    {
        if (!IsServer) return;


        // Get the player index from server's playerIndexMap
        if (!playerIndexMap.TryGetValue(playerId, out int playerIndex))
        {

            return;
        }

        // Find the legend stone for this player
        LegendStoneController[] allStones = FindObjectsByType<LegendStoneController>(FindObjectsSortMode.None);


        bool foundMatchingStone = false;
        for (int i = 0; i < allStones.Length; i++)
        {
            var stone = allStones[i];
            ulong stonePlayerId = stone.GetAssociatedPlayerId();


            if (stonePlayerId == playerId)
            {

                foundMatchingStone = true;

                // Move the stone on server side - NetworkTransform will sync to clients
                stone.transform.position = safePointPosition;

                // Notify clients about visual effects via RPC
                PlayLegendStoneTransformationEffectsClientRpc(playerId, safePointPosition);

                break;
            }
        }

        if (!foundMatchingStone)
        {

            // Debug all available stones
            for (int i = 0; i < allStones.Length; i++)
            {
                var stone = allStones[i];

            }
        }
    }

    [ClientRpc]
    private void PlayLegendStoneTransformationEffectsClientRpc(ulong playerId, Vector3 safePointPosition)
    {


        // Client-side visual/audio effects only
        // TODO: Implement visual effects
        // AudioManager.PlayTransformationSound();
        // UIManager.ShowPassiveActivatedNotification(playerId);
        // ParticleEffectManager.PlayTransformationEffect(safePointPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLegendStoneTransformationServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {


        // Validate that the sender is requesting transformation for their own client or is authorized
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != clientId)
        {

            return;
        }

        // Call the existing transformation logic
        HandleLegendStoneTransformation(clientId);
    }

    private void InitializeHPBars()
    {
        // Use directly assigned BAR components and store original widths
        if (playerHPBarComponent != null)
        {
            originalPlayerHPBarWidth = playerHPBarComponent.sizeDelta.x;
            ConfigureHPBarAnchoring(playerHPBarComponent);

        }
        else
        {

        }

        if (opponentHPBarComponent != null)
        {
            originalOpponentHPBarWidth = opponentHPBarComponent.sizeDelta.x;
            ConfigureHPBarAnchoring(opponentHPBarComponent);

        }
        else
        {

        }

        hpBarsInitialized = true;
    }

    private void ConfigureHPBarAnchoring(RectTransform hpBar)
    {
        // Set left-aligned anchoring so bar shrinks from right side
        hpBar.anchorMin = new Vector2(0f, 0.5f);  // Left-center anchor
        hpBar.anchorMax = new Vector2(0f, 0.5f);  // Left-center anchor  
        hpBar.pivot = new Vector2(0f, 0.5f);      // Pivot at left-center

        // Ensure position is at left edge
        Vector2 currentPos = hpBar.anchoredPosition;
        hpBar.anchoredPosition = new Vector2(0f, currentPos.y);
    }

    private void UpdateHPBarSize(RectTransform hpBar, float hpPercentage, float originalWidth)
    {
        if (hpBar == null || originalWidth <= 0f)
        {
            if (Time.frameCount % 120 == 0) // Less frequent logging
            {

            }
            return;
        }

        // Calculate new width based on HP percentage
        float clampedPercentage = Mathf.Clamp01(hpPercentage);
        float newWidth = originalWidth * clampedPercentage;

        // Debug log significant changes
        float oldWidth = hpBar.sizeDelta.x;
        if (Mathf.Abs(oldWidth - newWidth) > 1f && Time.frameCount % 30 == 0)
        {

        }

        // Update size (width only)
        hpBar.sizeDelta = new Vector2(newWidth, hpBar.sizeDelta.y);

        // Ensure position stays at left edge (should not be needed with proper anchoring, but just to be safe)
        Vector2 currentPos = hpBar.anchoredPosition;
        hpBar.anchoredPosition = new Vector2(0f, currentPos.y);
    }

    // Test method to manually show HP bars - can be called from Unity Inspector or other scripts
    [ContextMenu("Force Show HP Bars")]
    public void ForceShowHPBars()
    {


        if (playerHPBar != null)
        {
            playerHPBar.gameObject.SetActive(true);

        }
        else
        {

        }

        if (opponentHPBar != null)
        {
            opponentHPBar.gameObject.SetActive(true);

        }
        else
        {

        }


    }

    private void CreateFallbackManaUI()
    {
        hasTriedCreatingFallbackUI = true;


        try
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("FallbackCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // Create mana text
            GameObject manaTextGO = new GameObject("FallbackManaText");
            manaTextGO.transform.SetParent(canvas.transform, false);

            Text manaText = manaTextGO.AddComponent<Text>();
            manaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            manaText.fontSize = 24;
            manaText.color = Color.cyan;
            manaText.text = "Mana: --/--";

            // Position in top-left
            RectTransform rectTransform = manaTextGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(10, -10);
            rectTransform.sizeDelta = new Vector2(200, 30);

            playerManaText = manaText;

        }
        catch (System.Exception e)
        {

        }
    }

    private void EvaluateEndByScore()
    {
        if (!IsServer || matchEnded) return;

        if (NetworkManager.ConnectedClientsList.Count < 2)
        {

            return;
        }

        ulong hostId = NetworkManager.ServerClientId;
        ulong otherId = NetworkManager.ConnectedClientsIds.FirstOrDefault(id => id != hostId);


        if (playerScore.Value > opponentScore.Value)
        {
            EndGame("Score", hostId, 2);
        }
        else if (playerScore.Value < opponentScore.Value)
        {
            EndGame("Score", otherId, 2);
        }
        else
        {
            isSuddenDeath = true;

        }
    }

    public void EndGame(string type, ulong winnerId, int stars)
    {
        if (!IsServer || matchEnded)
        {
            return;
        }

        matchEnded = true;

        // Stop the game
        if (IsServer) gameStarted.Value = false;

        // Remove ball when match ends
        RemoveBall();



        Debug.Log($"[GameManager] Game ended - Type: {type}, Winner: {winnerId}, Stars: {stars}");

        // Call RPC to show end game UI on all clients
        ShowEndGameUIClientRpc(winnerId, stars, 5);
    }

    [ClientRpc]
    private void ShowEndGameUIClientRpc(ulong winnerId, int starCount, float matchDuration)
    {
        if (endGameUI == null)
        {
            Debug.LogError("[GameManager] EndGameUI is not assigned!");
            return;
        }

        // Determine if local client is the winner
        bool isWinner = (NetworkManager.Singleton.LocalClientId == winnerId);

        Debug.Log($"[GameManager] Showing end game UI - Winner: {winnerId}, LocalClient: {NetworkManager.Singleton.LocalClientId}, IsWinner: {isWinner}");

        // Show the end game UI
        endGameUI.ShowResult(isWinner, starCount, matchDuration);
    }

    public void InitializeFromDecks(Dictionary<ulong, DeckPayload> decksByClient)
    {

        if (!IsServer) return;

        _decksByClient = decksByClient != null
            ? new Dictionary<ulong, DeckPayload>(decksByClient)
            : new Dictionary<ulong, DeckPayload>();

        foreach (var kv in _decksByClient)
        {
            var p = kv.Value;

        }

        // 1) Assign indices: host=0, first client=1
        ServerAssignPlayerIndices();

        // 2) Spawn based on each deck
        foreach (var kv in _decksByClient)
        {
            var clientId = kv.Key;
            var payload = kv.Value;

            if (!playerIndexMap.ContainsKey(clientId))
            {

                continue;
            }

            RegisterPlayerDeck(clientId, payload);
        }

        // 3) Start if both are ready
        if (playerDecks.Count >= 2)
        {

            StartGameWithDecks();
            gameStarted.Value = true;
        }
        else
        {

        }

    }

    // Optional external start (also called via SendMessage from NetworkUI)


    // Assign consistent indices and notify each client
    private void ServerAssignPlayerIndices()
    {
        if (!IsServer) return;

        playerIndexMap.Clear();

        // Use actually connected clients
        var ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);


        ids.Sort((a, b) =>
        {
            if (a == NetworkManager.ServerClientId && b != NetworkManager.ServerClientId) return -1;
            if (b == NetworkManager.ServerClientId && a != NetworkManager.ServerClientId) return 1;
            return a.CompareTo(b);
        });

        int assigned = 0;
        foreach (var id in ids)
        {
            if (assigned >= 2) break;
            playerIndexMap[id] = assigned;

            // Send RPC to connected clients
            SetClientIndexClientRpc(assigned, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { id } }
            });


            assigned++;
        }




    }

    [ClientRpc]
    private void SetClientIndexClientRpc(int index, ClientRpcParams clientRpcParams = default)
    {
        IsClientOneInternal = index == 0;

    }

    private void SyncFloors()
    {
        string f0 = "";
        string f1 = "";

        foreach (var kv in playerDecks)
        {
            if (playerIndexMap.TryGetValue(kv.Key, out int index))
            {
                if (index == 0) f0 = kv.Value.floorId;
                else if (index == 1) f1 = kv.Value.floorId;
            }
        }

        SetFloorTexturesClientRpc(f0, f1);
    }

    [ClientRpc]
    private void SetFloorTexturesClientRpc(string floorId0, string floorId1)
    {
        ApplyFloorTexture(client1FloorRenderer, floorId0);
        ApplyFloorTexture(client2FloorRenderer, floorId1);
    }

    private void ApplyFloorTexture(MeshRenderer renderer, string floorId)
    {
        if (renderer == null || string.IsNullOrEmpty(floorId)) return;
        if (FloorRegistry.Instance == null)
        {
            var floorReg = Resources.Load<FloorRegistry>("FloorRegistry");
            if (floorReg != null) floorReg.InitializeRegistry();
        }

        if (FloorRegistry.Instance == null) return;

        var floorData = FloorRegistry.Instance.GetFloorById(floorId);
        if (floorData != null && floorData.floorMaterial != null)
        {
            renderer.material = floorData.floorMaterial;
        }
    }

    // Deck registration and spawning
public void RegisterPlayerDeck(ulong clientId, DeckPayload payload)
    {

        playerDecks[clientId] = payload;

        // Resolve cards for server-side validation/logic
        var resolvedCards = new List<CardData>();
        foreach (var cardId in payload.cardIds)
        {
            var card = DeckRegistry.Instance.GetCardById(cardId);
            if (card != null) resolvedCards.Add(card);
        }
        playerCardDecks[clientId] = resolvedCards;


        // Spawn paddle + legend
        if (!playerIndexMap.TryGetValue(clientId, out int index))
        {

            return;
        }

        SpawnPaddleForPlayer(clientId, payload);
        SpawnLegendForPlayer(clientId, payload);

        // Send deck back to that client for local DeckManager (UI/visuals)
        SendDeckToClientClientRpc(payload, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });


    }

    private void SpawnPaddleForPlayer(ulong clientId, DeckPayload payload)
    {
        var stone = DeckRegistry.Instance.GetLegendStoneById(payload.legendStoneId);
        if (stone == null)
        {

            return;
        }
        if (stone.AssociatedPaddlePrefab == null)
        {

            return;
        }


        Transform spawnT = (clientId == 1) ? client1SpawnPoint : client2SpawnPoint;




        // Instantiate new paddle from the stone's prefab
        var paddleGO = Instantiate(stone.AssociatedPaddlePrefab, spawnT.position, spawnT.rotation);

        var paddleController = paddleGO.GetComponent<PaddleController>();
        if (paddleController != null)
        {
            paddleController._tempInitialStoneNameForServer = stone.stoneName;
        }

        var netObj = paddleGO.GetComponent<NetworkObject>();
        if (!netObj)
        {

            Destroy(paddleGO);
            return;
        }

        netObj.SpawnWithOwnership(clientId);

        // Store the spawned paddle
        spawnedPaddles[clientId] = paddleGO;

        // Now spawn the legend stone for this paddle since they are connected
        SpawnLegendStoneForPaddle(clientId, stone, paddleGO);

        // Update the LegendStone to reference the newly spawned paddle on server
        UpdateLegendStonePaddleReference(stone.stoneName, paddleGO);

        // Also update clients with the new paddle reference
        NetworkObjectReference paddleRef = netObj;
        UpdateLegendStonePaddleReferenceClientRpc(stone.stoneName, paddleRef);


    }

    private void SpawnLegendStoneForPaddle(ulong clientId, LegendStoneData stoneData, GameObject paddleGO)
    {
        if (stoneData.legendStoneVisualPrefab == null)
        {
            return;
        }


        Transform stoneSpawnPoint = (clientId == 1) ? client1LegendStoneSpawnPoint : client2LegendStoneSpawnPoint;


        Vector3 stonePosition = stoneSpawnPoint.position;
        Quaternion stoneRotation = stoneSpawnPoint.rotation;



        // Instantiate legend stone from the stone data's prefab at the predefined spawn point
        var stoneGO = Instantiate(stoneData.legendStoneVisualPrefab, stonePosition, stoneRotation);

        var netObj = stoneGO.GetComponent<NetworkObject>();
        if (!netObj)
        {

            Destroy(stoneGO);
            return;
        }

        netObj.SpawnWithOwnership(clientId);

        // Initialize the legend stone controller
        var stoneController = stoneGO.GetComponent<LegendStoneController>();
        if (stoneController != null)
        {
            var paddleController = paddleGO.GetComponent<PaddleController>();
            stoneController.Initialize(paddleController, clientId);

        }
        else
        {

            if (netObj.IsSpawned) netObj.Despawn();
            else Destroy(stoneGO);
        }
    }

    private void UpdateLegendStonePaddleReference(string stoneName, GameObject spawnedPaddle)
    {
        // Find all LegendStoneController instances in the scene
        LegendStoneController[] allLegendStones = FindObjectsByType<LegendStoneController>(FindObjectsSortMode.None);

        foreach (var legendStone in allLegendStones)
        {
            // Check if this is the matching stone by comparing the stone name from DeckRegistry
            var stoneData = DeckRegistry.Instance.GetLegendStoneByName(stoneName);
            if (stoneData != null && legendStone.associatedPaddlePrefab == stoneData.AssociatedPaddlePrefab)
            {
                // Update the stone's paddle reference to the spawned instance
                legendStone.associatedPaddlePrefab = spawnedPaddle;


                // If the stone already has a spirit beam, reinitialize it with the new paddle
                var spiritBeam = legendStone.GetComponentInChildren<SpiritBeamController>();
                if (spiritBeam != null)
                {
                    // Create a temporary BeamSettings if needed
                    BeamSettings settings = legendStone.customBeamSettings ?? new BeamSettings();
                    spiritBeam.Initialize(legendStone.transform, spawnedPaddle.transform, settings);

                }

                break; // Found the matching stone, no need to continue
            }
        }
    }

    [ClientRpc]
    private void UpdateLegendStonePaddleReferenceClientRpc(string stoneName, NetworkObjectReference paddleNetRef)
    {
        // Convert NetworkObjectReference back to GameObject
        if (paddleNetRef.TryGet(out NetworkObject paddleNetObj))
        {
            GameObject spawnedPaddle = paddleNetObj.gameObject;

            // Find all LegendStoneController instances in the scene on client
            LegendStoneController[] allLegendStones = FindObjectsByType<LegendStoneController>(FindObjectsSortMode.None);

            foreach (var legendStone in allLegendStones)
            {
                // Check if this is the matching stone by comparing the stone name from DeckRegistry
                var stoneData = DeckRegistry.Instance.GetLegendStoneByName(stoneName);
                if (stoneData != null && legendStone.associatedPaddlePrefab == stoneData.AssociatedPaddlePrefab)
                {
                    // Update the stone's paddle reference to the spawned instance
                    legendStone.associatedPaddlePrefab = spawnedPaddle;


                    // If the stone already has a spirit beam, reinitialize it with the new paddle
                    var spiritBeam = legendStone.GetComponentInChildren<SpiritBeamController>();
                    if (spiritBeam != null)
                    {
                        // Create a temporary BeamSettings if needed
                        BeamSettings settings = legendStone.customBeamSettings ?? new BeamSettings();
                        spiritBeam.Initialize(legendStone.transform, spawnedPaddle.transform, settings);

                    }

                    break; // Found the matching stone, no need to continue
                }
            }
        }
        else
        {

        }
    }

    private void SpawnLegendForPlayer(ulong clientId, DeckPayload payload)
    {
        // Get assigned player index from playerIndexMap
        if (!playerIndexMap.TryGetValue(clientId, out int playerIndex))
        {

            return;
        }

        // Fixed assignment based on player index:
        // Player index 0 -> client1LegendSpawnPoint
        // Player index 1 -> client2LegendSpawnPoint

        Transform spawn = (playerIndex == 0) ? client1LegendSpawnPoint : client2LegendSpawnPoint;

        if (spawn == null)
        {
            string spawnPointName = (playerIndex == 0) ? "client1LegendSpawnPoint" : "client2LegendSpawnPoint";

            return;
        }


        var legendData = DeckRegistry.Instance.GetCastingLegendById(payload.castingLegendId);
        if (legendData == null || legendData.legendAvatarPrefab == null)
        {

            return;
        }


        GameObject legend = Instantiate(legendData.legendAvatarPrefab, spawn.position, spawn.rotation);
        var netObj = legend.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(clientId);
            spawnedLegends[clientId] = legend;

        }
        else
        {

            Destroy(legend);
        }
    }

    private void StartGameWithDecks()
    {
        if (!IsServer) return;


        // Reset score/time
        playerScore.Value = 0;
        opponentScore.Value = 0;
        isSuddenDeath = false;
        matchEnded = false;
        animationCompleted = false; // Reset animation completion flag
        // Don't set gameStarted = true yet, wait for animation to complete

        // Start the animation sequence instead of immediately spawning ball
        if (gameStartAnimationController == null)
        {
            // Try to find the animation controller in the scene
            gameStartAnimationController = FindFirstObjectByType<GameStartAnimationController>();
        }

        if (gameStartAnimationController != null)
        {

            gameStartAnimationController.OnAnimationComplete += OnGameStartAnimationComplete;
            gameStartAnimationController.StartGameAnimation();
        }
        else
        {

            gameStarted.Value = true;
            gameTimerStarted = true; // Also start timer immediately if no animation
            SpawnBall();
        }

    }

    private void OnGameStartAnimationComplete()
    {


        if (!IsServer)
        {

            return;
        }

        if (animationCompleted)
        {

            return;
        }

        animationCompleted = true;


        // Legend stones are now spawned directly with paddles, no separate initialization needed

        // Unsubscribe from the event
        if (gameStartAnimationController != null)
        {
            gameStartAnimationController.OnAnimationComplete -= OnGameStartAnimationComplete;
        }

        // Now actually start the game AND the timer
        gameStarted.Value = true;
        gameTimerStarted = true;


        SpawnBall();

    }

    private void SpawnBall()
    {


        if (!IsServer)
        {

            return;
        }

        if (NetworkManager.Singleton == null)
        {

            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {

            return;
        }

        if (ballPrefab == null)
        {

            return;
        }

        // Check if ball prefab has NetworkObject
        var ballNetObj = ballPrefab.GetComponent<NetworkObject>();
        if (ballNetObj == null)
        {

            return;
        }

        if (ballSpawnPoint == null)
        {

            return;
        }



        // Check if there's already a ball in the scene
        var existingBall = FindFirstObjectByType<BallController>();
        if (existingBall != null)
        {

            var oldNetObj = existingBall.GetComponent<NetworkObject>();
            if (oldNetObj != null && oldNetObj.IsSpawned)
            {
                oldNetObj.Despawn(true);
            }
            else if (existingBall != null)
            {
                Destroy(existingBall.gameObject);
            }
        }

        try
        {

            var ball = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);


            var netObj = ball.GetComponent<NetworkObject>();

            if (netObj != null)
            {

                netObj.Spawn();
                ball.GetComponent<BallController>().LaunchBall();

            }
            else
            {

                Destroy(ball);
            }
        }
        catch (System.Exception e)
        {

        }
    }

    private void RemoveBall()
    {
        if (!IsServer) return;

        var ball = FindFirstObjectByType<BallController>();
        if (ball != null)
        {
            var netObj = ball.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {

                netObj.Despawn();
            }
        }
    }

    public void ResetBall()
    {
        if (!IsServer) return;

        // Spawn a fresh ball at the center
        try
        {
            if (ballPrefab == null)
            {

                return;
            }

            var ball = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);


            var netObj = ball.GetComponent<NetworkObject>();

            if (netObj != null)
            {

                netObj.Spawn();


                // Update BallController.Instance to the new ball
                BallController ballController = ball.GetComponent<BallController>();
                if (ballController != null)
                {

                }
            }
            else
            {

                Destroy(ball);
            }
        }
        catch (System.Exception e)
        {

        }
    }

    // Scoring API (server-only)
    public void OpponentLosesPoint()
    {
        if (!IsServer) return;
        playerScore.Value++;

        // Wait for ball to be destroyed before respawning
        StartCoroutine(WaitForBallDestroyedThenReset());
    }

    public void PlayerLosesPoint()
    {
        if (!IsServer) return;
        opponentScore.Value++;

        // Wait for ball to be destroyed before respawning
        StartCoroutine(WaitForBallDestroyedThenReset());
    }

    private System.Collections.IEnumerator WaitForBallDestroyedThenReset()
    {
        // Wait until the ball is null (destroyed)
        while (BallController.Instance != null)
        {
            yield return null;
        }

        SpawnBall();
    }



    [ClientRpc]
    private void SendDeckToClientClientRpc(DeckPayload payload, ClientRpcParams clientRpcParams = default)
    {


        if (DeckManager.Instance != null)
        {
            var reconstructedDeck = ScriptableObject.CreateInstance<PlayerDeckData>();
            reconstructedDeck.selectedCards = payload.cardIds
                .Select(id => DeckRegistry.Instance.GetCardById(id))
                .Where(card => card != null)
                .ToList();

            reconstructedDeck.selectedCastingLegend = DeckRegistry.Instance.GetCastingLegendById(payload.castingLegendId);
            reconstructedDeck.selectedLegendStone = DeckRegistry.Instance.GetLegendStoneById(payload.legendStoneId);

            DeckManager.Instance.LoadDeck(reconstructedDeck);


            // Initialize CardManager UI after loading the deck
            var cardManager = FindFirstObjectByType<CardManager>();
            if (cardManager != null)
            {

                cardManager.StartCardUIInitialization();
            }
            else
            {

            }
        }
        else
        {

        }
    }

    // Mana management
    private bool TryConsumeMana(ulong playerId, float manaCost)
    {
        // Find the player's paddle
        if (!spawnedPaddles.TryGetValue(playerId, out GameObject paddleGO))
        {

            return false;
        }

        PaddleController paddle = paddleGO.GetComponent<PaddleController>();
        if (paddle == null)
        {

            return false;
        }

        // Check if player has enough mana
        if (paddle.currentMana.Value < manaCost)
        {

            return false;
        }

        // Consume the mana
        paddle.currentMana.Value -= manaCost;


        // Send mana update only to the player's client
        OnManaGeneratedClientRpc(playerId, paddle.currentMana.Value, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { playerId } }
        });

        return true;
    }

    // Effects (server to client)
    [ServerRpc(RequireOwnership = false)]
    public void ApplyCardEffectServerRpc(string cardName, ulong playerId, Vector3 position = default, Vector3 direction = default)
    {

        if (playerCardDecks.TryGetValue(playerId, out var cards))
        {
            var cardData = cards.Find(c => c.cardName == cardName);
            if (cardData != null)
            {
                // Check if player has enough mana
                if (!TryConsumeMana(playerId, cardData.manaCost))
                {

                    return; // Not enough mana, don't play the card
                }

                // Get card level from the deck payload
                int cardLevel = 1;
                if (playerDecks.TryGetValue(playerId, out var deckPayload))
                {
                    cardLevel = deckPayload.GetCardLevel(cardName);
                    Debug.Log($"[GameManager] Playing card '{cardName}' at level {cardLevel}");
                }

                // Apply the card effect (handles spawning and animation internally)
                if (cardData.effect != null)
                {
                    if (cardData.effect.RequiresPlacement())
                    {
                        cardData.effect.ApplyEffect(playerId, position, direction, cardData, cardLevel);
                        PlayCardEffectClientRpc(cardName, playerId, position, direction);
                    }
                    else
                    {
                        cardData.effect.ApplyEffect(playerId, cardData, cardLevel);
                        PlayCardEffectClientRpc(cardName, playerId);
                    }
                }
            }
            else
            {

            }
        }
        else
        {

        }
    }

    [ClientRpc]
    private void PlayCardEffectClientRpc(string cardName, ulong playerId, Vector3 position = default, Vector3 direction = default)
    {

        var cardData = DeckRegistry.Instance.GetCardByName(cardName);
        if (cardData == null || cardData.effect == null)
        {

            return;
        }

        cardData.effect.ApplyVisualEffect(playerId, position, direction);

        GameObject legendToAnimate = null;
        var spawnMgr = NetworkManager.Singleton?.SpawnManager;
        if (spawnMgr != null)
        {
            foreach (var so in spawnMgr.SpawnedObjects.Values)
            {
                if (so.OwnerClientId == playerId && so.GetComponent<Animator>() != null)
                {
                    legendToAnimate = so.gameObject;
                    break;
                }
            }
        }
        if (legendToAnimate != null)
            TriggerLegendCast(legendToAnimate);

    }

    private void TriggerLegendCast(GameObject legend)
    {
        if (!legend) return;
        var anim = legend.GetComponent<Animator>();
        if (anim) anim.SetTrigger("Cast");
    }

    // Defeat triggers
    public void TriggerLegendStoneDefeat(ulong loserId)
    {

        if (!IsServer)
        {
            return;
        }

        // Legend stone defeat overrides timer-based ending - it's an instant win condition
        if (matchEnded)
        {
        }


        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;

        ulong winnerId = connectedClients.First(id => id != loserId);

        // Reset match ended to allow this instant win condition
        matchEnded = false;
        EndGame("LegendStone", winnerId, 1);
    }

    public void TriggerPaddleDefeat(ulong loserId)
    {
        if (!IsServer || matchEnded) return;
        ulong winnerId = NetworkManager.Singleton.ConnectedClientsIds.First(id => id != loserId);
        EndGame("Paddle", winnerId, 3);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendClientReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (readyClients.Add(sender))
        {

        }

        // Start match when both players are ready and decks are present
        if (!gameStarted.Value && readyClients.Count >= 2 && playerDecks.Count >= 2)
        {
            SyncFloors();
            StartGameWithDecks();
            gameStarted.Value = true;
        }
else
        {

        }

    }
}
