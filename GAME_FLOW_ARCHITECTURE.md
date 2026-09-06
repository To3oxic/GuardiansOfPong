# Game Flow Architecture - GuardiansOfPong

## Complete Player Journey & Backend Integration

### Phase 1: Application Launch → Main Menu

**Sequence:**
1. **Game Starts**
   - App loads MainMenu scene
   - MainMenuManager initializes

2. **Load Player Data** (Database Query)
   - Fetch from Backend:
     - Player account info (username, level, rank, coins, etc.)
     - Player's saved decks (card collections)
     - Player's unlocked units/guardians
     - Player statistics (wins, losses, etc.)
   - Display loading screen with progress
   - Cache data locally (PlayerPrefs or serialized file)

3. **Main Menu Display**
   - Show player info (level, rank, coins) at top
   - Display game board zone with units patrolling
   - Display card selection zone with player's available cards
   - Show "Start Game" button

**UI/UX:**
- Main Menu has two horizontal zones (board + card selection room)
- Player can preview their deck before queuing
- Legend standing in background of card selection zone

---

### Phase 2: Player Presses "Start Game" → Matchmaking

**Sequence:**
1. **Connect to Backend Server**
   - Establish connection to backend matchmaking service
   - Send player ID + selected deck info

2. **Enter Matchmaking Queue**
   - Backend adds player to queue
   - Show "Searching for opponent..." UI
   - Display estimated wait time
   - Allow cancel option

3. **Waiting State**
   - Show animations/loading indicators
   - Backend continuously monitors queue

**Data Sent to Backend:**
```json
{
  "playerId": "player_uuid",
  "selectedDeck": {
    "deckId": "deck_uuid",
    "cards": [
      { "cardId": "card_1", "quantity": 3 },
      { "cardId": "card_2", "quantity": 2 },
      ...
    ]
  }
}
```

---

### Phase 3: Match Found → Server Setup

**Sequence (Backend-Side):**
1. **Match Confirmed**
   - Backend finds opponent in queue
   - Retrieves both players' decks from database

2. **Spawn Dedicated Headless Server Instance**
   - Backend launches headless server process:
     ```
     ./Myproject.exe -nographics -batchmode -server -logFile match_<matchId>.log
     ```
   - Server starts and binds on `0.0.0.0:7777` (configurable port)
   - Server is in waiting state, ready for clients

3. **Pass Deck Data to Headless Server**
   - Backend sends both player decks to the new server instance
   - Server caches decks in memory (GameManager)
   - Server awaits client connections

4. **Send Connection Info to Both Players**
   - Backend responds to both players with:
     ```json
     {
       "matchId": "match_uuid",
       "serverIp": "192.168.1.100", // Backend's public/local IP
       "serverPort": 7777,
       "playerId": "player_1",
       "opponentId": "player_2",
       "playerDecks": {
         "player_1": [...],
         "player_2": [...]
       }
     }
     ```

**Communication Flow:**
```
Main Menu (Client) 
    ↓ [Sends player ID + deck]
Backend Matchmaking Server
    ↓ [Finds opponent, launches headless, passes decks]
Headless Server Instance (Match-specific)
    ↑↓ [Receives connection from both clients]
Client 1 (Game Scene) ← → Client 2 (Game Scene)
```

---

### Phase 4: Players Connect → Game Starts

**Sequence (Client-Side):**
1. **Receive Server Connection Info**
   - Display "Match found! Connecting..."
   - Show server IP and port

2. **Connect to Headless Server**
   - Both players' clients connect to headless server at given IP:Port
   - Use NetworkManager to establish Netcode connection

3. **Load Game Scene**
   - Both clients load GameScene
   - Handshake with headless server confirms connection

4. **Receive Deck Data**
   - Headless server sends both decks to clients via NamedMessage
   - GameManager on each client initializes with deck data
   - UI updates to show player deck + opponent deck

5. **Game Starts**
   - Both players in game scene
   - Ball spawns, gameplay begins
   - All actions synced via NetworkTransform and RPC calls

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        PLAYER LIFECYCLE                          │
└─────────────────────────────────────────────────────────────────┘

1. APP LAUNCH
   └─→ Load MainMenu Scene
       └─→ Query Database (Player Info + Decks)
           └─→ Display Main Menu with User Data

2. PLAYER CLICKS "START GAME"
   └─→ Connect to Backend Matchmaking
       └─→ Send: { playerId, selectedDeck }
           └─→ Enter Queue (Show: "Searching...")

3. OPPONENT FOUND
   └─→ Backend Retrieves Both Decks from DB
       └─→ Spawn Headless Server Instance
           └─→ Send Decks to Headless Server
               └─→ Send { serverIp, serverPort, decks } to Both Clients

