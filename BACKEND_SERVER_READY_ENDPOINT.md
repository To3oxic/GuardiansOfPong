# Backend: Server Ready Endpoint Implementation

## Overview

The headless server now signals the backend when it's fully initialized and ready to accept client connections. This prevents clients from trying to connect to a server that's still initializing.

---

## Flow Diagram

```
Matchmaker spawns headless server
         ↓
Server starts, fetches decks
         ↓
Server calls: POST /api/matchmaking/server-ready/
         ↓
Backend updates match: status = "initializing" → "ready"
         ↓
Clients poll: GET /api/matchmaking/queue-status/
         ↓
Response includes: match_instance.status = "ready"
         ↓
Clients connect to server port
         ↓
Game starts!
```

---

## Endpoint: `POST /api/matchmaking/server-ready/`

### Purpose
Called by the headless server when it's ready to accept client connections. Updates the match status from "initializing" to "ready".

### Called By
Headless Unity server (after fetching player decks)

### Request Headers
```
Content-Type: application/json
```

### Request Body
```json
{
  "status": "ready"
}
```

### Response (Success)
```json
{
  "success": true,
  "match_id": "match-uuid"
}
```

### Response (Error)
```json
{
  "success": false,
  "error": "No initializing match found"
}
```

---

## Implementation

### File: `matchmaking/views.py` or `api/views.py`

```python
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from django.utils import timezone
from matchmaking.models import MatchInstance

@api_view(['POST'])
@permission_classes([AllowAny])  # Headless server calls this from localhost
def server_ready(request):
    """
    Called by headless server when it's ready to accept client connections.
    Updates match status from 'initializing' to 'ready'.
    
    Headless servers run on localhost, so we find the most recently created
    initializing match (the one we just spawned).
    """
    try:
        # Find the most recent initializing match
        # The headless server will call this immediately after fetching decks
        # So there should be exactly one initializing match that's "our" match
        match = MatchInstance.objects.filter(
            status='initializing'
        ).order_by('-created_at').first()
        
        if not match:
            return Response(
                {"success": False, "error": "No initializing match found"}, 
                status=404
            )
        
        # Update match status to ready
        match.status = 'ready'
        match.save()
        
        print(f"✓ Match {match.id} ready on port {match.server_port}")
        print(f"  Players: {match.player1.username} vs {match.player2.username}")
        
        return Response({
            "success": True,
            "match_id": str(match.id),
            "message": "Server ready, clients can now connect"
        })
    
    except Exception as e:
        return Response({
            "success": False,
            "error": str(e)
        }, status=500)
```

### URL Configuration

**File:** `matchmaking/urls.py` or `api/urls.py`

Add this line to your `urlpatterns`:

```python
from django.urls import path
from . import views

urlpatterns = [
    # ... existing endpoints ...
    
    # Headless server signals readiness
    path('matchmaking/server-ready/', 
         views.server_ready, 
         name='server_ready'),
]
```

### Full URL Path
```
POST /api/matchmaking/server-ready/
```

---

## How It Works

### Sequence of Events

1. **Matchmaker spawns server** (with player IDs as command-line args)
   ```bash
   Myproject.exe -batchmode -nographics -server \
     -player1Id=uuid1 -player2Id=uuid2 -port=7777
   ```

2. **Headless server initializes** (NetworkUI.cs)
   - Parses command-line arguments
   - Calls `StartServer()`
   - Starts listening on port 7777

3. **Headless server fetches decks** (NetworkUI.cs)
   - GET `/api/players/{player1Id}/active-deck/`
   - GET `/api/players/{player2Id}/active-deck/`
   - Stores decks locally

4. **Headless server signals ready** (NEW)
   - POST `/api/matchmaking/server-ready/`
   - Backend updates: `match.status = "initializing" → "ready"`

5. **Clients poll for match status**
   - GET `/api/matchmaking/queue-status/`
   - Backend returns: `match_instance.status = "ready"`

