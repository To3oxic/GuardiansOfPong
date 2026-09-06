# Client Connection Integration with HeadlessServer

## How Clients Use HeadlessServer.cs to Join the Game

### Trigger Point: MatchmakingManager Detects "Ready" Status

**File:** `MatchmakingManager.cs` line 270-280

```csharp
private void OnStatusResponse(string response)
{
    var json = JsonUtility.FromJson<StatusResponse>(response);
    
    if (json.status == "matched")
    {
        // Only connect if server is ready
        if (json.match_instance.status == "ready" || json.match_instance.status == "running")
        {
            // STOP POLLING
            isSearching = false;
            
            // EXTRACT SERVER INFO
            OnMatchFound(json);  // ← Extract IP:port
        }
    }
}
```

### Connection Flow in MatchmakingManager

**Step 1: OnMatchFound - Extract Server Information**

```csharp
private void OnMatchFound(StatusResponse matchInfo)
{
    var matchInstance = matchInfo.match_instance;
    
    // Extract from backend response
    serverIp = matchInstance.server_ip;           // e.g., "127.0.0.1"
    serverPort = matchInstance.server_port;       // e.g., 7777
    
    // Start connection routine
    StartCoroutine(ConnectToGameServer());
}
```

**Step 2: ConnectToGameServer - Delegate to HeadlessServer**

```csharp
private IEnumerator ConnectToGameServer()
{
    Debug.Log($"[MatchmakingManager] Attempting to connect to {serverIp}:{serverPort}");
    
    yield return new WaitForSeconds(0.5f);
    
    // Hide matchmaking UI
    HideMatchmakingUI();
    
    // ★ CALL HEADLESSSERVER ★
    // 1. Set the connection configuration
    HeadlessServer.SetClientConnectionConfig(serverIp, (ushort)serverPort);
    
    // 2. Start the client connection routine
    yield return HeadlessServer.Instance.StartCoroutine(
        HeadlessServer.Instance.GetClientConnectionRoutine()
    );
    
    Debug.Log("[MatchmakingManager] HeadlessServer client connection complete");
}
```

## HeadlessServer Client Connection Process

### Static Configuration Method

```csharp
public static void SetClientConnectionConfig(string serverIp, ushort serverPort)
{
    _clientConnectionIp = serverIp;
    _clientConnectionPort = serverPort;
    Debug.Log($"[HeadlessServer] Client connection config set: {_clientConnectionIp}:{_clientConnectionPort}");
}
```

**Called by:** MatchmakingManager after extracting server info from backend response

**Parameters:**
- `serverIp`: Server IP from `match_instance.server_ip`
- `serverPort`: Server port from `match_instance.server_port`

### Public Routine Getter

```csharp
public IEnumerator GetClientConnectionRoutine()
{
    yield return StartCoroutine(ClientConnectionRoutine());
}
```

**Called by:** MatchmakingManager to get a reference to the client connection coroutine

**Returns:** IEnumerator that handles the entire client connection flow

### ClientConnectionRoutine - The Main Logic

```csharp
private IEnumerator ClientConnectionRoutine()
{
    Debug.Log("[HeadlessServer] === Client Connection START ===");
    
    var nm = NetworkManager.Singleton;
    
    // Step 1: Configure transport for client connection
    Debug.Log("[HeadlessServer] Step 1: Configuring transport for client");
    ConfigureTransportForClient();  // Uses _clientConnectionIp:_clientConnectionPort
    
    // Step 2: Register callbacks
    Debug.Log("[HeadlessServer] Step 2: Registering client callbacks");
    nm.OnClientConnectedCallback += OnClientConnectedToServer;
    nm.OnClientDisconnectCallback += OnClientDisconnectedFromServer;
    
    if (nm.CustomMessagingManager != null)
    {
        nm.CustomMessagingManager.RegisterNamedMessageHandler(
            DeckAssignMsg, 
            OnClientReceiveDeckAssignment
        );
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
    
    Debug.Log("[HeadlessServer] === Client Connection COMPLETE ===");
}
```

### What Happens After Connection

**Once `OnClientConnectedToServer` fires:**

```csharp
private void OnClientConnectedToServer(ulong clientId)
{
    // Client side: we connected to server
    if (nm.IsClient && !nm.IsServer && clientId == nm.LocalClientId)
    {
        Debug.Log($"[HeadlessServer] Connected to server as client {clientId}");
        
        // Auto-submit player ID after a short delay
        StartCoroutine(SubmitPlayerIdAfterDelay());
    }
}

private IEnumerator SubmitPlayerIdAfterDelay()
{
    yield return new WaitForSeconds(0.5f);
    
    // Get player ID from BackendManager
    string playerId = GetPlayerIdForClient();
    
    if (!string.IsNullOrEmpty(playerId))
    {
        Debug.Log($"[HeadlessServer] Submitting player ID to server: {playerId}");
        SendPlayerIdToServer(playerId);
    }
}
```

**The server receives the player ID:**
- Server's `OnClientSubmitPlayerId` is triggered
- Maps `clientId → playerId`
- Once both clients submit IDs, server fetches decks
- Server loads GameScene via NetworkManager
- Game starts!

---

## Complete Data Flow Diagram

