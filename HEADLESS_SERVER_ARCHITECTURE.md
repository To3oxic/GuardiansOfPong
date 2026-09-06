# HeadlessServer Architecture

## Overview

`HeadlessServer.cs` is a unified component that handles both **headless server** and **client** connection logic for matchmaking. It detects whether it's running in headless mode and routes to the appropriate flow.

---

## Server Flow (Headless Mode)

### Activation
- Server detects headless mode via `Application.isBatchMode` or `-server` command-line flag
- Spawned by matchmaker with command-line arguments:
  ```bash
  Myproject.exe -batchmode -nographics -server \
    -matchId=UUID \
    -player1Id=UUID \
    -player2Id=UUID \
    -port=7777
  ```

### Flow

**1. Parse Command-Line Arguments (Awake)**
- Extract: `-matchId`, `-player1Id`, `-player2Id`, `-port`
- Store in instance variables

**2. Configure Transport (Start → Step 1)**
- Bind to `0.0.0.0:{port}` (listen on all interfaces)
- Set buffer sizes (MaxPacketQueueSize: 512, MaxPayloadSize: 6000)

**3. Start Server (Step 2)**
- Call `NetworkManager.StartServer()`
- Register message handlers:
  - `PlayerIdSubmit`: Receives client player IDs
- Register connection callbacks:
  - `OnClientConnectedToServer`
  - `OnClientDisconnectedFromServer`

**4. Wait for Both Clients (Step 3)**
- Poll `ConnectedClientsIds.Count >= 2` (server + 2 clients)
- Timeout: 30 seconds
- Each client connecting triggers `OnClientConnectedToServer`

**5. Fetch Decks from Backend (Step 4)**
- GET `/api/players/{player1Id}/active-deck/`
- GET `/api/players/{player2Id}/active-deck/`
- Parse as `DeckPayload`
- Store in `_playerDecks` dictionary

**6. Start Game with Decks (Step 5)**
- Wait for `GameManager` to exist (5s timeout)
- Build `Dictionary<ulong, DeckPayload>` mapping client IDs to decks
- Call `gameManager.InitializeFromDecks(decksByClientId)`
- Signal backend: POST `/api/matchmaking/server-ready/`

### Code Structure

```csharp
ServerInitializationRoutine()
├─ ConfigureTransport()
├─ StartServerAndRegisterHandlers()
│  ├─ Register callbacks
│  ├─ Register message handlers
│  └─ StartServer()
├─ WaitForBothClientsToConnect()
├─ FetchPlayerDecks()
│  ├─ FetchDeckFromBackend(player1Id)
│  └─ FetchDeckFromBackend(player2Id)
└─ StartGameWithDecks()
   ├─ Find GameManager
   └─ Initialize with decks
```

---

## Client Flow (Non-Headless Mode)

### Activation
- Client detects non-headless mode (normal game client)
- Should have deck data ready from `InitiateMatchmakingAndLoadScene(payload)`

### Flow

**1. Configure Transport (Start → Step 1)**
- Connect to `127.0.0.1:{port}` (localhost, same port as server)
- Set buffer sizes

**2. Register Callbacks (Step 2)**
- `OnClientConnectedToServer`: Handle successful connection
- Message handler: `OnClientReceiveDeckAssignment`

**3. Start Client (Step 3)**
- Call `NetworkManager.StartClient()`

**4. Wait for Connection (Step 4)**
- Poll `IsConnectedClient` (15s timeout)
- Once connected, `OnClientConnectedToServer` fires

**5. Submit Player ID (Step 5)**
- After connection stabilizes (0.5s delay)
- Get player ID from `BackendManager.GetCachedPlayerData().id`
- Send via `SendPlayerIdToServer(playerId)`

### Server Receives Player ID

When client calls `SendPlayerIdToServer(playerId)`:

1. Message handler `OnClientSubmitPlayerId` receives it
2. Server maps: `_clientIdToPlayerId[clientId] = playerId`
3. Once both clients submit IDs, server has complete mapping
4. Server fetches decks using these IDs
5. Server assigns decks to correct clients by client ID

### Code Structure

```csharp
ClientConnectionRoutine()
├─ ConfigureTransportForClient()
├─ Register callbacks
├─ StartClient()
├─ WaitForClientConnection()
└─ (Client waits for scene load from server)
```

---

## Client-to-Server Communication

