# Headless Server Backend Integration Guide

## 📋 Overview

The Unity headless server now fetches player decks directly from the backend database instead of waiting for client submissions. This guide explains the complete flow and what the backend needs to implement.

---

## 🔄 Complete Flow

```
1. Backend Matchmaker (matchmaker.py service)
   ↓
2. Finds 2 matched players
   ↓
3. Spawns headless server with:
   build.exe -server -player1Id=<UUID> -player2Id=<UUID>
   ↓
4. Headless Server Starts (NetworkUI.cs)
   ├─ Parses command line: player1Id, player2Id
   ├─ Starts Netcode server on port 7777 (default)
   ├─ Fetches from backend:
   │  ├─ GET /api/players/{player1Id}/active-deck/
   │  └─ GET /api/players/{player2Id}/active-deck/
   ├─ Stores decks in TempPlayerDecks[0] and TempPlayerDecks[1]
   └─ Waits for both clients to connect
   ↓
5. Two Clients Connect
   ├─ Client 1 connects to server IP:7777
   ├─ Client 2 connects to server IP:7777
   └─ Both authenticated via SessionID
   ↓
6. Server has all data, loads GameScene
   ├─ Calls GameManager.InitializeFromDecks(TempPlayerDecks)
   ├─ GameManager receives both player decks
   └─ Match begins
```

---

## 🛠️ Backend Changes Required

### 1. New API Endpoint: Get Active Deck

**Location:** `guardians_backend/players/views.py`

**Endpoint:**
```
GET /api/players/{playerId}/active-deck/
```

**Response:**
```json
{
  "id": "deck-uuid",
  "deck_name": "Main Deck",
  "cards": [
    {
      "id": "card-uuid-1",
      "registry_name": "FireballSpell",
      "card_type": "spell",
      "level": 2
    },
    {
      "id": "card-uuid-2",
      "registry_name": "IceWizard",
      "card_type": "unit",
      "level": 1
    },
    ...8 regular cards total...
  ],
  "legend_stone": {
    "id": "legend-uuid",
    "registry_name": "BoulderGuardian",
    "card_type": "legend",
    "level": 3
  },
  "guardian_card": {
    "id": "guardian-uuid",
    "registry_name": "BoulderGuardian",
    "card_type": "guardian",
    "level": 2
  }
}
```

**Implementation Example:**
```python
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import IsAuthenticated
from rest_framework.response import Response
from rest_framework import status
from .models import PlayerDeck, PlayerCard

@api_view(['GET'])
@permission_classes([IsAuthenticated])  # Or use token auth
def get_active_deck(request, player_id):
    """
    Fetch a player's active deck by their UUID
    Called by headless server to get deck data
    """
    try:
        # Get player
        player = Player.objects.get(id=player_id)
        
        # Get active deck
        deck = PlayerDeck.objects.filter(
            player=player,
            is_active=True
        ).first()
        
        if not deck:
            return Response(
                {"error": "No active deck found"},
                status=status.HTTP_404_NOT_FOUND
            )
        
        # Serialize deck
        serializer = DeckSerializer(deck)
        return Response(serializer.data)
        
    except Player.DoesNotExist:
        return Response(
            {"error": "Player not found"},
            status=status.HTTP_404_NOT_FOUND
        )
    except Exception as e:
        return Response(
            {"error": str(e)},
            status=status.HTTP_500_INTERNAL_SERVER_ERROR
        )
```

**URL Routing:** Add to `guardians_backend/players/urls.py`
```python
path('api/players/<str:player_id>/active-deck/', views.get_active_deck, name='get-active-deck'),
```

---

### 2. Matchmaker Service Changes

**Location:** `guardians_backend/players/management/commands/matchmaker.py`

**Update the server spawn command:**