```
MATCHMAKING                           BACKEND                         HEADLESSSERVER
    │                                    │                                  │
    │  GET /queue-status/               │                                  │
    ├───────────────────────────────────>│                                  │
    │                                    │                                  │
    │  Response:                         │                                  │
    │  ✓ status: "matched"               │                                  │
    │  ✓ match_instance.status: "ready"  │                                  │
    │  ✓ server_ip: "127.0.0.1"          │                                  │
    │  ✓ server_port: 7777               │                                  │
    │<───────────────────────────────────┤                                  │
    │                                    │                                  │
    │  OnStatusResponse()                │                                  │
    │  └─ Extract IP + Port              │                                  │
    │  └─ OnMatchFound()                 │                                  │
    │     └─ ConnectToGameServer()       │                                  │
    │                                    │                                  │
    │  SET CONNECTION CONFIG             │                                  │
    │  ─────────────────────────────────────────────────────────────────────>
    │  HeadlessServer.SetClientConnectionConfig("127.0.0.1", 7777)         │
    │                                    │                                  │
    │  GET CLIENT ROUTINE                │                                  │
    │  ─────────────────────────────────────────────────────────────────────>
    │  GetClientConnectionRoutine()      │                                  │
    │                                    │                                  │
    │  [ClientConnectionRoutine Executes on Client]                        │
    │  ├─ Configure transport with 127.0.0.1:7777                          │
    │  ├─ RegisterNetworkCallbacks                                         │
    │  ├─ StartClient()                                                    │
    │  └─ WaitForConnection()                                              │
    │                                    │                                  │
    │                                    │                                  Connect
    │                                    │     to server ────────────────────>
    │                                    │                                  │
    │                                    │                          OnClientConnected
    │                                    │     <──────────────────  SubmitPlayerIdAfter
    │                                    │                          Delay()
    │                                    │                          │
    │                                    │                    GetPlayerIdForClient()
    │                                    │                    SendPlayerIdToServer()
    │                                    │                          │
    │                                    │         Message: PlayerIdSubmit ──>
    │                                    │                          │
    │                                    │                   OnClientSubmitPlayerId
    │                                    │                   Map clientId→playerId
    │                                    │                          │
    │                                    │         [Server now has both client IDs]
    │                                    │         [Server fetches both decks]
    │                                    │         [Server loads GameScene]
    │                                    │         [Server calls GameManager]
    │                                    │                          │
    │                                    │  <- Server Ready Signal  │
    │                                    │
    │  [Receive Scene Load from Server]  │
    │  LoadScene("GameScene")            │
    │                                    │
    │  [Game Starts!]                    │
    │
```

---

## Code Integration Summary

### MatchmakingManager.cs Changes

**Location:** `ConnectToGameServer()` method

**Before:**
```csharp
// Old code: Direct transport configuration and connection
transport.SetConnectionData(serverIp, (ushort)serverPort);
NetworkManager.Singleton.StartClient();
```

**After:**
```csharp
// New code: Delegate to HeadlessServer
HeadlessServer.SetClientConnectionConfig(serverIp, (ushort)serverPort);
yield return HeadlessServer.Instance.StartCoroutine(
    HeadlessServer.Instance.GetClientConnectionRoutine()
);
```

### HeadlessServer.cs Changes

**New Static Methods:**
- `SetClientConnectionConfig(string ip, ushort port)` - Set connection target
- `GetClientConnectionRoutine()` - Get the routine to execute

**Uses Static Variables:**
```csharp
private static string _clientConnectionIp = "127.0.0.1";
private static ushort _clientConnectionPort = 7777;
```

**Modified ConfigureTransportForClient():**
- Now uses `_clientConnectionIp` and `_clientConnectionPort` instead of hardcoded values

---

## Execution Flow (Step by Step)

1. **Player in queue sees server ready**
   ```
   GET /queue-status/ → status: "ready", server_ip: "127.0.0.1", port: 7777
   ```

2. **MatchmakingManager extracts and calls HeadlessServer**
   ```csharp
   HeadlessServer.SetClientConnectionConfig("127.0.0.1", 7777);
   yield return HeadlessServer.Instance.GetClientConnectionRoutine();
   ```

3. **HeadlessServer.ClientConnectionRoutine executes**
   ```
   ✓ ConfigureTransportForClient() - uses the set IP:port
   ✓ RegisterCallbacks
   ✓ StartClient()
   ✓ WaitForClientConnection()
   ```

4. **Client connects to server**
   ```
   ClientConnectionRoutine waits for OnClientConnectedToServer callback
   ```

5. **Connection established**
   ```
   OnClientConnectedToServer fires
   SubmitPlayerIdAfterDelay() auto-triggers
   SendPlayerIdToServer() sends player ID
   ```

6. **Server processes**
   ```
   OnClientSubmitPlayerId receives ID
   Maps clientId → playerId
   Once both received: FetchPlayerDecks() → LoadGameScene() → GameManager.Initialize()
   ```

7. **Scene loads and game starts**
   ```
   Both clients load GameScene
   GameManager.InitializeFromDecks() called
   Game runs with synced network state
   ```

---

## Key Design Points

✅ **Separation of Concerns:**
- MatchmakingManager: Queue polling and server info extraction
- HeadlessServer: Network connection and player ID submission
- NetworkUI: Legacy support (can be disabled for matchmaking flow)

✅ **Reusability:**
- HeadlessServer works for both server and client modes
- Same class handles both flows based on headless detection

✅ **Auto-Submission:**
- Client automatically submits player ID after connection
- No manual integration needed in MatchmakingManager

✅ **Config-Based:**
- Connection IP:port set via static method before routine starts
- Allows flexibility for different server addresses