### Player ID Submission

**Client → Server:**
```csharp
HeadlessServer.SendPlayerIdToServer(playerId: string)
```

**Message Format:**
- Message name: `"PlayerIdSubmit"`
- Content: UTF8-encoded player ID string with length prefix
- Recipient: Server

**Server Handler:**
```csharp
OnClientSubmitPlayerId(senderClientId, reader)
├─ Read length
├─ Read UTF8 string
├─ Map: _clientIdToPlayerId[senderClientId] = playerId
└─ Check if ready to start game
```

---

## Deck Assignment Flow

### Mapping

```
Backend Database:
- player1Id (UUID) → deck1
- player2Id (UUID) → deck2

Server _clientIdToPlayerId:
- clientId_1 (e.g., 1) → player1Id
- clientId_2 (e.g., 2) → player2Id

Server _playerDecks:
- player1Id → DeckPayload
- player2Id → DeckPayload

Final decksByClientId (passed to GameManager):
- clientId_1 → DeckPayload (deck1)
- clientId_2 → DeckPayload (deck2)
```

### Example

```
1. Client 1 connects (clientId = 1)
2. Client 1 submits playerId = "uuid-player1"
3. _clientIdToPlayerId[1] = "uuid-player1"
4. 
5. Client 2 connects (clientId = 2)
6. Client 2 submits playerId = "uuid-player2"
7. _clientIdToPlayerId[2] = "uuid-player2"
8.
9. Server fetches:
   - GET /api/players/uuid-player1/active-deck/ → deck1
   - GET /api/players/uuid-player2/active-deck/ → deck2
10.
11. Store in _playerDecks:
    - _playerDecks["uuid-player1"] = deck1
    - _playerDecks["uuid-player2"] = deck2
12.
13. Build decksByClientId:
    - decksByClientId[1] = _playerDecks["uuid-player1"] = deck1
    - decksByClientId[2] = _playerDecks["uuid-player2"] = deck2
14.
15. Call gameManager.InitializeFromDecks(decksByClientId)
```

---

## Integration with NetworkUI

### NetworkUI Role (Simplified)

With `HeadlessServer` handling the hard work:

1. **Headless mode**: NetworkUI can be disabled or minimal
2. **Client mode**: NetworkUI still handles matchmaking queue flow
3. **Scene loading**: Use `NetworkManager.SceneManager.LoadScene()` for sync

### Next Steps

1. Update `MatchmakingManager.cs` to:
   - Call `HeadlessServer.SendPlayerIdToServer()` after client connects
   - Use `NetworkManager.SceneManager.LoadScene()` instead of `SceneManager.LoadScene()`

2. Ensure `BackendManager` is initialized before clients connect

3. Test complete flow:
   - Backend spawns headless server
   - Two clients join queue and receive server IP:port
   - Clients connect to server
   - Clients submit player IDs
   - Server fetches decks
   - Game starts

---

## Debugging

### Server Logs
- `[HeadlessServer] === Server Initialization START ===`
- `[HeadlessServer] Client {clientId} connected`
- `[HeadlessServer] Stored deck for client {clientId}`
- `[HeadlessServer] === Server Initialization COMPLETE ===`

### Client Logs
- `[HeadlessServer] === Client Connection START ===`
- `[HeadlessServer] ✓ Successfully connected to server!`
- `[HeadlessServer] Submitted player ID to server: {playerId}`
- `[HeadlessServer] === Client Connection COMPLETE ===`

### Common Issues

**Issue**: Client never submits player ID
- **Cause**: `BackendManager` not initialized or `GetCachedPlayerData()` returns null
- **Fix**: Ensure `BackendManager` is set up before client connects

**Issue**: Server still waiting for clients
- **Cause**: Clients can't connect to server (firewall, wrong IP/port)
- **Fix**: Check server logs, verify port is open, check client connection config

**Issue**: Deck fetch fails
- **Cause**: Player IDs not submitted, backend unreachable
- **Fix**: Check player ID submission logs, verify backend is running

---

## Future Enhancements

1. **Client ID assignment**: Currently clients auto-identify. Could be explicit from matchmaker.
2. **Error recovery**: Automatic retry if deck fetch fails
3. **Client timeout**: Currently 15s. Could be configurable
4. **Multiple ports**: Support multiple matches on different ports simultaneously
5. **Health checks**: Periodic ping from clients to ensure connection

