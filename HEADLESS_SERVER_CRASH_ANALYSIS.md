# Headless Server Crash Analysis & Frontend Requirements

## Problem Summary

The headless server is **crashing with exit code 1** immediately after initialization. The server starts successfully, initializes networking on port 7777, but then exits without error message.

**Log Evidence:**
```
✓ Created match between testplayer and testplayer2 on port 7777 (PID: 31524)
⚠ Server died on port 7777 (PID: 31524, exit code: 1)
```

**Last Log Output:**
```
[NetworkUI] Configured transport: 0.0.0.0:7777, MaxPacketQueueSize: 512
```

Then the process terminates.

---

## Root Cause Analysis

The crash is **NOT** caused by:
- ❌ Port binding issues (port 7777 is available)
- ❌ Backend API problems (API is working correctly)
- ❌ Missing executable (file exists and starts)
- ❌ Shader/GPU issues (expected for headless, doesn't cause crashes)

**The crash IS caused by:**
- ✅ **Missing server game loop** - Server initializes but has no main loop to keep running
- ✅ **Command-line arguments not parsed** - Server doesn't read `-player1Id` and `-player2Id`
- ✅ **No game logic after initialization** - Server starts then exits because there's nothing keeping it alive

---

## Required Frontend Changes

Your Unity headless server **MUST** implement:

### 1. Parse Command-Line Arguments

```csharp
void Start()
{
    // Get command-line arguments
    string[] args = System.Environment.GetCommandLineArgs();
    
    string player1Id = null;
    string player2Id = null;
    int port = 7777;
    
    // Parse arguments
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith("-player1Id="))
            player1Id = args[i].Replace("-player1Id=", "");
        
        if (args[i].StartsWith("-player2Id="))
            player2Id = args[i].Replace("-player2Id=", "");
        
        if (args[i].StartsWith("-port="))
            port = int.Parse(args[i].Replace("-port=", ""));
    }
    
    Debug.Log($"[Server] Started with:");
    Debug.Log($"  Player 1 ID: {player1Id}");
    Debug.Log($"  Player 2 ID: {player2Id}");
    Debug.Log($"  Port: {port}");
    
    // Initialize server
    InitializeServer(player1Id, player2Id, port);
}
```

### 2. Fetch Player Decks from Backend

```csharp
private IEnumerator FetchPlayerDecks(string player1Id, string player2Id)
{
    Debug.Log("[Server] Fetching player decks...");
    
    // Fetch Player 1 Deck
    using (UnityWebRequest request1 = UnityWebRequest.Get(
        $"http://localhost:8000/api/players/{player1Id}/active-deck/"))
    {
        yield return request1.SendWebRequest();
        
        if (request1.result == UnityWebRequest.Result.Success)
        {
            string deckJson = request1.downloadHandler.text;
            Debug.Log($"[Server] Player 1 Deck: {deckJson}");
            // Parse and store deck data
            var player1Deck = JsonUtility.FromJson<DeckData>(deckJson);
        }
        else
        {
            Debug.LogError($"[Server] Failed to fetch Player 1 deck: {request1.error}");
            Application.Quit(1);
            yield break;
        }
    }
    
    // Fetch Player 2 Deck
    using (UnityWebRequest request2 = UnityWebRequest.Get(
        $"http://localhost:8000/api/players/{player2Id}/active-deck/"))
    {
        yield return request2.SendWebRequest();
        
        if (request2.result == UnityWebRequest.Result.Success)
        {
            string deckJson = request2.downloadHandler.text;
            Debug.Log($"[Server] Player 2 Deck: {deckJson}");
            // Parse and store deck data
            var player2Deck = JsonUtility.FromJson<DeckData>(deckJson);
        }
        else
        {
            Debug.LogError($"[Server] Failed to fetch Player 2 deck: {request2.error}");
            Application.Quit(1);
            yield break;
        }
    }
    
    // Start game
    StartGameMatch();
}
```

### 3. Keep Server Running - Main Game Loop

```csharp
void Update()
{
    // Main server game loop - processes network messages, game logic, etc.
    
    // This Update() method keeps the server alive
    // Process incoming connections
    // Handle game state
    // Send updates to clients
    
    // Without this, the server has no reason to stay running
}

private void StartGameMatch()
{
    Debug.Log("[Server] Game match initialized - waiting for players...");
    gameState = GameState.WaitingForPlayers;
    
    // Server is now running and will stay alive due to Update() loop
    // Listen for player connections on the port
    // When both players connected, start the actual game
}
```

### 4. Deck Data Structure

```csharp
[System.Serializable]
public class CardData
{
    public string id;
    public string name;
    public string registry_name;
    public string description;
    public string card_type;
    public string rarity;
    public int max_level;
    public int level;
    public int copies;
}

[System.Serializable]
public class DeckData
{
    public string id;
    public string deck_name;
    public bool is_active;
    public CardData card_slot_1;
    public CardData card_slot_2;
    public CardData card_slot_3;
    public CardData card_slot_4;
    public CardData card_slot_5;
    public CardData card_slot_6;
    public CardData card_slot_7;
    public CardData card_slot_8;
    public CardData legend_stone;
    public CardData guardian_card;
    public string created_at;
    public string updated_at;
}
```

---

## Command Format Received

Your server receives this exact command:

```bash
C:\Users\ahmad\My project\testbuild\Myproject.exe \
  -batchmode \
  -nographics \
  -server \
  -port=7777 \
  -player1Id=550e8400-e29b-41d4-a716-446655440000 \
  -player2Id=6ba7b810-9dad-11d1-80b4-00c04fd430c8
```

**Your server MUST:**
1. ✅ Parse `-player1Id={uuid}`
2. ✅ Parse `-player2Id={uuid}`
3. ✅ Parse `-port={number}`
4. ✅ Fetch both decks from the backend
5. ✅ Stay running (don't quit after initialization)

---

## Backend API Endpoints Available

### Fetch Player Deck

**Endpoint:** `GET /api/players/{player_id}/active-deck/`

**Response Example:**
```json
{
  "id": "deck-uuid",
  "deck_name": "My First Deck",
  "is_active": true,
  "card_slot_1": {
    "card": {
      "id": "card-uuid",
      "name": "ToxinBall",
      "registry_name": "ToxinBall",
      "description": "Poison spell",
      "card_type": "spell",
      "rarity": "common",
      "max_level": 5
    },
    "level": 1,
    "copies": 3
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
  },
  "created_at": "2025-11-30T10:00:00Z",
  "updated_at": "2025-11-30T10:00:00Z"
}
```

**Note:** This endpoint is public (no authentication required) for localhost access.

---

## Server Lifecycle

### Current Broken Flow:
```
1. Matchmaker spawns: Myproject.exe -player1Id=... -player2Id=... -port=7777
2. Unity starts
3. Network initialized on port 7777 ✓
4. [NetworkUI] Configured transport: 0.0.0.0:7777 ✓
5. No game loop running
6. Server exits (exit code 1) ✗
```

### Required Fixed Flow:
```
1. Matchmaker spawns: Myproject.exe -player1Id=... -player2Id=... -port=7777
2. Unity starts
3. Parse command-line arguments ✓
4. Network initialized on port 7777 ✓
5. Fetch Player 1 deck from backend ✓
6. Fetch Player 2 deck from backend ✓
7. Start game loop (Update() running) ✓
8. Listen for client connections on port 7777 ✓
9. When both players connect → Start match ✓
10. Server continues running until match ends ✓
```

---

## Expected Server Log Output (After Fix)

```
[Server] Started with:
  Player 1 ID: 550e8400-e29b-41d4-a716-446655440000
  Player 2 ID: 6ba7b810-9dad-11d1-80b4-00c04fd430c8
  Port: 7777

[Server] Fetching player decks...
[Server] Player 1 Deck: {"id":"deck-uuid","deck_name":"My First Deck",...}
[Server] Player 2 Deck: {"id":"deck-uuid","deck_name":"My First Deck",...}
[Server] Game match initialized - waiting for players...
[Server] Port 7777 listening for connections...
[Server] Player 1 connected
[Server] Player 2 connected
[Server] Match started!
```

---

## Common Mistakes to Avoid

❌ **Not parsing command-line arguments** - Server doesn't know which players to load
❌ **Not keeping a game loop running** - Update() method missing or disabled
❌ **Hardcoding deck data** - Should fetch from backend API
❌ **Quitting immediately after initialization** - Need Application.Quit() only on error
❌ **Not logging** - Make debugging impossible
❌ **Wrong URL format** - Use full URL with http://localhost:8000
❌ **Not handling fetch failures** - Should error gracefully if deck fetch fails

---

## Debugging Tips

1. **Check server logs** in `server_logs/server_7777_*.log`
2. **Add verbose logging** at every step (argument parsing, fetch, game loop)
3. **Test locally first** - Run server manually with test arguments before matchmaker
4. **Verify port is listening** - Use `netstat -ano | findstr :7777` after server starts
5. **Check backend API** - Verify `GET /api/players/{id}/active-deck/` returns data
6. **Monitor process** - Check if process PID matches and stays alive

---

## What Backend is Providing ✅

- ✅ Correct player IDs via command-line arguments
- ✅ Correct port allocation (7777-7800)
- ✅ Player deck API endpoint
- ✅ All card data with registry names
- ✅ Server process spawning and monitoring
- ✅ Logging and error tracking
- ✅ Automatic server restart on crash

---

## What Frontend Must Provide ⚠️

- ⚠️ Command-line argument parsing
- ⚠️ Server game loop (Update method)
- ⚠️ Deck fetching from backend API
- ⚠️ Network connection handling
- ⚠️ Game state management
- ⚠️ Keep-alive logic (don't quit after init)
- ⚠️ Client connection listening on assigned port