4. CLIENTS RECEIVE SERVER INFO
   └─→ Client 1 Connects to Headless Server
       └─→ Client 2 Connects to Headless Server
           └─→ Both Confirm Connected to GameManager
               └─→ Headless Server Sends Decks via NamedMessage
                   └─→ Load Game Scene
                       └─→ START MATCH

5. MATCH ENDS
   └─→ Send Results to Backend
       └─→ Update Player Statistics
           └─→ Award Coins/XP
               └─→ Return to Main Menu
```

---

## Key Architecture Components

### Backend Services Required

| Service | Responsibility |
|---------|---|
| **Matchmaking Service** | Queue management, opponent finding |
| **Database Layer** | Store player info, decks, stats |
| **Server Manager** | Spawn/destroy headless instances, assign ports |
| **Session Manager** | Track active matches, manage timeouts |

### Client-Side Flow

| Component | Role |
|-----------|------|
| **MainMenuManager** | Display menu, load player data |
| **MatchmakingController** | Connect to backend, manage queue UI |
| **NetworkUI** | Handle server connection, show IP/port |
| **GameManager** | Receive decks, initialize match |

### Headless Server Role

| Task | Details |
|------|---------|
| **Wait for Clients** | Listen on port 7777 |
| **Receive Connections** | Two players must connect |
| **Send Deck Data** | NamedMessage with both decks |
| **Sync Gameplay** | NetworkTransform updates, RPC calls |
| **Track Match End** | Determine winner, send results to backend |
| **Cleanup** | Exit process when match ends |

---

## Critical Implementation Details

### 1. Headless Server Instance Management
- **Port Assignment**: Allocate unique ports for each match (7777, 7778, 7779, etc.)
- **Process Tracking**: Backend must track PID of each headless instance
- **Timeout**: Kill process if no clients connect within 30 seconds
- **Cleanup**: Terminate process immediately after match end

### 2. Deck Data Transmission
- **To Headless**: Backend sends decks before clients connect
- **To Clients**: Headless sends decks via `NamedMessage` on first connection
- **Format**: Serialize CardData arrays as JSON or binary

### 3. Player Identification
- **Session Token**: Backend issues token when player queues
- **Validation**: Headless server validates token when client connects
- **Prevents**: Unauthorized players, replay attacks, queue spoofing

### 4. Network Resilience
- **Reconnection**: Allow clients to reconnect within 10 seconds of disconnect
- **Timeout**: If client doesn't reconnect, opponent wins
- **Server Crash**: Backend detects crash, notifies both players

---

## Database Schema (Conceptual)

### Players Table
```
- playerId (PK)
- username
- email
- level
- rank
- coins
- totalWins
- totalLosses
- createdAt
```

### Decks Table
```
- deckId (PK)
- playerId (FK)
- deckName
- cards (JSON array of card IDs)
- isActive
- createdAt
```

### Match History Table
```
- matchId (PK)
- player1Id (FK)
- player2Id (FK)
- winnerId (FK)
- player1Deck (JSON)
- player2Deck (JSON)
- duration
- timestamp
```

---

## API Endpoints (Proposed)

### 1. GET `/api/player/info`
**Response:** Player data (level, coins, decks, stats)

### 2. POST `/api/matchmaking/queue`
**Request:** { playerId, selectedDeckId }
**Response:** { queueId, estimatedWait }

### 3. POST `/api/matchmaking/cancel`
**Request:** { queueId }
**Response:** { success: true }

### 4. GET `/api/matchmaking/status`
**Response:** { status: "searching" | "matched", serverIp?, serverPort?, decks? }

### 5. POST `/api/match/complete`
**Request:** { matchId, playerId, winnerId, result }
**Response:** { coinsAwarded, xpGained }

---

## Future Enhancements

- [ ] Ranked ladder system with ELO rating
- [ ] Seasonal passes and battle pass progression
- [ ] Tournament mode (multiple rounds)
- [ ] Spectator mode (watch live matches)
- [ ] Replay system (store and replay matches)
- [ ] Regional servers (EU, NA, ASIA)
- [ ] Cross-platform play (iOS, Android, PC)
- [ ] Clan/Guild system

---

## Notes & Decisions

1. **Headless Server per Match**: Creates dedicated instance per match (scalable but resource-intensive)
   - Alternative: Shared server with multiple match instances (cheaper but complex)

2. **Direct Deck Transmission**: Decks sent by backend to avoid client manipulation
   - Players cannot modify their deck after queueing

3. **Automatic Cleanup**: Headless process exits after match completes
   - Ensures no orphaned processes consuming resources

4. **Local IP for Testing**: Use player PC's local IP (192.168.1.x)
   - For production: Use backend server's public IP or regional server farm

5. **Port Allocation Strategy**: Start at 7777, increment per match
   - Could also use random available port + notify backend
