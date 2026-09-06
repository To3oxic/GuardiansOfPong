# VPS Game Server Setup Guide

This guide explains how to deploy the VPS server on Contabo VPS to manage game server instances.

## Prerequisites

- Contabo VPS with Ubuntu 22.04 LTS or similar
- Python 3.9+
- Flask
- The compiled Unity game build for Linux

## Installation Steps

### 1. Connect to VPS

```bash
ssh root@YOUR_VPS_IP
```

### 2. Install Dependencies

```bash
# Update system
apt-get update && apt-get upgrade -y

# Install Python and pip
apt-get install -y python3 python3-pip python3-venv

# Install Flask
pip3 install flask

# Install optional: screen or systemd for process management
apt-get install -y screen
```

### 3. Create Game Directory Structure

```bash
# Create game directory
mkdir -p /home/game/builds
mkdir -p /home/game/logs
mkdir -p /home/game/data

# Copy the compiled game build to the VPS
# From your local machine:
# scp -r path/to/Game.x86_64 root@YOUR_VPS_IP:/home/game/builds/

# Or if you compiled on the VPS:
# cp /path/to/project/build/Game.x86_64 /home/game/builds/

# Make executable
chmod +x /home/game/builds/Game.x86_64
```

### 4. Create VPS Server Script

Copy `vps_server.py` to the VPS:

```bash
# From your local machine:
scp vps_server.py root@YOUR_VPS_IP:/home/game/

# On VPS, make executable
chmod +x /home/game/vps_server.py
```

### 5. Set Environment Variables

Create `/home/game/.env`:

```bash
export GAME_BUILD_PATH=/home/game/builds/Game.x86_64
export VPS_PUBLIC_IP=YOUR_VPS_PUBLIC_IP  # Get this from Contabo control panel
export BASE_PORT=7777
export BACKEND_URL=http://localhost:8000
export PLAYFAB_API_URL=http://localhost:5000
```

### 6. Run the Server

#### Option A: Manual Testing (Foreground)

```bash
cd /home/game
source .env
python3 vps_server.py
```

#### Option B: Run with Screen (Long-running)

```bash
# Start in a screen session
screen -S game-server -d -m python3 /home/game/vps_server.py

# Attach to the session later
screen -r game-server

# Detach (leave running)
# Press Ctrl+A then D
```

#### Option C: Run with Systemd (Recommended for Production)

Create `/etc/systemd/system/game-server.service`:

```ini
[Unit]
Description=Game Server Manager
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/home/game
EnvironmentFile=/home/game/.env
ExecStart=/usr/bin/python3 /home/game/vps_server.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Then:

```bash
systemctl daemon-reload
systemctl enable game-server
systemctl start game-server
systemctl status game-server
```

### 7. Firewall Configuration

Allow the necessary ports:

```bash
# Allow API server port
ufw allow 5000/tcp

# Allow game server port range (7777-7877 for up to 100 concurrent games)
ufw allow 7777:7877/tcp

# Check rules
ufw status
```

### 8. Test the Server

```bash
# Health check
curl http://localhost:5000/health

# Should return: {"status":"healthy","timestamp":"..."}
```

## API Reference

### Start a Match

```bash
POST /api/start-match

{
  "match_id": "match_12345",
  "player1_id": "player_1",
  "player2_id": "player_2",
  "player1_deck": {
    "cards": [{"id": "c1", "level": 5}, ...],
    "guardian_card": {"id": "g1", "level": 5},
    "legend_stone": {"id": "l1", "level": 5}
  },
  "player2_deck": {...}
}

# Response:
{
  "success": true,
  "match_id": "match_12345",
  "ip_address": "YOUR_VPS_PUBLIC_IP",
  "port": 7777,
  "status": "starting"
}
```

### Check Match Status

```bash
GET /api/match/match_12345/status

# Response:
{
  "success": true,
  "match_id": "match_12345",
  "status": "ready",
  "ip_address": "YOUR_VPS_PUBLIC_IP",
  "port": 7777
}
```

### List All Matches

```bash
GET /api/matches

# Response:
{
  "success": true,
  "matches": [...],
  "count": 5
}
```

### Stop a Match

```bash
POST /api/match/match_12345/stop

# Response:
{
  "success": true,
  "message": "Match match_12345 stopped"
}
```

### Cleanup

```bash
POST /api/cleanup

# Response:
{
  "success": true,
  "cleaned_matches": ["match_123", "match_456"]
}
```

## Monitoring

### View Logs

```bash
# If using systemd:
journalctl -u game-server -f

# Or check the log file:
tail -f /home/game/vps_server.log
```

### Monitor Resource Usage

```bash
# CPU and Memory
top

# Network connections
netstat -tlnp | grep 7777
```

## Troubleshooting

### Server Won't Start

1. Check that the game build exists:
   ```bash
   ls -la /home/game/builds/Game.x86_64
   ```

2. Check Python path:
   ```bash
   which python3
   ```

3. Check logs:
   ```bash
   tail -20 /home/game/vps_server.log
   ```

### Server Starts but Clients Can't Connect

1. Verify public IP in response matches your VPS public IP
2. Check firewall rules are open
3. Check from local machine:
   ```bash
   telnet YOUR_VPS_PUBLIC_IP 7777
   ```

### Game Process Crashes

1. Check game logs:
   ```bash
   tail -f /home/game/vps_server.log
   ```

2. Try running game manually to see errors:
   ```bash
   /home/game/builds/Game.x86_64 -server -batchmode -nographics -port=7777
   ```

## Scaling Considerations

- **Concurrent Games**: Each game takes ~500MB-1GB RAM. Adjust BASE_PORT and port range accordingly.
- **CPU**: For headless servers, count on 1 core per game approximately.
- **Network**: Each connection uses ~50KB/s average. Plan bandwidth accordingly.

For multiple VPS instances, use a load balancer or have PlayFab choose which VPS to use based on load.

## Security Notes

- Restrict API access by IP if possible
- Use HTTPS in production (add SSL certificate)
- Validate all input data before starting servers
- Consider API key authentication for /api/start-match endpoint
