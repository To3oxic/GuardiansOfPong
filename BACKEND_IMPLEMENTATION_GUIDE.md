# Backend Implementation Strategy - GuardiansOfPong

## Overview

Your game flow requires a **three-tier backend architecture**:

```
┌─────────────────────────────────────────────────────────┐
│                   API Server (Always Running)           │
│  - Player authentication & data retrieval               │
│  - Matchmaking queue management                         │
│  - Database connectivity                                │
│  - Headless server spawning & lifecycle                 │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│            Matchmaking Service (Always Running)         │
│  - Queue management (Redis or in-memory)                │
│  - Match pairing algorithm                              │
│  - Server instance allocation                           │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│       Headless Game Servers (Per-Match Instances)       │
│  - One per active match                                 │
│  - Spawned on demand                                    │
│  - Terminated when match ends                           │
│  - Syncs all gameplay                                   │
└─────────────────────────────────────────────────────────┘
```

---

## Recommended Tech Stack

### API Server
**Option A: Node.js + Express (Recommended for rapid development)**
- Lightweight, JavaScript-based
- Large ecosystem (matchmaking libraries, websockets)
- Easy deployment on cloud platforms
- Good for real-time features

**Option B: C# + ASP.NET Core (Same language as Unity)**
- Type safety across entire pipeline
- Familiar to your team
- Good for complex game logic
- More enterprise-grade

**Pick: Node.js + Express** - Faster to develop, easier to scale

### Database
- **PostgreSQL** (primary - player data, decks, match history)
- **Redis** (secondary - matchmaking queue, session cache)

### Message Queue (Optional but recommended)
- **RabbitMQ** or **Redis** for matchmaking events

### Hosting
- **AWS EC2** for API server and matchmaking
- **AWS Lambda** for headless server spawning (auto-scaling)
- **Docker** containerization for consistency

---

## Architecture Deep Dive

### 1. Authentication & Player Data Service

**Endpoint: `POST /api/auth/login`**
```json
Request:
{
  "username": "player_name",
  "password": "hashed_password"
}

Response:
{
  "success": true,
  "token": "jwt_token",
  "playerId": "uuid",
  "player": {
    "username": "player_name",
    "level": 10,
    "rank": "Gold",
    "coins": 5000,
    "wins": 45,
    "losses": 12
  },
  "decks": [
    {
      "deckId": "deck_1",
      "deckName": "Aggro Knight",
      "cards": [
        { "cardId": "brave_knight", "level": 3, "quantity": 2 },
        { "cardId": "toxin_ball", "level": 1, "quantity": 3 },
        ...
      ],
      "isActive": true
    },
    ...
  ],
  "unlockedCards": [
    { "cardId": "brave_knight", "maxLevel": 3 },
    { "cardId": "toxin_ball", "maxLevel": 1 },
    ...
  ]
}
```

**Flow:**
1. Client starts → Calls `/api/auth/login` with credentials
2. Backend validates, generates JWT token
3. Returns player profile + all decks + card collection
4. Client caches locally (PlayerPrefs or encrypted file)
5. MainMenu displays player info from cached data

**Client Code (Pseudocode):**
```csharp
public class PlayerDataManager
{
    public static async Task<PlayerData> LoadPlayerData(string username, string password)
    {
        var response = await API.Post("/auth/login", new { username, password });
        
        if (response.success)
        {
            PlayerData data = response.player;
            PlayerPrefs.SetString("auth_token", response.token);
            CachePlayerData(data);
            return data;
        }
        return null;
    }
}
```

---

### 2. Matchmaking Service

**Endpoint: `POST /api/matchmaking/queue`**
```json
Request:
{
  "playerId": "uuid",
  "token": "jwt_token",
  "selectedDeckId": "deck_1",
  "skillRating": 1200  // For ranking matches
}

Response:
{
  "queueId": "queue_123",
  "position": 5,
  "estimatedWait": "2-3 minutes"
}
```

**Endpoint: `GET /api/matchmaking/status/:queueId`**
```json
Response (Waiting):
{
  "status": "searching",
  "position": 3
}

Response (Match Found):
{
  "status": "matched",
  "matchId": "match_uuid",
  "serverIp": "54.123.45.67",
  "serverPort": 7778,
  "opponentId": "opponent_uuid"
}
```

**Backend Matchmaking Logic:**

```
Queue System (In-Memory + Redis):
1. Client enters queue with deckId
2. Matchmaking runs every 5 seconds:
   - Pair similar skill players
   - Prefer players waiting 30+ seconds
   - If match found:
     a. Reserve port (7777 + match_id % 1000)
     b. Spawn headless server: 
        ./Myproject.exe -nographics -batchmode -server -port <PORT>
     c. Fetch both decks from DB
     d. Send server decks via localhost TCP
     e. Respond to both clients with server IP:Port
     f. Start match timeout (30s to connect, 15m max game time)
   - If timeout, refund to queue

Queue State Management:
- Player1: queuing, deckId, timestamp, skillRating
- Player2: queuing, deckId, timestamp, skillRating
↓
- Both matched
- Headless spawned, IP:Port returned
↓
- Both clients connect (confirmed)
- Match locked, timer started
↓
- Match ends, results sent
- Both removed from queue
- Match history recorded
```