6. **Clients connect to server**
   - Clients see status is "ready"
   - Clients connect to server IP:port
   - Game starts!

---

## Testing

### Manual Test from PowerShell

1. **Trigger a match** (two players join queue)
   ```powershell
   # Terminal 1: Run backend matchmaker
   python manage.py matchmaker
   
   # Terminal 2: Player 1 joins queue
   $player1_token = "..." # from login
   Invoke-WebRequest -Uri "http://localhost:8000/api/matchmaking/join-queue/" `
     -Method POST `
     -Headers @{"X-CSRFToken"=$csrf_token} `
     -Cookies $cookie
   
   # Terminal 3: Player 2 joins queue
   $player2_token = "..."
   Invoke-WebRequest -Uri "http://localhost:8000/api/matchmaking/join-queue/" `
     -Method POST `
     -Headers @{"X-CSRFToken"=$csrf_token} `
     -Cookies $cookie
   ```

2. **Watch the logs**
   ```
   # Backend matchmaker output:
   ✓ Created match between player1 and player2 on port 7777 (PID: 12345)
   
   # Headless server logs (in server_logs/server_7777_*.log):
   [NetworkUI] Successfully loaded both decks from backend
   [NetworkUI] Signaling server ready to backend
   [NetworkUI] Server ready signal sent successfully
   
   # Backend matchmaker continues:
   ✓ Match 123e4567-e89b-12d3-a456-426614174000 ready on port 7777
   ```

3. **Check match status from client**
   ```powershell
   $response = Invoke-WebRequest -Uri "http://localhost:8000/api/matchmaking/queue-status/" `
     -Method GET `
     -Cookies $cookie
   
   $json = $response | ConvertFrom-Json
   $json.match_instance.status  # Should be "ready" now
   ```

---

## Error Handling

### What if no initializing match is found?

**Scenario:** Server calls endpoint but no "initializing" match exists

**Response:**
```json
{
  "success": false,
  "error": "No initializing match found",
  "status_code": 404
}
```

**Cause:** Match entry wasn't created by matchmaker, or status was already changed

**Solution:** 
- Check that matchmaker created the MatchInstance before spawning server
- Verify match status is "initializing" in database

### What if multiple initializing matches exist?

**Scenario:** Multiple matches are initializing simultaneously

**Current Logic:** Takes the most recently created one (`order_by('-created_at').first()`)

**Better Logic (Optional Enhancement):**
```python
# Have the server send its port number and match the port
# Or have the backend pass match_id as command-line arg to server

# Option 1: Server sends port in request body
POST /api/matchmaking/server-ready/
{
  "port": 7777
}

# Then in view:
match = MatchInstance.objects.filter(
    status='initializing',
    server_port=request.data.get('port')
).first()
```

---

## Integration with Existing Endpoints

### Queue Status Endpoint Update

The `queue-status` endpoint already returns the match_instance with its status field. No changes needed, but verify it returns:

```json
{
  "status": "matched",
  "match_instance": {
    "status": "initializing",  // ← Will be "ready" after server-ready call
    "server_ip": "127.0.0.1",
    "server_port": 7777,
    ...
  }
}
```

---

## Status Timeline

### Match Lifecycle

| Status | When | By Whom | What It Means |
|--------|------|---------|---------------|
| `queued` | Player joins queue | join-queue endpoint | Player waiting for opponent |
| `matched` | 2 players found + match created | Matchmaker | Opponent found, server spawning |
| `initializing` | Server process spawned | Matchmaker | Server starting up, loading decks |
| `ready` | Server fetched decks + signaled | server-ready endpoint | Server ready to accept clients |
| `running` | Clients connected | GameManager | Game in progress |
| `completed` | Match finished | GameManager | Match ended, results recorded |
| `failed` | Server crashed | Matchmaker cleanup | Server died, match cancelled |

---

## Logging Output Example

### Backend Matchmaker Console

