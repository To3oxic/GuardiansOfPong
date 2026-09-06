# PlayFab Matchmaking System Implementation Guide

## Architecture Overview

The matchmaking system consists of four main components:

1. **PlayFab Cloud Functions** (Matchmaking.js)
   - Manages player queue
   - Creates matches when 2 players queue
   - Calls VPS API to start game server
   - Tracks match status

2. **VPS Server** (vps_server.py)
   - Listens for API calls to start matches
   - Launches Unity headless server instances
   - Reports server IP/port back to PlayFab

3. **Unity Headless Server** (HeadlessServer.cs)
   - Receives match data via command-line args
   - Starts networking server
   - Reports "ready" to VPS
   - Waits for clients to connect

4. **Unity Clients**
   - Click "Find Match" to queue
   - Poll PlayFab for match status
   - Receive server IP/port from PlayFab
   - Connect to headless server
   - Game starts

## Data Flow

```
Player 1 & 2                  PlayFab                    VPS                  Headless Server
     |                           |                         |                          |
     +-- Click Find Match -----> |                         |                          |
     |                           |                         |                          |
     |                      Queue both players             |                          |
     |                           |                         |                          |
     |  Poll checkMatchStatus()   |                         |                          |
     | <------------------------+ |                         |                          |
     |  status: "starting"        |                         |                          |
     |                           |--- Call /api/start-match |                          |
     |                           |------------------------> |                          |
     |                           |                         |--- Start process ----> |
     |                           |                         |                        |
     |                           |                   <---- Server Started          |
     |                           |<------------ Return IP/Port                     |
     |                           |                         |                        |
     |<-- markMatchReady() ------+                         |                        |
     |  IP & Port provided       |                         |                  Signal Ready
     |                           |                         |                 (via HTTP)
     |                           |                         |                        |
     +-- Connect to IP:Port -------------------------------------------------------+
     |                           |                         |                        |
     +---- Receive Deck, Load Game, Start Match -----------------------------------+
```

## Implementation Checklist

### 1. Deploy VPS Server
- [ ] Copy `vps_server.py` to Contabo VPS
- [ ] Install Python dependencies: `pip install flask`
- [ ] Set environment variables (game build path, VPS IP)
- [ ] Run as systemd service or screen session
- [ ] Test `/health` endpoint

### 2. Upload PlayFab Cloud Functions
- [ ] Upload Matchmaking.js to PlayFab
- [ ] Set Title Data `vps_url` to your VPS IP:5000
- [ ] Set Title Data `vps_api_key` for security
- [ ] Test `startMatchmaking` function

### 3. Client Implementation
- [ ] Create MatchmakingUIManager.cs for UI states
- [ ] Add "Find Match" button to main menu
- [ ] Implement polling loop for match status
- [ ] Update NetworkUI to use received server IP/port

### 4. Headless Server
- [ ] HeadlessServer.cs already parses command-line args
- [ ] Verify it handles player IDs and decks from VPS
- [ ] Test locally: `-server -player1Id=test1 -player2Id=test2`

## Key Files

| File | Purpose |
|------|---------|
| `vps_server.py` | VPS server manager - starts game instances |
| `Matchmaking.js` | PlayFab cloud functions for queueing |
| `MatchmakingUIManager.cs` | Unity client - UI states during matchmaking |
| `HeadlessServer.cs` | Server-side - already adapted |
| `NetworkUI.cs` | Networking - needs update to use VPS IP |

## PlayFab Cloud Functions Reference

### startMatchmaking()
**Called by:** Client when clicking "Find Match"
**Returns:**
```json
{
  "success": true,
  "queued": true,
  "message": "Added to matchmaking queue",
  "playersInQueue": 2,
  "matchId": "match_123456"  // If match found immediately
}
```

### checkMatchStatus()
**Called by:** Client polling loop every 1 second
**Returns:**
```json
{
  "success": true,
  "matched": true,
  "matchId": "match_123456",
  "status": "ready",
  "serverIp": "YOUR_VPS_IP",
  "serverPort": 7777,
  "opponent": "opponent_player_id"
}
```

### cancelMatchmaking()
**Called by:** Client when canceling queue
**Returns:**
```json
{
  "success": true,
  "message": "Removed from matchmaking queue"
}
```

### markMatchReady()
**Called by:** VPS server after starting game
**Parameters:**
```json
{
  "matchId": "match_123456",
  "serverIp": "YOUR_VPS_IP",
  "serverPort": 7777,
  "apiKey": "YOUR_API_KEY"
}
```

## VPS API Reference

