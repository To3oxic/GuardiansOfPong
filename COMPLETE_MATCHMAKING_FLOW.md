# Complete Matchmaking & Game Flow Architecture

## End-to-End Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          COMPLETE MATCHMAKING FLOW                          │
└─────────────────────────────────────────────────────────────────────────────┘

PHASE 1: QUEUE
═════════════

Player 1 (Client)                Backend                    Player 2 (Client)
       │                            │                              │
       │  POST /join-queue/         │                              │
       ├───────────────────────────>│                              │
       │                            │                              │
       │  Response: queued          │                              │
       │<───────────────────────────┤                              │
       │                            │  POST /join-queue/           │
       │                            │<─────────────────────────────┤
       │                            │                              │
       │                            │  Response: queued            │
       │                            ├─────────────────────────────>│
       │                            │                              │
       │  [Matchmaker detects 2 queued players]                   │
       │                            │                              │

PHASE 2: MATCH CREATION & SERVER SPAWN
══════════════════════════════════════

Matchmaker Backend                Headless Server            Players (waiting)
       │                                 │                         │
       │  1. Create MatchInstance       │                         │
       │     status = "initializing"    │                         │
       │     server_port = 7777         │                         │
       │     server_pid = NULL          │                         │
       │                                │                         │
       │  2. Spawn headless server      │                         │
       │  with command-line args        │                         │
       ├───────────────────────────────>│                         │
       │                                │                         │
       │    -matchId=UUID               │                         │
       │    -player1Id=UUID             │                         │
       │    -player2Id=UUID             │                         │
       │    -port=7777                  │                         │
       │                                │                         │

PHASE 3: HEADLESS SERVER INITIALIZATION
════════════════════════════════════════

Headless Server                  Backend                     Players
       │                            │                         │
       │  Awake: Parse args         │                         │
       │  ✓ matchId extracted       │                         │
       │  ✓ player1Id extracted     │                         │
       │  ✓ player2Id extracted     │                         │
       │  ✓ port extracted          │                         │
       │                            │                         │
       │  Start:                    │                         │
       │  ✓ Configure transport     │                         │
       │    Bind to 0.0.0.0:7777    │                         │
       │                            │                         │
       │  ✓ StartServer()           │                         │
       │    Listen on port 7777     │                         │
       │                            │                         │
       │  ✓ Update MatchInstance    │                         │
       │    status = "initializing" │                         │
       │    server_ip = "127.0.0.1" │                         │
       │    server_pid = PID        │                         │
       │                            │                         │

PHASE 4: PLAYERS POLL & RECEIVE SERVER INFO
═════════════════════════════════════════════

Player 1                         Backend                    Player 2
       │                            │                         │
       │  GET /queue-status/        │                         │
       ├───────────────────────────>│                         │
       │                            │ GET /queue-status/      │
       │                            │<────────────────────────┤
       │                            │                         │
       │  Response:                 │  Response:              │
       │  ✓ status: "matched"       │  ✓ status: "matched"   │
       │  ✓ match_instance:         │  ✓ match_instance:     │
       │    - server_ip: "127.0.0.1"│    - server_ip: "..." │
       │    - server_port: 7777     │    - server_port: 7777 │
       │    - status: "initializing"│    - status: "..."      │
       │<───────────────────────────┤                         │
       │                            │ Response              │
       │                            ├────────────────────────>│
       │                            │                         │
       │  ✓ Extract: IP + Port      │  ✓ Extract: IP + Port  │
       │  ✓ Wait for "ready"        │  ✓ Wait for "ready"    │
       │                            │                         │

PHASE 5: CLIENTS CONNECT TO SERVER
════════════════════════════════════

Player 1 Client                  Headless Server            Player 2 Client
       │                              │                          │
       │  Poll queue-status           │                          │
       │  ✓ Sees status "ready"       │  Poll queue-status       │
       │  ✓ Extracts IP:port          │  ✓ Sees status "ready"   │
       │                              │  ✓ Extracts IP:port      │
       │  [HeadlessServer.cs]         │  [HeadlessServer.cs]     │
       │  ClientConnectionRoutine()   │  ClientConnectionRoutine()
       │  ✓ ConfigureTransport        │  ✓ ConfigureTransport    │
       │    127.0.0.1:7777            │    127.0.0.1:7777        │
       │  ✓ StartClient()             │  ✓ StartClient()         │
       │                              │                          │
       │  NetworkManager.StartClient()│                          │
       ├─────────────────────────────>│<────────────────────────┤
       │                              │                          │
       │                              │  OnClientConnectedToServer
       │                              │  ├─ Log client connection
       │                              │  └─ Check if 2 clients
       │                              │                          │
       │  OnClientConnectedToServer   │                          │
       │  ├─ SubmitPlayerIdAfterDelay │                          │
       │  └─ SendPlayerIdToServer()   │                          │
       │                              │                          │
       │  Message: "PlayerIdSubmit"   │                          │
       ├─────────────────────────────>│                          │
       │                              │ Message: "PlayerIdSubmit"│
       │                              │<────────────────────────┤
       │                              │                          │

