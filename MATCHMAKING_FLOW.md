# GuardiansOfPong Matchmaking Flow

## Overview
Complete end-to-end matchmaking system that connects two players, spawns a headless Unity server, and sends all necessary connection and deck information to both clients.

---

## 1. Player Login

**Endpoint:** `POST /api/players/login/`

**Request:**
```json
{
  "username": "testplayer",
  "password": "testpass123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "user": {
    "id": "player-uuid",
    "username": "testplayer",
    "email": "testplayer@example.com",
    "level": 10,
    "rank": 0,
    "coins": 5000,
    "total_wins": 0,
    "total_losses": 0,
    "skill_rating": 1000,
    "decks": [
      {
        "id": "deck-uuid",
        "deck_name": "My First Deck",
        "is_active": true,
        "card_slot_1": { ... },
        "card_slot_2": { ... },
        ...
      }
    ],
    "all_cards": [ ... ],
    "created_at": "...",
    "updated_at": "..."
  },
  "session_id": "session-key",
  "csrf_token": "csrf-token-value"
}
```

**Key Information Extracted:**
- `csrf_token`: Required for all subsequent POST requests
- `session_id`: Maintained in cookies for session authentication
- Active deck information

---

## 2. Player Joins Matchmaking Queue

**Endpoint:** `POST /api/matchmaking/join-queue/`

**Headers Required:**
```
X-CSRFToken: {csrf_token_from_login}
Content-Type: application/json
Cookie: sessionid={session_id}
```

**Request Body:**
```json
{}
```

**What Happens:**
1. Backend checks if player has an active deck (`is_active=True`)
2. Creates a `MatchQueue` entry with status `queued`
3. Assigns queue position based on join time (FIFO)

**Response:**
```json
{
  "success": true,
  "message": "Joined queue at position 1 with deck: My First Deck",
  "queue_entry": {
    "id": "queue-entry-uuid",
    "player_username": "testplayer",
    "deck_name": "My First Deck",
    "status": "queued",
    "joined_at": "2025-11-30T14:02:46Z",
    "matched_at": null,
    "queue_position": 1
  },
  "queue_position": 1
}
```

---

## 3. Matchmaker Polling Service

**Command:** `python manage.py matchmaker`

**Process:**
```
┌─────────────────────────────────────────────────────┐
│  Matchmaker Service (runs continuously)             │
│  - Polls queue every 2 seconds                      │
│  - Checks for pairs of queued players              │
│  - Spawns headless servers                         │
│  - Tracks process health                           │
└─────────────────────────────────────────────────────┘
```

### When Match Found:

1. **Find Pair**: Get first 2 queued players (FIFO order)
   ```python
   player1_queue = first queued player
   player2_queue = second queued player
   ```

2. **Allocate Port**: Find available port (range 7777-7800)
   ```
   Port 7777 → Free ✓
   ```

3. **Spawn Headless Server**
   ```bash
   C:\Users\ahmad\My project\testbuild\Myproject.exe \
     -batchmode \
     -nographics \
     -server \
     -port=7777 \
     -player1Id=player1-uuid \
     -player2Id=player2-uuid
   ```

   **Process Created:**
   - Process ID (PID): 34388
   - Logs: `server_logs/server_7777_20251130_140246.log`
   - Server starts listening on port 7777

4. **Create MatchInstance**
   ```python
   MatchInstance.create(
     player1=testplayer,
     player2=testplayer2,
     player1_deck=deck1,
     player2_deck=deck2,
     server_ip='127.0.0.1',
     server_port=7777,
     server_pid=34388,
     status='initializing'
   )
   ```

5. **Update Queue Status**
   ```python
   player1_queue.status = 'matched'
   player1_queue.matched_at = now()
   player1_queue.save()
   
   player2_queue.status = 'matched'
   player2_queue.matched_at = now()
   player2_queue.save()
   ```

6. **Log Output**
   ```
   ✓ Created match between testplayer and testplayer2 on port 7777 (PID: 34388)
   ```