### POST /api/start-match
**Request:**
```json
{
  "match_id": "match_123456",
  "player1_id": "playfab_id_1",
  "player2_id": "playfab_id_2",
  "player1_deck": {
    "cards": [{"id": "c1", "level": 5}, ...],
    "guardian_card": {"id": "g2", "level": 5},
    "legend_stone": {"id": "l1", "level": 5}
  },
  "player2_deck": {...}
}
```

**Response:**
```json
{
  "success": true,
  "match_id": "match_123456",
  "ip_address": "YOUR_VPS_IP",
  "port": 7777,
  "status": "starting"
}
```

### GET /api/match/<match_id>/status
**Response:**
```json
{
  "success": true,
  "match_id": "match_123456",
  "status": "ready",
  "ip_address": "YOUR_VPS_IP",
  "port": 7777
}
```

### GET /api/matches
**Response:**
```json
{
  "success": true,
  "matches": [
    {
      "match_id": "match_123456",
      "player1_id": "player_1",
      "player2_id": "player_2",
      "status": "ready",
      "ip_address": "YOUR_VPS_IP",
      "port": 7777,
      "created_at": "2024-01-08T10:00:00Z"
    }
  ],
  "count": 1
}
```

## Configuration

### PlayFab Title Data
Set these in PlayFab Console under Title Data:

```
vps_url: http://YOUR_VPS_IP:5000
vps_api_key: YOUR_SECRET_KEY
```

### Environment Variables (VPS)
```bash
GAME_BUILD_PATH=/home/game/builds/Game.x86_64
VPS_PUBLIC_IP=YOUR_VPS_PUBLIC_IP
BASE_PORT=7777
```

## Testing Workflow

### 1. Local Testing (Single Machine)
```bash
# Terminal 1: Run VPS server
python3 vps_server.py

# Terminal 2: Run game in server mode
Game.x86_64 -server -batchmode -nographics

# Terminal 3: Run two game clients
# Client 1
Game.x86_64 -player1Id=test1

# Client 2
Game.x86_64 -player2Id=test2

# Test API
curl -X POST http://localhost:5000/api/start-match \
  -H "Content-Type: application/json" \
  -d '{"match_id":"test","player1_id":"p1","player2_id":"p2","player1_deck":{},"player2_deck":{}}'
```

### 2. VPS Testing
```bash
# SSH to VPS
ssh root@YOUR_VPS_IP

# Check server status
systemctl status game-server

# Monitor logs
journalctl -u game-server -f

# Test connectivity
curl http://YOUR_VPS_IP:5000/health
```

### 3. Full E2E Testing
1. Build game for Linux headless
2. Deploy to VPS
3. Upload Cloud Functions to PlayFab
4. In Unity Editor, click "Find Match"
5. Check PlayFab and VPS logs
6. Verify server started on VPS
7. Verify client receives IP/port
8. Verify client connects successfully

## Troubleshooting

### Server Won't Start
- Check VPS logs: `tail -f /home/game/vps_server.log`
- Verify game build exists and is executable
- Check ports are open: `netstat -tlnp | grep 7777`

### Clients Won't Connect
- Verify server IP in response matches VPS public IP
- Check firewall rules on VPS
- Test from client: `telnet YOUR_VPS_IP 7777`

### Match Status Returns "starting" forever
- Check if game process is actually running on VPS
- Check if game process is waiting for something
- Check game output logs on VPS

### VPS API Returns 500 Error
- Check VPS server logs for Python exceptions
- Verify JSON payload is valid
- Verify all required fields are present

## Performance Tuning

### Max Concurrent Games
- Default: 100 games (ports 7777-7877)
- Each game ~500MB-1GB RAM
- Adjust BASE_PORT and port range in vps_server.py

### Matchmaking Speed
- Current: Poll every 1 second
- Faster: Poll every 0.5 seconds (more network traffic)
- Better: Implement callback system (more complex)

## Security Considerations

1. **API Key**: Use vps_api_key to validate requests
2. **Rate Limiting**: Add rate limits to PlayFab functions
3. **Validation**: Validate all input in cloud functions
4. **HTTPS**: Use HTTPS in production
5. **Deck Validation**: PlayFab validates deck ownership
6. **Process Limits**: Kill servers after match ends

## Next Steps

1. ✅ Create VPS server (vps_server.py)
2. ✅ Create Cloud Functions (Matchmaking.js)
3. ⏳ Create MatchmakingUIManager.cs
4. ⏳ Add "Find Match" button to MainMenu
5. ⏳ Implement polling loop
6. ⏳ Update NetworkUI to use VPS IP
7. ⏳ Test full E2E flow