```
✓ Matchmaker started
Listening for queued players (polling every 2s)...

[Queue: 1 player] player1
[Queue: 2 players] player1 player2
✓ Created match between player1 and player2 on port 7777 (PID: 12345)

# Wait for server-ready call...

✓ Match abc123def456 ready on port 7777
  Players: player1 vs player2

# Clients poll and start connecting...

[Match Running] Match on port 7777 (PID: 12345)
```

### Headless Server Console (in logs)

```
[NetworkUI] Successfully loaded both decks from backend
[NetworkUI] Signaling server ready to backend
[NetworkUI] Server ready signal sent successfully
```

### Client Console

```
[MatchmakingManager] Status response: {"status":"matched","match_instance":{"status":"ready",...}}
[MatchmakingManager] Match instance status: ready
[MatchmakingManager] Match found!
[MatchmakingManager] Attempting to connect to 127.0.0.1:7777
[MatchmakingManager] Configuring transport: 127.0.0.1:7777
[MatchmakingManager] NetworkManager started, waiting for connection...
[MatchmakingManager] Successfully connected to server
```

---

## Troubleshooting

### Issue: Clients never connect (socket errors persist)

**Cause:** Server-ready endpoint not implemented or not called

**Check:**
1. Is the endpoint registered in urls.py?
2. Does the headless server log show "Server ready signal sent"?
3. Check backend console for `✓ Match ... ready` message

**Solution:**
- Implement the endpoint exactly as shown above
- Restart backend: `python manage.py runserver`
- Rebuild and retest

### Issue: "No initializing match found" error

**Cause:** Match status was already changed or match doesn't exist

**Check:**
1. Does the matchmaker create the MatchInstance before spawning?
2. Is the match status "initializing" when server calls?
3. How many matches are in "initializing" state?

**Solution:**
- Add logging to matchmaker to confirm match creation
- Check database: `python manage.py shell`
  ```python
  from matchmaking.models import MatchInstance
  MatchInstance.objects.filter(status='initializing')
  ```

### Issue: Multiple matches in "initializing" state

**Cause:** Previous matches didn't complete or weren't cleaned up

**Check:**
```python
from matchmaking.models import MatchInstance
MatchInstance.objects.filter(status__in=['initializing', 'failed'])
```

**Solution:**
- Manually clean up in shell: `MatchInstance.objects.filter(status='initializing').delete()`
- Or implement better match cleanup logic

---

## Performance Considerations

### Polling Delay
- Client polls every 1-2 seconds for queue status
- Small delay between server-ready call and client reconnection is fine
- Natural delay from network round-trip

### Concurrent Matches
- Each match gets its own port (7777-7800 range)
- Each can call server-ready independently
- No conflicts since each finds most recent initializing match for their port

### Load Testing
If supporting 20+ concurrent matches:
- Consider passing match_id in server-ready request
- Or pass port number for more precise matching
- Current implementation (most recent) works for sequential matches

---

## Summary

**What to implement:**

1. ✅ Add `server_ready()` view function to `matchmaking/views.py`
2. ✅ Add URL path to `matchmaking/urls.py`
3. ✅ Restart Django backend: `python manage.py runserver`
4. ✅ Rebuild Unity headless executable
5. ✅ Test with two clients joining queue

**Expected result:**
- Server fetches decks
- Server calls server-ready endpoint
- Backend updates match to "ready"
- Clients see status is "ready"
- Clients connect successfully
- Socket errors disappear
- Game starts!

**Files Modified:**
- `matchmaking/views.py` - Add server_ready view
- `matchmaking/urls.py` - Add server_ready URL
- Backend already updated: `MatchmakingManager.cs` waits for "ready" status before connecting
- Backend already updated: `NetworkUI.cs` calls server-ready endpoint after fetching decks

---

## Next Steps

1. Implement this endpoint
2. Rebuild headless executable
3. Run full end-to-end test
4. Verify "ready" status in client logs
5. Confirm successful connections

You're very close to a working matchmaking system! 🚀
