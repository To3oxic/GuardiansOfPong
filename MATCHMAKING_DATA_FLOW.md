# Matchmaking Data Flow: How Clients Receive Server Information

## Overview

When clients are in queue and a match is found, they receive server connection information through the `/api/matchmaking/queue-status/` endpoint response. This document explains the complete data flow.

---

## Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT SIDE (Unity)                      │
└─────────────────────────────────────────────────────────────────┘

Client 1 joins queue          Client 2 joins queue
    ↓                              ↓
POST /api/matchmaking/join-queue/
    ↓
[MatchmakingManager.JoinQueue()]
    ↓
isSearching = true
polls every 1-2 seconds
    ↓
GET /api/matchmaking/queue-status/
    ↓
Response: {"status":"queued", "queue_position":1}
    ↓
[OnStatusResponse] → UpdateQueuePositionUI()
    ↓
[Still polling...]

═════════════════════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────────────┐
│                      BACKEND SIDE (Django)                      │
└─────────────────────────────────────────────────────────────────┘

Matchmaker detects 2 queued players
    ↓
Creates MatchInstance: status = "initializing"
    ↓
Spawns headless server: port 7777
    ↓
Stores server details in MatchInstance database:
  - server_ip: "127.0.0.1"
  - server_port: 7777
  - server_pid: 12345
  - status: "initializing"

═════════════════════════════════════════════════════════════════

Headless server initializes
    ↓
Fetches both player decks
    ↓
POST /api/matchmaking/server-ready/
    ↓
Backend updates: MatchInstance.status = "ready"

═════════════════════════════════════════════════════════════════

┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT SIDE (Unity)                      │
└─────────────────────────────────────────────────────────────────┘

Still polling...
    ↓
GET /api/matchmaking/queue-status/
    ↓
Response: {"status":"matched", "match_instance":{...}}
    ↓
[OnStatusResponse] checks status
    ↓
if status == "matched" AND match_instance.status == "ready"
    ↓
[OnMatchFound()] extracts server info:
  - serverIp = match_instance.server_ip
  - serverPort = match_instance.server_port
    ↓
[ConnectToGameServer()]
    ↓
StartClient() → connects to 127.0.0.1:7777
    ↓
OnClientConnected() fires
    ↓
Load GameArena scene
    ↓
Game starts!
```

---

## Data Structure: Queue Status Response

### When Status = "queued" (Waiting for Opponent)

```json
{
  "status": "queued",
  "queue_position": 2,
  "message": "Waiting for opponent..."
}
```

**What it contains:**
- `status`: "queued" → Player is in queue
- `queue_position`: Position in queue (1st, 2nd, etc)

**What client does:** Shows "Position in queue: 2", continues polling

---

### When Status = "matched" (Opponent Found + Match Created)

```json
{
  "status": "matched",
  "match_instance": {
    "id": "abc123-def456-ghi789",
    "player1_username": "player1",
    "player2_username": "player2",
    "player1_deck": {
      "id": "deck1",
      "name": "Pyro Deck",
      "cards": [...]
    },
    "player2_deck": {
      "id": "deck2",
      "name": "Frost Deck",
      "cards": [...]
    },
    "server_ip": "127.0.0.1",
    "server_port": 7777,
    "server_pid": 12345,
    "status": "initializing",
    "created_at": "2025-11-30T15:30:00Z",
    "started_at": null,
    "ended_at": null
  }
}
```

**Key fields for connection:**
- `match_instance.server_ip`: IP address of headless server
- `match_instance.server_port`: Port headless server is listening on
- `match_instance.status`: "initializing" or "ready"
- `match_instance.player1_deck`: Opponent's deck configuration
- `match_instance.player2_deck`: Your deck configuration

---

## Client Code: How Server Info is Extracted

### 1. Polling Loop (Every 1-2 seconds)

**File:** `MatchmakingManager.cs` line 218

```csharp
private IEnumerator CheckMatchStatusCoroutine()
{
    string backendUrl = "http://localhost:8000";
    
    using (UnityWebRequest request = UnityWebRequest.Get(
        $"{backendUrl}/api/matchmaking/queue-status/"))
    {
        // ... set up cookies ...
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            OnStatusResponse(request.downloadHandler.text);  // ← Parse response
        }
    }
}
```

### 2. Response Parsing

**File:** `MatchmakingManager.cs` line 261

```csharp
private void OnStatusResponse(string response)
{
    Debug.Log("[MatchmakingManager] Status response: " + response);
    
    var json = JsonUtility.FromJson<StatusResponse>(response);
    
    if (json.status == "queued")
    {
        // Still waiting for opponent
        UpdateQueuePositionUI(json.queue_position);
    }
    else if (json.status == "matched")
    {
        // MATCH FOUND! Extract server info
        var matchInstance = json.match_instance;
        
        Debug.Log($"Server: {matchInstance.server_ip}:{matchInstance.server_port}");
        
        if (matchInstance.status == "ready")
        {
            OnMatchFound(json);  // ← Start connection
        }
    }
}
```

### 3. Extract and Connect

**File:** `MatchmakingManager.cs` line 307

```csharp
private void OnMatchFound(StatusResponse matchInfo)
{
    var matchInstance = matchInfo.match_instance;
    
    // Extract server information
    serverIp = matchInstance.server_ip;           // "127.0.0.1"
    serverPort = matchInstance.server_port;       // 7777
    
    // Also get deck information for the game
    var decks = new MatchData
    {
        opponentName = matchInstance.player2_username,
        serverIp = serverIp,
        serverPort = serverPort,
        player1_deck = matchInstance.player1_deck,
        player2_deck = matchInstance.player2_deck
    };
    
    // Start connection
    StartCoroutine(ConnectToGameServer());
}
```

### 4. Connection

**File:** `MatchmakingManager.cs` line 337

```csharp
private IEnumerator ConnectToGameServer()
{
    // Configure transport with server IP and port from response
    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    
    transport.SetConnectionData(serverIp, (ushort)serverPort);
    //                            ↑                    ↑
    //                    From queue-status response
    
    // Connect
    NetworkManager.Singleton.StartClient();
    
    // Wait for connection callback to load scene
}
```

---

## Backend Data Flow

### Step 1: Create Match (When 2 Players Found)

**File:** `matchmaking/matchmaker.py`

```python
# Matchmaker detects 2 queued players
player1 = queue.pop(0)
player2 = queue.pop(0)