```python
def spawn_game_server(self, player1_id, player2_id, port):
    """
    Spawn headless server with player IDs as arguments
    """
    build_path = self.build_path  # e.g., "C:\builds\game.exe"
    
    command = [
        str(build_path),
        "-server",                    # Headless mode
        f"-player1Id={player1_id}",  # Player 1 UUID
        f"-player2Id={player2_id}",  # Player 2 UUID
        "-nographics",               # Optional: disable graphics
        "-batchmode"                 # Optional: batch mode
    ]
    
    try:
        process = subprocess.Popen(
            command,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE
        )
        
        self.stdout.write(
            self.style.SUCCESS(
                f'✓ Spawned server on port {port} '
                f'for players {player1_id}, {player2_id}'
            )
        )
        
        return process
        
    except Exception as e:
        self.stdout.write(
            self.style.ERROR(f'✗ Failed to spawn server: {e}')
        )
        return None
```

---

### 3. Headless Server Logging

**What the backend should expect to see in server logs:**

```
[NetworkUI] Headless mode detected - starting dedicated server
[NetworkUI] Dedicated server started successfully
[NetworkUI] Player 1 ID: 550e8400-e29b-41d4-a716-446655440000
[NetworkUI] Player 2 ID: 550e8400-e29b-41d4-a716-446655440001
[NetworkUI] Fetching decks from backend for players: 550e8400-e29b-41d4-a716-446655440000, 550e8400-e29b-41d4-a716-446655440001
[NetworkUI] Fetching deck from: http://backend-server/api/players/550e8400-e29b-41d4-a716-446655440000/active-deck/
[NetworkUI] Successfully fetched deck for player 550e8400-e29b-41d4-a716-446655440000
[NetworkUI] Fetching deck from: http://backend-server/api/players/550e8400-e29b-41d4-a716-446655440001/active-deck/
[NetworkUI] Successfully fetched deck for player 550e8400-e29b-41d4-a716-446655440001
[NetworkUI] Successfully loaded both decks from backend
[NetworkUI] LoadGameScene called - gameSceneName: GameArena
[NetworkUI] Scene 'GameArena' is in build settings
[NetworkUI] Loading scene: GameArena
[NetworkUI] Client 1 connected to server
[NetworkUI] Client 2 connected to server
[NetworkUI] GameManager found, starting match...
```

**If there's an error:**
```
[NetworkUI] HTTP error fetching deck for 550e8400-e29b-41d4-a716-446655440000: 404 (Status: 404)
[NetworkUI] Response: {"error": "Player not found"}
```

---

## 🔐 Security Considerations

### 1. Authentication for Headless Server

Since the headless server runs as a background process, you need to handle authentication:

**Option A: Use a special headless token**
```python
# In matchmaker.py
HEADLESS_AUTH_TOKEN = "your-secret-headless-token"

def spawn_game_server(self, player1_id, player2_id, port):
    command = [
        str(build_path),
        "-server",
        f"-player1Id={player1_id}",
        f"-player2Id={player2_id}",
        f"-authToken={HEADLESS_AUTH_TOKEN}"  # Pass secure token
    ]
```

**Option B: Bypass auth for internal requests**
```python
# In views.py
@api_view(['GET'])
def get_active_deck(request, player_id):
    # Check if request from localhost (headless server)
    # or has special authorization header
    
    if request.META.get('REMOTE_ADDR') != '127.0.0.1':
        return Response(
            {"error": "Unauthorized"},
            status=status.HTTP_401_UNAUTHORIZED
        )
```

---

## 🧪 Testing

### Test the new endpoint manually:

```bash
# 1. Get a valid player UUID from your database
python manage.py shell
>>> from players.models import Player
>>> player = Player.objects.first()
>>> print(player.id)  # Copy this UUID

# 2. Test the endpoint
curl -X GET http://localhost:8000/api/players/550e8400-e29b-41d4-a716-446655440000/active-deck/ \
  -H "Authorization: Bearer YOUR_TOKEN"

# 3. Verify response has all card data
```

### Test server spawn:

```bash
# 1. Build the headless executable
# In Unity: File → Build Settings → Build

# 2. Test spawn command manually
C:\builds\game.exe -server -player1Id=550e8400-e29b-41d4-a716-446655440000 -player2Id=550e8400-e29b-41d4-a716-446655440001 -nographics -batchmode

# 3. Check logs for successful deck fetching
# Server should output deck fetch logs
```