PHASE 6: SERVER RECEIVES PLAYER IDS
═════════════════════════════════════

Headless Server                  Backend                     Players
       │                            │                         │
       │  Receive player ID from    │                         │
       │  Client 1: "uuid-player1"  │                         │
       │  ✓ Map: clientId_1 →       │                         │
       │         uuid-player1       │                         │
       │                            │                         │
       │  Receive player ID from    │                         │
       │  Client 2: "uuid-player2"  │                         │
       │  ✓ Map: clientId_2 →       │                         │
       │         uuid-player2       │                         │
       │                            │                         │
       │  ✓ Both clients mapped      │                         │
       │  Start deck fetching        │                         │
       │                            │                         │

PHASE 7: SERVER FETCHES DECKS
══════════════════════════════

Headless Server                  Backend                     Players
       │                            │                         │
       │  GET /api/players/         │                         │
       │  uuid-player1/active-deck/ │                         │
       ├───────────────────────────>│                         │
       │                            │                         │
       │                    Response: DeckPayload (deck1)      │
       │<───────────────────────────┤                         │
       │                            │                         │
       │  GET /api/players/         │                         │
       │  uuid-player2/active-deck/ │                         │
       ├───────────────────────────>│                         │
       │                            │                         │
       │                    Response: DeckPayload (deck2)      │
       │<───────────────────────────┤                         │
       │                            │                         │
       │  ✓ Store:                  │                         │
       │    _playerDecks[uuid-player1] = deck1                │
       │    _playerDecks[uuid-player2] = deck2                │
       │                            │                         │

PHASE 8: SERVER LOADS GAME SCENE
═════════════════════════════════

Headless Server (all clients)    GameManager               Backend
       │                            │                        │
       │  ✓ Build decksByClientId:  │                        │
       │    [1] → deck1             │                        │
       │    [2] → deck2             │                        │
       │                            │                        │
       │  NetworkManager.SceneManager.LoadScene()            │
       │  Load "GameScene" on all clients                     │
       │                            │                        │
       │  Wait for scene load       │                        │
       │  Find GameManager          │                        │
       │  gameManager.InitializeFromDecks(decksByClientId)   │
       │                            │                        │
       │                       Setup game                     │
       │                       ├─ Spawn board               │
       │                       ├─ Spawn units               │
       │                       └─ Start game loop           │
       │                            │                        │
       │  Signal backend:           │                        │
       │  POST /server-ready/       │                        │
       ├───────────────────────────>│                        │
       │                            │                        │
       │                    Update MatchInstance:           │
       │                    status = "running"              │
       │                            │                        │

PHASE 9: GAME RUNNING
══════════════════════

Player 1 Client            Headless Server            Player 2 Client
       │                         │                          │
       │  ◄──────────────────────►│◄────────────────────────┤
       │   Network synchronization via Netcode for GameObjects
       │
       │  Units attack
       │  Cards played
       │  Moves synced
       │
       │  [Game continues...]
       │

PHASE 10: GAME ENDS
════════════════════

Headless Server            Backend                     Players
       │                     │                         │
       │  Match complete     │                         │
       │  POST /match-result/│                         │
       ├────────────────────>│                         │
       │                     │                         │
       │         Response: OK│                         │
       │<────────────────────┤                         │
       │                     │                         │
       │                Update MatchInstance:         │
       │                status = "completed"          │
       │                winner = "..."                │
       │                                               │
       │  Server shuts down  │                         │
       │                     │                         │
```

---

## Technical Summary

### State Transitions

```
Queue Flow:
┌─────────┐
│ queued  │ ← Player joins queue
└────┬────┘
     │ (2+ players found)
     ↓
┌─────────────┐
│  matched    │ ← MatchInstance created, server spawning
└────┬────────┘
     │ (server initializing, clients waiting)
     ↓
┌─────────────┐
│initializing │ ← Server starting, listening on port
└────┬────────┘
     │ (clients connecting, submitting IDs)
     ↓
┌─────────────┐
│   ready     │ ← Server ready signal sent, clients can connect
└────┬────────┘
     │ (clients connecting, scene loading)
     ↓
┌─────────────┐
│  running    │ ← Game started, match in progress
└────┬────────┘
     │ (game ends)
     ↓