# Create match in database
match = MatchInstance.objects.create(
    player1=player1,
    player2=player2,
    status='initializing',
    server_port=7777,  # Assign port
    server_ip='127.0.0.1'
)

# Spawn headless server
subprocess.Popen([
    'Myproject.exe',
    '-batchmode', '-nographics', '-server',
    f'-player1Id={player1.id}',
    f'-player2Id={player2.id}',
    f'-port=7777'
])
```

### Step 2: Respond to Queue Status Check

**File:** `matchmaking/views.py` - `queue_status` endpoint

```python
@api_view(['GET'])
def queue_status(request):
    player = get_player_from_request(request)
    
    # Check if player is in queue
    queue_entry = QueueEntry.objects.filter(player=player).first()
    
    if not queue_entry:
        # Player not in queue or already matched
        if player in matched_players:
            # Player is in a match
            match = MatchInstance.objects.get(player1=player) or \
                    MatchInstance.objects.get(player2=player)
            
            return Response({
                'status': 'matched',
                'match_instance': {
                    'id': match.id,
                    'player1_username': match.player1.username,
                    'player2_username': match.player2.username,
                    'player1_deck': match.player1_deck.to_dict(),
                    'player2_deck': match.player2_deck.to_dict(),
                    'server_ip': match.server_ip,      # ← Key field!
                    'server_port': match.server_port,  # ← Key field!
                    'server_pid': match.server_pid,
                    'status': match.status,            # "ready" when server ready
                    'created_at': match.created_at,
                    'started_at': match.started_at,
                    'ended_at': match.ended_at
                }
            })
        else:
            return Response({
                'status': 'none',
                'message': 'Not in queue or matched'
            })
    
    # Still in queue
    return Response({
        'status': 'queued',
        'queue_position': queue_entry.queue_position
    })
```

### Step 3: Update Status When Server Ready

**File:** `matchmaking/views.py` - `server_ready` endpoint

```python
@api_view(['POST'])
def server_ready(request):
    # Find the initializing match
    match = MatchInstance.objects.filter(
        status='initializing'
    ).order_by('-created_at').first()
    
    if match:
        # Update status to "ready"
        match.status = 'ready'
        match.save()
        
        print(f"✓ Match {match.id} ready on port {match.server_port}")
        # Now when clients poll, match_instance.status will be "ready"
        # and they will connect