---

## 4. Client Checks Match Status

**Endpoint:** `GET /api/matchmaking/queue-status/`

**Headers Required:**
```
Content-Type: application/json
Cookie: sessionid={session_id}
```

**Response (When Matched):**
```json
{
  "status": "matched",
  "match_instance": {
    "id": "match-instance-uuid",
    "player1_username": "testplayer",
    "player2_username": "testplayer2",
    "player1_deck": {
      "id": "deck-uuid",
      "deck_name": "My First Deck",
      "is_active": true,
      "card_slot_1": {
        "card": {
          "id": "card-uuid",
          "name": "ToxinBall",
          "description": "...",
          "card_type": "spell",
          "rarity": "common",
          "max_level": 5,
          "registry_name": "ToxinBall"
        },
        "level": 1,
        "copies": 3,
        "acquired_at": "..."
      },
      "card_slot_2": { ... },
      "card_slot_3": { ... },
      "card_slot_4": { ... },
      "card_slot_5": { ... },
      "card_slot_6": { ... },
      "card_slot_7": { ... },
      "card_slot_8": { ... },
      "legend_stone": {
        "card": {
          "id": "card-uuid",
          "name": "StoneSentinel",
          "registry_name": "StoneSentinel",
          ...
        },
        "level": 1,
        "copies": 1,
        "acquired_at": "..."
      },
      "guardian_card": {
        "card": {
          "id": "card-uuid",
          "name": "ForestSpirit",
          "registry_name": "ForestSpirit",
          ...
        },
        "level": 1,
        "copies": 1,
        "acquired_at": "..."
      },
      "created_at": "2025-11-30T10:00:00Z",
      "updated_at": "2025-11-30T10:00:00Z"
    },
    "player2_deck": { ... same structure as player1_deck ... },
    "server_ip": "127.0.0.1",
    "server_port": 7777,
    "server_pid": 34388,
    "status": "initializing",
    "winner_username": null,
    "created_at": "2025-11-30T14:02:48Z",
    "started_at": null,
    "ended_at": null
  }
}
```

**Client Information Received:**
- ✅ Server IP address: `127.0.0.1`
- ✅ Server port: `7777`
- ✅ Server process ID: `34388`
- ✅ Both player usernames
- ✅ Complete deck information for both players (all 10 cards)
- ✅ Match status: `initializing`

---

## 5. Client Connects to Headless Server

**What Happens:**
1. Client uses server connection details from queue-status response
2. Client connects to `127.0.0.1:7777`
3. Client sends player ID via connection handshake
4. Game match begins

---

## 6. Headless Server Fetches Player Decks (Optional)

**If headless server needs fresh deck data:**

**Endpoint:** `GET /api/players/{player_id}/active-deck/`

**Example:**
```
GET /api/players/550e8400-e29b-41d4-a716-446655440000/active-deck/
```

**Response:**
```json
{
  "id": "deck-uuid",
  "deck_name": "My First Deck",
  "cards": [
    {
      "slot": 1,
      "card": {
        "id": "card-uuid",
        "name": "ToxinBall",
        "registry_name": "ToxinBall",
        "description": "...",
        "card_type": "spell",
        "rarity": "common",
        "max_level": 5
      },
      "level": 1,
      "copies": 3
    },
    ...
  ],
  "legend_stone": {
    "card": {
      "name": "StoneSentinel",
      "registry_name": "StoneSentinel",
      ...
    },
    "level": 1,
    "copies": 1
  },
  "guardian_card": {
    "card": {
      "name": "ForestSpirit",
      "registry_name": "ForestSpirit",
      ...
    },
    "level": 1,
    "copies": 1
  }
}
```

---

## 7. Match Status Updates

### Possible States:

| Status | Meaning | Where Set |
|--------|---------|-----------|
| `queued` | Waiting for opponent in queue | join-queue endpoint |
| `matched` | Opponent found, server spawning | matchmaker service |
| `initializing` | Server started, clients connecting | matchmaker service |
| `running` | Match in progress | Headless server |
| `completed` | Match finished | Headless server |
| `failed` | Server crashed or error | matchmaker cleanup |
| `cancelled` | Player left queue | leave-queue endpoint |

### Check Status:
```
GET /api/matchmaking/queue-status/
```

---

## 8. Leave Queue (Before Match)

**Endpoint:** `POST /api/matchmaking/leave-queue/`

**Headers Required:**
```
X-CSRFToken: {csrf_token}
Content-Type: application/json
Cookie: sessionid={session_id}
```

**Request Body:**
```json
{}
```

**Response:**
```json
{
  "success": true,
  "message": "Left queue successfully"
}
```

**Effect:**
- Cancels queue entry
- Status changed to `cancelled`
- Player removed from matchmaking

---

## 9. Headless Server Process Cleanup

**Automatic Process Management:**
- Matchmaker monitors all spawned processes every 2 seconds
- If process dies unexpectedly:
  - Marks match as `failed`
  - Re-queues both players automatically
  - Logs: `⚠ Server died on port 7777 (PID: 34388, exit code: 1)`

**Manual Cleanup (Shutdown):**
- When matchmaker service stops (Ctrl+C):
  - Terminates all spawned servers gracefully
  - Updates all matches to `failed`
  - Closes all connections

---

## Complete Request Flow Diagram

```
┌──────────────┐
│ Player Login │
└──────┬───────┘
       │ Returns: session_id, csrf_token
       ▼
┌────────────────────────┐
│ Join Queue             │
│ (with CSRF token)      │
└──────┬─────────────────┘
       │ Status: queued, position: 1
       │
       │ [Waiting for opponent...]
       │
       ▼
┌──────────────────────────────────────────┐
│ Matchmaker Service (polling every 2s)    │
│ Detects: 2 queued players                │
└──────┬───────────────────────────────────┘
       │
       ├─► Allocate port: 7777
       │
       ├─► Spawn headless server with player IDs
       │   - player1Id=uuid1
       │   - player2Id=uuid2
       │   - port=7777
       │
       └─► Create MatchInstance
           Update queue entries: status=matched
           Log: "Created match... port 7777 (PID: 34388)"
       
       ▼
┌─────────────────────────────────────┐
│ Client Polls: queue-status          │
│ (every 1-2 seconds)                 │
└──────┬──────────────────────────────┘
       │ Response:
       │ status: matched
       │ server_ip: 127.0.0.1
       │ server_port: 7777
       │ player1_deck: {...all cards...}
       │ player2_deck: {...all cards...}
       │
       ▼
┌──────────────────────────────────┐
│ Client Connects to Server        │
│ 127.0.0.1:7777                   │
└──────┬───────────────────────────┘
       │
       ▼
┌──────────────────────────────────┐
│ Game Match Begins                │
│ Players compete in real-time     │
└──────┬───────────────────────────┘
       │
       ▼
┌──────────────────────────────────┐
│ Match Completes                  │
│ Status: completed                │
│ Winner determined                │
└──────────────────────────────────┘
```

---

## Configuration

### Matchmaker Settings (matchmaker.py)

```python
BASE_PORT = 7777              # Starting port for servers
MAX_SERVERS = 24              # Maximum concurrent matches
MAX_PORT = 7801               # Ending port (7777 + 24)
POLLING_INTERVAL = 2          # Check for matches every 2 seconds
HEADLESS_SERVER_PATH = r"C:\Users\ahmad\My project\testbuild\Myproject.exe"
```

### CSRF Configuration (settings.py)

```python
CSRF_TRUSTED_ORIGINS = ['http://localhost:3000', 'http://127.0.0.1:8000']
CSRF_USE_SESSIONS = False        # Use cookie-based CSRF tokens
CSRF_COOKIE_HTTPONLY = False     # Allow JavaScript to read CSRF token
```

---

## Testing

### Test Scripts