**Client Code:**
```csharp
public class MatchmakingManager
{
    private string queueId;
    private Coroutine statusCheckCoroutine;
    
    public async Task EnterQueue(string deckId)
    {
        var response = await API.Post("/matchmaking/queue", new { deckId });
        queueId = response.queueId;
        
        // Poll for match every 2 seconds
        statusCheckCoroutine = StartCoroutine(PollMatchStatus());
    }
    
    private IEnumerator PollMatchStatus()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);
            
            var status = await API.Get($"/matchmaking/status/{queueId}");
            
            if (status.status == "matched")
            {
                OnMatchFound(status);
                break;
            }
        }
    }
    
    private void OnMatchFound(dynamic matchData)
    {
        // Connect to headless server
        NetworkManager.Singleton.GetComponent<NetworkTransport>().ConnectionData.Address = matchData.serverIp;
        NetworkManager.Singleton.GetComponent<NetworkTransport>().ConnectionData.Port = matchData.serverPort;
        NetworkManager.Singleton.StartClient();
    }
}
```

---

### 3. Headless Server Spawning & Management

**Backend Process:**

```csharp
// On match found, backend does:

public class MatchSpawner
{
    public async Task SpawnMatchServer(string matchId, string player1Id, string player2Id, 
                                       string deck1Id, string deck2Id)
    {
        // 1. Allocate port
        int port = AllocatePort(); // 7777 + unique offset
        
        // 2. Fetch decks from DB
        var deck1 = await DB.GetDeck(deck1Id);
        var deck2 = await DB.GetDeck(deck2Id);
        
        // 3. Start headless process
        var process = new ProcessStartInfo()
        {
            FileName = "/path/to/Myproject.exe",
            Arguments = $"-nographics -batchmode -server -port {port} -matchId {matchId}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        
        var serverProcess = Process.Start(process);
        
        // 4. Wait for server to be ready (ping port for 10s)
        bool serverReady = await WaitForServerReady(port, 10000);
        
        if (!serverReady)
        {
            serverProcess.Kill();
            throw new Exception("Headless server failed to start");
        }
        
        // 5. Send deck data to server (TCP or local file)
        await SendDecksToServer(port, deck1, deck2);
        
        // 6. Record in database
        await DB.CreateMatch(new Match
        {
            MatchId = matchId,
            Player1Id = player1Id,
            Player2Id = player2Id,
            ServerPort = port,
            StartTime = DateTime.UtcNow,
            Status = "active"
        });
        
        // 7. Start timeout monitor (kill if no clients connect in 30s)
        MonitorMatchTimeout(matchId, serverProcess, 30000);
    }
    
    private void MonitorMatchTimeout(string matchId, Process process, int timeoutMs)
    {
        Task.Delay(timeoutMs).ContinueWith(_ =>
        {
            if (process.IsRunning)
            {
                process.Kill();
                DB.UpdateMatch(matchId, new { Status = "timeout" });
            }
        });
    }
}
```