┌──────────────┐
│  completed   │ ← Match finished, results recorded
└──────────────┘
```

### Component Responsibilities

**MatchmakingManager (Client)**
- ✅ Join queue via `/join-queue/`
- ✅ Poll `/queue-status/` for match status
- ✅ Extract server IP:port when status="ready"
- ✅ Call HeadlessServer to connect as client

**Backend (Django)**
- ✅ Queue management (join/leave/status endpoints)
- ✅ Match creation when 2 players found
- ✅ Spawn headless server process with command-line args
- ✅ Update match status throughout lifecycle
- ✅ Provide deck data via `/api/players/{id}/active-deck/`
- ✅ Receive server-ready signal and update status

**HeadlessServer (Server)**
- ✅ Parse command-line arguments (player IDs, match ID, port)
- ✅ Initialize NetworkManager and transport
- ✅ Start as server, listen on configured port
- ✅ Wait for 2 clients to connect
- ✅ Receive player IDs from connecting clients
- ✅ Fetch decks from backend using player IDs
- ✅ Load GameScene via NetworkManager.SceneManager
- ✅ Call GameManager.InitializeFromDecks() with deck mapping
- ✅ Signal backend when server ready

**HeadlessServer (Client)**
- ✅ Parse command-line arguments
- ✅ Connect to server at IP:port
- ✅ Auto-submit player ID to server
- ✅ Wait for GameScene load
- ✅ Play game with synced network state

**GameManager**
- ✅ Receive `InitializeFromDecks(Dictionary<ulong, DeckPayload>)`
- ✅ Setup game board and units
- ✅ Start game loop
- ✅ Sync game state across network

---

## Command-Line Arguments Format

### Server Spawn
```bash
Myproject.exe \
  -batchmode \
  -nographics \
  -server \
  -matchId=550e8400-e29b-41d4-a716-446655440000 \
  -player1Id=650e8400-e29b-41d4-a716-446655440001 \
  -player2Id=750e8400-e29b-41d4-a716-446655440002 \
  -port=7777
```

### Parsed Values
```csharp
_matchId = "550e8400-e29b-41d4-a716-446655440000"
_player1Id = "650e8400-e29b-41d4-a716-446655440001"
_player2Id = "750e8400-e29b-41d4-a716-446655440002"
serverPort = 7777
```

---

## API Endpoints Involved

### Queue Management
- **POST** `/api/matchmaking/join-queue/` - Join queue
- **GET** `/api/matchmaking/queue-status/` - Check queue/match status
- **POST** `/api/matchmaking/leave-queue/` - Leave queue

### Deck Fetching
- **GET** `/api/players/{playerId}/active-deck/` - Get player's deck

### Server Lifecycle
- **POST** `/api/matchmaking/server-ready/` - Server signals it's ready

---

## Data Flow Summary

```
Backend Database
    ↓
Player 1 & 2 join queue
    ↓
Matchmaker finds match
    ↓
Create MatchInstance + spawn headless server
    ↓
Server receives player IDs from both clients
    ↓
Server fetches decks from database using player IDs
    ↓
Server loads GameScene and initializes GameManager with decks
    ↓
Game starts with synced network state
    ↓
Game ends, results recorded
```

---

## Verification Checklist

✅ **Queue Phase**
- [ ] Player 1 joins queue
- [ ] Player 2 joins queue
- [ ] Backend matchmaker detects 2 queued players

✅ **Spawn Phase**
- [ ] Backend creates MatchInstance (status="initializing")
- [ ] Backend spawns headless server with correct args
- [ ] Server Awake() parses all command-line arguments correctly

✅ **Server Init Phase**
- [ ] Server Start() detects headless mode
- [ ] Server configures transport (0.0.0.0:7777)
- [ ] Server calls StartServer() successfully
- [ ] Server waits for 2 clients

✅ **Client Connection Phase**
- [ ] Client 1 polls and sees status="ready"
- [ ] Client 1 extracts IP:port
- [ ] Client 1 calls HeadlessServer.ClientConnectionRoutine()
- [ ] Client 1 connects to server
- [ ] Client 2 does the same
- [ ] Server sees 2 connected clients

✅ **ID & Deck Phase**
- [ ] Client 1 submits player ID to server
- [ ] Client 2 submits player ID to server
- [ ] Server receives both IDs and maps them
- [ ] Server fetches deck for player 1
- [ ] Server fetches deck for player 2
- [ ] Server stores both decks

✅ **Game Start Phase**
- [ ] Server loads GameScene via NetworkManager.SceneManager
- [ ] Both clients load GameScene
- [ ] Server finds GameManager
- [ ] Server calls InitializeFromDecks() with correct mapping
- [ ] GameManager initializes board and units
- [ ] Game starts

---

## Error Scenarios

| Scenario | Detection | Recovery |
|----------|-----------|----------|
| Client never submits ID | Server timeout on deck fetch | Retry or timeout |
| Deck fetch fails | HTTP error on deck API | Retry or fallback |
| GameManager not found | FindFirstObjectByType returns null | Timeout and error log |
| Client disconnects | OnClientDisconnected callback | End match or wait for reconnect |
| Server crashes | Backend doesn't receive ready signal | Mark match as failed |