**Two-Player Full Flow:**
```powershell
.\test_matchmaking_two_players.ps1
```

Performs:
1. Login for player 1 (testplayer)
2. Login for player 2 (testplayer2)
3. Player 1 joins queue
4. Player 1 checks status (queued)
5. Player 2 joins queue (triggers match)
6. Both players check status (matched with server info)

**Output:**
```
✓ Created match between testplayer and testplayer2 on port 7777 (PID: 34388)
[MATCH FOUND!]
Match ID: match-uuid
Player 1: testplayer
Player 2: testplayer2
Server IP: 127.0.0.1
Server Port: 7777
Server PID: 34388
Match Status: initializing
```

### Clear Queue

```powershell
python manage.py clear_queue
```

Removes all entries from queue (useful for testing).

---

## Data Models

### MatchQueue
- `id` (UUID): Unique queue entry identifier
- `player` (FK CustomUser): Queued player
- `deck` (FK SavedDeck): Deck being used
- `status` (Choice): queued | matched | cancelled
- `joined_at` (DateTime): When player joined queue
- `matched_at` (DateTime): When match was created
- `created_at` (DateTime): Entry creation timestamp

### MatchInstance
- `id` (UUID): Unique match identifier
- `player1` (FK CustomUser): First player
- `player2` (FK CustomUser): Second player
- `player1_deck` (FK SavedDeck): Player 1's deck
- `player2_deck` (FK SavedDeck): Player 2's deck
- `server_ip` (String): Server IP address
- `server_port` (Integer): Server port
- `server_pid` (Integer): Server process ID
- `status` (Choice): initializing | running | completed | failed
- `winner` (FK CustomUser, nullable): Match winner
- `created_at` (DateTime): Match creation time
- `started_at` (DateTime, nullable): When game started
- `ended_at` (DateTime, nullable): When game ended

---

## API Endpoints Summary

| Endpoint | Method | Purpose | Authentication |
|----------|--------|---------|-----------------|
| `/api/players/login/` | POST | Authenticate player | Public |
| `/api/matchmaking/join-queue/` | POST | Join matchmaking queue | Session + CSRF |
| `/api/matchmaking/queue-status/` | GET | Check current queue/match status | Session |
| `/api/matchmaking/leave-queue/` | POST | Leave queue or cancel match | Session + CSRF |
| `/api/players/{id}/active-deck/` | GET | Fetch player's active deck | Public (localhost) |

---

## Error Handling

### Common Errors

**403 Forbidden - CSRF Token Missing:**
```
Missing X-CSRFToken header in POST request
Solution: Include header from login response
```

**400 Bad Request - No Active Deck:**
```
"You do not have an active deck. Please set one as active first."
Solution: Set a deck as is_active=True before joining queue
```

**400 Bad Request - Already in Queue:**
```
"You are already in the queue or matched"
Solution: Leave queue first before joining again
```

---

## Process Monitoring

### Matchmaker Logs Example

```
✓ Matchmaker started
Listening for queued players (polling every 2s)...

[Queue: 1 player] testplayer queued
[Queue: 2 players] testplayer2 queued
✓ Created match between testplayer and testplayer2 on port 7777 (PID: 34388)

[Match Running] Match on port 7777 (PID: 34388)
⚠ Server died on port 7777 (PID: 34388, exit code: 1)
  Re-queued testplayer
  Re-queued testplayer2

Shutting down all servers...
✓ Terminated server on port 7777
✓ Matchmaker stopped
```

---

## Production Considerations

1. **SSL/TLS**: Use HTTPS in production
2. **CORS**: Configure allowed origins properly
3. **Rate Limiting**: Add rate limits to prevent abuse
4. **Database**: Use PostgreSQL instead of SQLite
5. **Logging**: Set up persistent log aggregation
6. **Monitoring**: Monitor server health and match statistics
7. **Scaling**: Consider multiple matchmaker instances
8. **Firewall**: Open necessary ports (7777-7800) on production server

