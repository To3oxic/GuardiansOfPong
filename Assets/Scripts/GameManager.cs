using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;


public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public EndGameUI endGameUI;
    public GameObject client1Paddle;
    public GameObject client2Paddle;
    public GameObject ballPrefab;
    public GameObject client1LS;
    public GameObject client2LS;
    public Transform[] spawnPoints; // Array of spawn points for client-only model
    public Transform ballSpawnPoint;
    public Transform[] legendSpawnPoints;
    public Transform[] stoneSpawnPoints;
    public Text playerScoreText;
    public Text opponentScoreText;
    private float flashTimer = 0f;
    private bool flashOn = false;

    private Dictionary<ulong, GameObject> spawnedLegends = new();

    private NetworkVariable<float> syncedGameTime = new NetworkVariable<float>(
        180f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isSuddenDeath = false;
    private bool matchEnded = false;
    public bool gameStarted = false;
    public TextMeshProUGUI gameTimerText;
    private NetworkVariable<int> playerScore = new NetworkVariable<int>(0);
    private NetworkVariable<int> opponentScore = new NetworkVariable<int>(0);

    private Dictionary<ulong, int> playerIndexMap = new();
    private Dictionary<ulong, DeckPayload> playerDecks = new();
    private Dictionary<ulong, string> sharedLegends = new();
    private HashSet<ulong> clientsReady = new();

    private static bool IsClientOneInternal = false;
    public static bool IsClientOne => IsClientOneInternal;

    public Dictionary<ulong, List<CardData>> playerCardDecks = new();


    private void Awake()
    {
        Instance = this;
        DeckRegistry registry = Resources.Load<DeckRegistry>("DeckRegistry");
        if (registry != null)
        {
            registry.InitializeRegistry();
        }
        else
        {
            Debug.LogError("DeckRegistry not found in Resources folder!");
        }
    }
    private void SpawnLegendForPlayer(ulong clientId, int index)
    {
        Transform spawn = legendSpawnPoints[index];
        var deck = playerDecks[clientId];
        var legendData = DeckRegistry.Instance.GetCastingLegendByName(deck.castingLegendName);

        if (legendData != null && legendData.legendAvatarPrefab != null)
        {
            GameObject legend = Instantiate(legendData.legendAvatarPrefab, spawn.position, spawn.rotation);
            NetworkObject netObj = legend.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnWithOwnership(clientId);
                spawnedLegends[clientId] = legend;
            }
        }
    }
    private void Update()
    {
        if (IsServer && gameStarted)
        {
            syncedGameTime.Value -= Time.deltaTime;
        }
        if (IsClient && gameTimerText != null)
        {
            float displayTime = syncedGameTime.Value;

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
        if (syncedGameTime.Value <= 0f && !isSuddenDeath)
        {
            EvaluateEndByScore();
        }
    }
    private void FlashColor(float interval)
    {
        flashTimer += Time.deltaTime;

        if (flashTimer >= interval)
        {
            flashTimer = 0f;
            flashOn = !flashOn;
            gameTimerText.color = flashOn ? Color.red : Color.white;
        }
    }
    private void EvaluateEndByScore()
    {
        if (playerScore.Value > opponentScore.Value)
        {
            EndGame("Score", NetworkManager.ConnectedClientsList[0].ClientId, 2);
        }
        else if (playerScore.Value < opponentScore.Value)
        {
            EndGame("Score", NetworkManager.ConnectedClientsList[1].ClientId, 2);
        }
        else
        {
            isSuddenDeath = true;
            Debug.Log("[GameManager] Entering Sudden Death!");
        }
    }
    public void EndGame(string type, ulong winnerId, int stars)
    {
        if (matchEnded) return;
        matchEnded = true;

        Debug.Log($"[GameManager] Game ended by {type}. Winner: {winnerId}, Stars: {stars}");
        ShowResultClientRpc(winnerId, stars, syncedGameTime.Value);

    }

    public override void OnNetworkSpawn()
    {
        Debug.Log(IsClientOne);
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        if (playerIndexMap.Count == 2) return;
        if (clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("[GameManager] Skipping server-only connection.");
            return;
        }
        if (playerIndexMap.ContainsKey(clientId))
        {
            Debug.Log($"[GameManager] Client {clientId} already assigned a paddle.");
            return;
        }

        int spawnIndex = playerIndexMap.Count;
        SetClientIndexClientRpc(spawnIndex, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });

        if (spawnIndex >= spawnPoints.Length)
        {
            Debug.LogError("Too many players! Not enough spawn points.");
            return;
        }
        playerIndexMap[clientId] = spawnIndex;
        GameObject paddle = spawnIndex == 0 ? client1Paddle : client2Paddle;
        string spawnName = spawnIndex == 0 ? "client1" : "client2";
        Transform spawn = spawnPoints.FirstOrDefault(t => t.name == spawnName);

        paddle = Instantiate(paddle, spawn.position, spawn.rotation);
        paddle.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);

        Debug.Log($"[GameManager] Spawned paddle for client {clientId} at index {spawnIndex}");

        GameObject stone = spawnIndex == 0 ? client1LS : client2LS;
        spawnName = spawnIndex == 0 ? "client1LSSpawn" : "client2LSSpawn";
        spawn = stoneSpawnPoints.FirstOrDefault(t => t.name == spawnName);
        stone = Instantiate(stone, spawn.position, spawn.rotation);
        stone.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        stone.GetComponent<LegendStone>().ownerClientId = clientId;

    }



    public void RegisterPlayerDeck(ulong clientId, DeckPayload payload)
    {
        playerDecks[clientId] = payload;
        Debug.Log($"[GameManager] Deck registered for client {clientId}");

        List<CardData> resolvedCards = new();
        foreach (var name in payload.cardNames)
        {
            var card = DeckRegistry.Instance.GetCardByName(name);
            if (card != null) resolvedCards.Add(card);
        }
        playerCardDecks[clientId] = resolvedCards;
        sharedLegends[clientId] = payload.castingLegendName;
        int index = playerIndexMap[clientId];
        SpawnLegendForPlayer(clientId, index);
        SendDeckToClientClientRpc(payload, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });

        if (playerDecks.Count == 2)
        {
            Debug.Log("[GameManager] All decks received. Queuing match start...");
            StartCoroutine(StartGameNextFrame());
        }

    }

    [ClientRpc]
    void SendDeckToClientClientRpc(DeckPayload payload, ClientRpcParams clientRpcParams = default)

    {
        if (DeckManager.Instance != null)
        {
            PlayerDeckData reconstructedDeck = ScriptableObject.CreateInstance<PlayerDeckData>();
            reconstructedDeck.selectedCards = payload.cardNames
                .Select(name => DeckRegistry.Instance.GetCardByName(name))
                .Where(card => card != null)
                .ToList();

            reconstructedDeck.selectedCastingLegend = DeckRegistry.Instance.GetCastingLegendByName(payload.castingLegendName);
            reconstructedDeck.selectedLegendStone = DeckRegistry.Instance.GetLegendStoneByName(payload.legendStoneName);

            DeckManager.Instance.LoadDeck(reconstructedDeck);
            Debug.Log("[GameManager] Deck sent to DeckManager and loaded");
        }
        else
        {
            Debug.LogWarning("[GameManager] DeckManager.Instance is null!");
        }
    }



    private IEnumerator StartGameNextFrame()
    {
        if (clientsReady.Count != 2)
        {
            yield return null;
        }

        StartGameWithDecks();
    }

    private void StartGameWithDecks()
    {
        SpawnBall();
        gameStarted = true;
    }

    private void SpawnBall()
    {
        if (ballPrefab == null || ballSpawnPoint == null) return;

        GameObject ball = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        ball.GetComponent<NetworkObject>().Spawn();
    }

    public void ResetBall()
    {
        BallController ball = FindFirstObjectByType<BallController>();
        if (ball != null)
        {
            ball.ResetToCenter();
        }
    }


    [ClientRpc]
    private void ShowResultClientRpc(ulong winnerId, int stars, float time)
    {
        bool isWinner = winnerId == NetworkManager.Singleton.LocalClientId;
        endGameUI.ShowResult(isWinner, stars, time);

    }


    public void OpponentLosesPoint()
    {
        if (!IsServer) return;
        playerScore.Value++;
        ResetBall();
    }

    public void PlayerLosesPoint()
    {
        if (!IsServer) return;
        opponentScore.Value++;
        ResetBall();
    }


    [ServerRpc(RequireOwnership = false)]
    public void SubmitDeckServerRpc(DeckPayload payload, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        RegisterPlayerDeck(clientId, payload);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyCardEffectServerRpc(string cardName, ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        Debug.Log($"[GameManager-Server] ApplyCardEffectServerRpc invoked. Card: {cardName}, Player: {playerId}, Position: {position}, Direction: {direction}");
        PlayCardEffectClientRpc(cardName, playerId, position, direction);

        if (playerCardDecks.TryGetValue(playerId, out var cards))
        {
            CardData cardData = cards.Find(card => card.cardName == cardName);
            if (cardData != null && cardData.effect != null)
            {
                if (cardData.effect.RequiresPlacement())
                {
                    cardData.effect.ApplyEffect(playerId, position, direction);
                }
                else
                {
                    cardData.effect.ApplyEffect(playerId);
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] Card not found or has no effect: " + cardName);
            }
        }
    }



    public void TriggerLegendStoneDefeat(ulong loserId)
    {
        if (!IsServer || matchEnded) return;

        ulong winnerId = NetworkManager.Singleton.ConnectedClientsIds.First(id => id != loserId);

        EndGame("LegendStone", winnerId, 1);
    }


    [ClientRpc]
    private void PlayCardEffectClientRpc(string cardName, ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        Debug.Log($"[GameManager] Client {NetworkManager.Singleton.LocalClientId}: Received PlayCardEffectClientRpc for {cardName} by player {playerId}");

        CardData cardData = DeckRegistry.Instance.GetCardByName(cardName);
        if (cardData == null)
        {
            Debug.LogError($"[GameManager] Client: CardData not found for {cardName}");
            return;
        }
        if (cardData.effect == null)
        {
            Debug.LogError($"[GameManager] Client: CardEffect not found for {cardName}");
            return;
        }

        // Log the type of the effect
        Debug.Log($"[GameManager] Client {NetworkManager.Singleton.LocalClientId}: Effect type is {cardData.effect.GetType()} for card {cardName}. Calling ApplyVisualEffect.");

        cardData.effect.ApplyVisualEffect(playerId, position, direction);

        // Find the legend GameObject on the client
        GameObject legendToAnimate = null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            foreach (var spawnedObject in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                // TODO: Replace this with a more specific check for your legend objects.
                // For example, check for a specific tag: if (spawnedObject.OwnerClientId == playerId && spawnedObject.CompareTag("Legend"))
                // Or a specific component: if (spawnedObject.OwnerClientId == playerId && spawnedObject.GetComponent<YourLegendControllerScript>() != null)
                if (spawnedObject.OwnerClientId == playerId && spawnedObject.GetComponent<Animator>() != null)
                {
                    legendToAnimate = spawnedObject.gameObject;
                    break;
                }
            }
        }

        if (legendToAnimate != null)
        {
            TriggerLegendCast(legendToAnimate);
        }
        else
        {
            Debug.LogWarning($"[GameManager] Client {NetworkManager.Singleton.LocalClientId}: Could not find spawned legend for player {playerId} to animate.");
        }
    }
    public void TriggerPaddleDefeat(ulong loserId)
    {
        if (!IsServer || matchEnded) return;

        ulong winnerId = NetworkManager.Singleton.ConnectedClientsIds.First(id => id != loserId);

        EndGame("Paddle", winnerId, 3);
    }


    [ServerRpc(RequireOwnership = false)]
    public void PlayCardServerRpc(string cardName, ulong playerId)
    {
        Debug.Log("[GameManager] Playing card: " + cardName);
        ApplyCardEffectServerRpc(cardName, playerId);
    }
    [ServerRpc(RequireOwnership = false)]
    public void SendClientReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!clientsReady.Contains(clientId))
        {
            clientsReady.Add(clientId);
            Debug.Log($"[GameManager] Client {clientId} is ready ({clientsReady.Count}/2)");

            if (clientsReady.Count == 2 && playerDecks.Count == 2)
            {
                Debug.Log("[GameManager] All players are ready! Starting the game now.");
                gameStarted = true;
            }
        }
    }

    [ClientRpc]
    private void SetClientIndexClientRpc(int index, ClientRpcParams clientRpcParams = default)
    {
        IsClientOneInternal = index == 0;
        Debug.Log("[GameManager] This client is " + (IsClientOneInternal ? "Client ONE" : "Client TWO"));
    }

    private void TriggerLegendCast(GameObject legend)
    {
        if (legend != null)
        {
            Animator anim = legend.GetComponent<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("Cast");
            }
        }
    }





}