---

## 📊 Data Flow Diagram

```
Backend API                 Headless Server               Clients
─────────────              ──────────────────            ───────

                           StartUp
                             │
                    Parse args: player IDs
                             │
                    Start Netcode Server
                             │
                    Fetch Deck 1 ────────→ GET /api/players/{p1}/active-deck/
                             ←─── DeckPayload (cards, legend, guardian)
                             │
                    Fetch Deck 2 ────────→ GET /api/players/{p2}/active-deck/
                             ←─── DeckPayload (cards, legend, guardian)
                             │
                    Store in TempPlayerDecks
                             │
                  Waiting for clients...
                             │
                                             Client 1 connects → IP:7777
                                             Client 2 connects → IP:7777
                             │
                    Both clients connected
                             │
                    Load GameScene
                             │
                    GameManager.InitializeFromDecks(TempPlayerDecks)
                             │
                    Match begins ─────────→ Send game state to clients
```

---

## ⚠️ Common Issues & Solutions

### Issue: "HTTP 404 Player not found"
**Cause:** Player UUID doesn't exist in database
**Solution:** 
- Verify player exists: `Player.objects.get(id=player_id)`
- Check matchmaker is passing correct UUIDs
- Verify player is created before matching

### Issue: "HTTP 401 Unauthorized"
**Cause:** Headless server not authenticated
**Solution:**
- Add auth exception for localhost requests
- Or pass auth token as command line argument
- Or use special headless token with server

### Issue: "Connection timeout - clients not connecting"
**Cause:** Server IP mismatch or firewall
**Solution:**
- Ensure server binding to `0.0.0.0` (done in NetworkUI)
- Check firewall allows port 7777
- Verify clients have correct server IP from matchmaker response

### Issue: "GameManager not found after timeout"
**Cause:** GameScene not loading or GameManager not spawned as NetworkObject
**Solution:**
- Verify GameScene in Build Settings
- Ensure GameManager has NetworkObject component
- Check scene loads successfully

---

## 📝 Summary Checklist

- [ ] Create `/api/players/{playerId}/active-deck/` endpoint
- [ ] Returns DeckPayload with all card data
- [ ] Update matchmaker to spawn with `-player1Id=` `-player2Id=`
- [ ] Test endpoint manually with curl
- [ ] Test server spawn command
- [ ] Set up authentication (localhost bypass or token)
- [ ] Verify logs show successful deck fetching
- [ ] Monitor server startup logs for errors
- [ ] Test with 2 real clients connecting

---

## 🚀 Production Deployment

Before going live:

1. **Build headless executable**
   ```bash
   # Unity: File → Build Settings
   # Select Linux/Windows target
   # Uncheck "Development Build"
   # Disable graphics/UI
   # Build to: C:\builds\game.exe
   ```

2. **Update backend config**
   ```python
   # settings.py or matchmaker.py
   HEADLESS_BUILD_PATH = "/path/to/game.exe"
   HEADLESS_PORT_RANGE = (7777, 8000)  # 24 concurrent matches
   ```

3. **Start matchmaker service**
   ```bash
   python manage.py matchmaker --buildpath "C:\builds\game.exe"
   ```

4. **Test full flow**
   - 2 players login
   - Both click "Find Match"
   - Backend matches them
   - Server spawns with correct player IDs
   - Both clients connect
   - Match starts successfully

---

## 📞 Integration Points

**Files involved:**
- `guardians_backend/players/views.py` - New `get_active_deck` endpoint
- `guardians_backend/players/urls.py` - Add route
- `guardians_backend/players/management/commands/matchmaker.py` - Update spawn command
- `Unity/Assets/Scripts/NetworkUI.cs` - Already implemented ✅

**Data structures:**
- `DeckPayload` - Sent from backend to headless server
- `TempPlayerDecks` - Dictionary storing both player decks
- `GameManager.InitializeFromDecks()` - Receives dict on match start

---

This completes the headless server integration! The backend now controls the entire flow from matchmaking through server spawn and deck delivery.