```

---

## Connection Information Lifecycle

| When | Server IP | Server Port | Who Sets It | Status |
|------|-----------|------------|-------------|--------|
| Match created | 127.0.0.1 | 7777 | Matchmaker | "initializing" |
| Server spawned | 127.0.0.1 | 7777 | Headless process | "initializing" |
| Server ready | 127.0.0.1 | 7777 | server-ready endpoint | **"ready"** |
| Client polls | **Gets above** | **Gets above** | queue-status endpoint | "ready" |
| Client connects | Connects to ↑ | Connects to ↑ | MatchmakingManager | Success! |

---

## Timing Sequence

```
T+0s    | Player 1 joins queue → isSearching = true
T+0s    | Player 2 joins queue → isSearching = true
        |
        | Both poll every 1-2 seconds:
        | "status": "queued", "queue_position": 1
T+0.5s  | Both poll again
T+1s    | Both poll again
T+1.5s  | ...
        |
        |  [BACKEND: Matchmaker detects 2 queued players]
        |  [BACKEND: Creates MatchInstance, spawns server]
        |
T+2s    | Both poll
        | Response: "status": "matched", "match_instance": {...}
        |           BUT match_instance.status = "initializing"
        |
        | Client code checks: if status == "ready" ?
        | NO → continue polling
        |
        | [BACKEND: Headless server fetches decks]
        | [BACKEND: Server calls server-ready endpoint]
        | [BACKEND: MatchInstance.status = "ready"]
        |
T+3s    | Both poll
        | Response: match_instance.status = "ready" ✓
        | OnMatchFound() called
        | StartClient() called
        |
T+3.5s  | Connection established via Netcode
        | OnClientConnected() fires
        | Scene loads
        | Game starts!
```

---

## Summary

**When clients are in queue, they receive server information through:**

1. **Polling:** Every 1-2 seconds via `GET /api/matchmaking/queue-status/`

2. **When status changes to "matched":**
   - Response includes `match_instance` object
   - Contains `server_ip` and `server_port`
   - Also contains match status ("initializing" → "ready")

3. **When server is ready:**
   - Match status updates to "ready"
   - Next poll returns complete server info
   - Client extracts IP:port from response

4. **Connection:**
   - MatchmakingManager gets IP:port from queue-status response
   - Calls `transport.SetConnectionData(ip, port)`
   - Calls `NetworkManager.StartClient()`
   - Connects to headless server

---

## Data Classes (C#)

### StatusResponse
```csharp
public class StatusResponse
{
    public string status;                      // "queued" | "matched" | "none"
    public int queue_position;                 // if queued
    public MatchInstanceResponse match_instance; // if matched
}
```

### MatchInstanceResponse
```csharp
public class MatchInstanceResponse
{
    public string id;
    public string player1_username;
    public string player2_username;
    public object player1_deck;
    public object player2_deck;
    public string server_ip;        // ← Connection target
    public int server_port;         // ← Connection target
    public int server_pid;
    public string status;           // "initializing" | "ready" | "running"
    public string created_at;
    public string started_at;
    public string ended_at;
}
```

---

## Debugging Checklist

- [ ] Backend matchmaker creates MatchInstance with server_ip and server_port
- [ ] Backend spawns headless process on correct port
- [ ] queue-status endpoint returns match_instance when status="matched"
- [ ] server_ip and server_port are correctly populated in response
- [ ] Headless server calls server-ready endpoint
- [ ] MatchInstance.status changes to "ready" after server-ready call
- [ ] Client's next poll sees status="ready"
- [ ] Client extracts serverIp and serverPort correctly
- [ ] Client calls transport.SetConnectionData(ip, port)
- [ ] Client calls StartClient() and connects

---

## Common Issues

**Issue:** "Not in queue or matched" error
- **Cause:** Backend didn't update queue status or match doesn't exist
- **Fix:** Check backend logs, verify MatchInstance was created

**Issue:** Client receives IP/port but can't connect
- **Cause:** Server not actually running on that port, firewall blocking, wrong IP
- **Fix:** Check if headless server process is running, check server logs

**Issue:** Client tries to connect before server is ready
- **Cause:** match_instance.status is still "initializing"
- **Fix:** Make sure server calls server-ready endpoint after fetching decks

**Issue:** "Server initializing... waiting for ready status"
- **Cause:** Headless server is still starting up
- **Fix:** This is normal! Client will retry on next poll. Check server logs to debug if it takes too long.