**Headless Server Modifications (Client-side C#):**

```csharp
// In NetworkUI or GameManager:

public class HeadlessServerInitializer : MonoBehaviour
{
    private void Start()
    {
        // Parse command line args
        string[] args = System.Environment.GetCommandLineArgs();
        
        bool isServer = args.Contains("-server");
        string matchId = GetArgValue(args, "-matchId");
        
        if (isServer)
        {
            // Server mode
            NetworkManager.Singleton.StartServer();
            
            // Wait for deck data (sent by backend)
            StartCoroutine(WaitForDeckData(matchId));
        }
        else
        {
            // Client mode - handled by MatchmakingManager
        }
    }
    
    private IEnumerator WaitForDeckData(string matchId)
    {
        // Server waits for deck data on TCP port 9999
        var deckData = await ListenForDeckData(9999);
        
        // Store decks globally
        GameManager.Instance.SetPlayerDecks(deckData.player1Deck, deckData.player2Deck);
        
        // Wait for clients to connect (they have 30s)
        yield return new WaitForSeconds(30f);
        
        if (/* both clients connected */)
        {
            GameManager.Instance.StartMatch();
        }
        else
        {
            // Timeout - exit
            Application.Quit();
        }
    }
}
```

---

### 4. Match Result Service

**Endpoint: `POST /api/match/complete`**
```json
Request:
{
  "matchId": "match_uuid",
  "winnerId": "player_uuid",
  "loserId": "player_uuid",
  "duration": 180,  // seconds
  "player1Score": 5,
  "player2Score": 3
}

Response:
{
  "success": true,
  "rewards": {
    "winnerId": {
      "coinsAwarded": 100,
      "xpGained": 25,
      "newLevel": 11
    },
    "loserId": {
      "coinsAwarded": 30,
      "xpGained": 10
    }
  }
}
```

**Backend Flow:**
1. Headless server calls this endpoint when match ends
2. Backend updates player stats
3. Calculates rewards
4. Returns rewards to client
5. Terminates headless process
6. Records match history

---

## Database Schema

### Players Table
```sql
CREATE TABLE players (
  id UUID PRIMARY KEY,
  username VARCHAR(50) UNIQUE NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  email VARCHAR(100) UNIQUE,
  
  -- Stats
  level INT DEFAULT 1,
  rank VARCHAR(20) DEFAULT 'Bronze',
  total_coins INT DEFAULT 100,
  total_wins INT DEFAULT 0,
  total_losses INT DEFAULT 0,
  skill_rating INT DEFAULT 1200,
  
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP DEFAULT NOW()
);
```

### Decks Table
```sql
CREATE TABLE decks (
  id UUID PRIMARY KEY,
  player_id UUID NOT NULL REFERENCES players(id),
  deck_name VARCHAR(100),
  
  -- JSON array of cards
  cards JSONB NOT NULL, -- [{ cardId, level, quantity }, ...]
  
  is_active BOOLEAN DEFAULT false,
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP DEFAULT NOW()
);
```

### Player Card Collection Table
```sql
CREATE TABLE player_cards (
  id UUID PRIMARY KEY,
  player_id UUID NOT NULL REFERENCES players(id),
  card_id VARCHAR(100) NOT NULL,
  level INT DEFAULT 1,
  
  UNIQUE(player_id, card_id)
);
```

### Matches Table
```sql
CREATE TABLE matches (
  id UUID PRIMARY KEY,
  player1_id UUID NOT NULL REFERENCES players(id),
  player2_id UUID NOT NULL REFERENCES players(id),
  winner_id UUID REFERENCES players(id),
  
  player1_deck_id UUID REFERENCES decks(id),
  player2_deck_id UUID REFERENCES decks(id),
  
  server_port INT,
  duration INT,
  
  status VARCHAR(20), -- 'active', 'completed', 'timeout'
  created_at TIMESTAMP DEFAULT NOW(),
  completed_at TIMESTAMP
);
```

---

## Implementation Phases

### Phase 1: Foundation (Week 1-2)
- [ ] Set up Node.js + Express server
- [ ] Create PostgreSQL + Redis setup
- [ ] Implement authentication endpoints
- [ ] Implement player data retrieval
- [ ] Test with Postman/ThunderClient

### Phase 2: Matchmaking (Week 2-3)
- [ ] Implement queue system (Redis)
- [ ] Implement pairing algorithm
- [ ] Implement queue status polling
- [ ] Update client to call endpoints

### Phase 3: Server Spawning (Week 3-4)
- [ ] Implement headless spawning (local testing first)
- [ ] Implement deck transfer to headless
- [ ] Implement server port allocation
- [ ] Implement timeout management
- [ ] Test locally with multiple instances

### Phase 4: Match Results (Week 4)
- [ ] Implement match completion endpoint
- [ ] Implement reward calculation
- [ ] Implement match history recording
- [ ] Test end-to-end flow

### Phase 5: Production Deployment (Week 5+)
- [ ] Cloud deployment (AWS EC2)
- [ ] Load testing
- [ ] Auto-scaling configuration
- [ ] Monitoring & logging
- [ ] Database optimization

---

## Local Testing Setup

Before deploying to cloud, test locally:

```bash
# Terminal 1 - Start API server
npm install
npm start
# Output: Server listening on http://localhost:3000

# Terminal 2 - Start Redis
redis-server

# Terminal 3 - Start first game client
cd path/to/game
./Myproject.exe

# Terminal 4 - Start second game client
./Myproject.exe
```

---

## Key Decisions & Recommendations

1. **Deck Transmission Method**
   - ✅ Via API response (simple, secure, no extra connections)
   - ❌ Via direct TCP to headless (complex, needs encryption)

2. **Server Spawning**
   - ✅ Local subprocess (for testing)
   - ✅ Docker containers (for cloud)
   - ❌ EC2 instances per match (too slow)

3. **Session Management**
   - ✅ JWT tokens + Redis cache (stateless, scalable)
   - ❌ Server-side sessions (limits scalability)

4. **Matchmaking Algorithm**
   - ✅ Skill-based pairing (ranked mode)
   - ✅ FIFO with max wait time (casual mode)
   - Start with FIFO, add skill-based later

5. **Error Handling**
   - If headless fails to start: return to queue, notify player
   - If client disconnects: opponent wins, match recorded
   - If timeout: both get refunded queue position

---

## Security Considerations

1. **JWT Token Validation** on every API call
2. **Deck Integrity** - Validate deck before match starts (no client tampering)
3. **Rate Limiting** on queue endpoint (prevent spam)
4. **Password Hashing** (bcrypt, not plaintext)
5. **HTTPS/TLS** for all API communication
6. **Input Sanitization** on all endpoints

---

## Monitoring & Logging

```javascript
// Log structure
{
  timestamp: "2025-11-27T10:30:00Z",
  event: "match_spawned" | "match_completed" | "error",
  matchId: "uuid",
  players: [player1Id, player2Id],
  duration: 180,
  error?: "headless_failed_to_start"
}
```

Use **ELK Stack** (Elasticsearch, Logstash, Kibana) for production logging.

