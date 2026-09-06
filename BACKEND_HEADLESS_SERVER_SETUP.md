# Backend: Headless Server Integration Guide

## Overview

The backend needs to support headless Unity servers spawning for multiplayer matches. This guide covers all required endpoints, permissions, and database updates.

---

## 1. Make Player Deck Endpoint Public (Localhost)

### Endpoint: `GET /api/players/{player_id}/active-deck/`

**Current Issue:** 
- Headless server cannot fetch player decks (403 Forbidden)
- Endpoint requires authentication
- Server process crashes when deck fetch fails

**Solution:**
Allow unauthenticated access from localhost only.

### Implementation

**File:** `players/views.py` or `api/views.py`

```python
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import AllowAny
from rest_framework.response import Response
from django.http import JsonResponse
from django.core.serializers.json import DjangoJSONEncoder
import json

@api_view(['GET'])
@permission_classes([AllowAny])  # Allow unauthenticated access
def get_player_active_deck(request, player_id):
    """
    Fetch a player's active deck.
    
    Unauthenticated access allowed ONLY from localhost (127.0.0.1, ::1)
    Used by headless servers to fetch match decks.
    """
    
    # Allow localhost access (headless servers)
    client_ip = request.META.get('REMOTE_ADDR', '')
    is_localhost = client_ip in ['127.0.0.1', '::1', 'localhost']
    
    # If not localhost, require authentication
    if not is_localhost and not request.user.is_authenticated:
        return Response({"error": "Unauthorized"}, status=403)
    
    try:
        player = CustomUser.objects.get(id=player_id)
    except CustomUser.DoesNotExist:
        return Response({"error": "Player not found"}, status=404)
    
    # Get active deck
    try:
        deck = SavedDeck.objects.get(player=player, is_active=True)
    except SavedDeck.DoesNotExist:
        return Response({"error": "No active deck found"}, status=404)
    
    # Serialize deck with all card data
    deck_data = {
        "id": str(deck.id),
        "deck_name": deck.deck_name,
        "is_active": deck.is_active,
        "card_slot_1": serialize_card_slot(deck.card_slot_1),
        "card_slot_2": serialize_card_slot(deck.card_slot_2),
        "card_slot_3": serialize_card_slot(deck.card_slot_3),
        "card_slot_4": serialize_card_slot(deck.card_slot_4),
        "card_slot_5": serialize_card_slot(deck.card_slot_5),
        "card_slot_6": serialize_card_slot(deck.card_slot_6),
        "card_slot_7": serialize_card_slot(deck.card_slot_7),
        "card_slot_8": serialize_card_slot(deck.card_slot_8),
        "legend_stone": serialize_card_slot(deck.legend_stone),
        "guardian_card": serialize_card_slot(deck.guardian_card),
        "created_at": deck.created_at.isoformat(),
        "updated_at": deck.updated_at.isoformat(),
    }
    
    return Response(deck_data, status=200)


def serialize_card_slot(slot):
    """Serialize a CardSlot to JSON."""
    if not slot:
        return None
    
    return {
        "card": {
            "id": str(slot.card.id),
            "name": slot.card.name,
            "registry_name": slot.card.registry_name,
            "description": slot.card.description,
            "card_type": slot.card.card_type,
            "rarity": slot.card.rarity,
            "max_level": slot.card.max_level,
        },
        "level": slot.level,
        "copies": slot.copies,
        "acquired_at": slot.acquired_at.isoformat() if slot.acquired_at else None,
    }
```

### URL Configuration

**File:** `players/urls.py` or `api/urls.py`

```python
from django.urls import path
from . import views

urlpatterns = [
    # ... existing endpoints ...
    
    # Headless server deck fetch (public for localhost)
    path('players/<uuid:player_id>/active-deck/', 
         views.get_player_active_deck, 
         name='get_player_active_deck'),
]
```

### Testing the Endpoint

```powershell
# Test from localhost (should work - 200)
curl -X GET "http://localhost:8000/api/players/550e8400-e29b-41d4-a716-446655440000/active-deck/"

# Response should include all card data
{
  "id": "deck-uuid",
  "deck_name": "My First Deck",
  "is_active": true,
  "card_slot_1": {
    "card": {
      "id": "card-uuid",
      "name": "ToxinBall",
      "registry_name": "ToxinBall",
      ...
    },
    "level": 1,
    "copies": 3,
    ...
  },
  ...
}
```

---

## 2. Update Matchmaking Endpoints

### Endpoint: `POST /api/matchmaking/join-queue/`

**Current Status:** ✅ Already implemented and working

**Verify it returns:**
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

### Endpoint: `GET /api/matchmaking/queue-status/`

**Current Status:** ⚠️ Needs update to include nested match_instance

**Should return (when matched):**
```json
{
  "status": "matched",
  "queue_position": null,
  "match_instance": {
    "id": "match-uuid",
    "player1_username": "testplayer",
    "player2_username": "testplayer2",
    "player1_deck": {
      "id": "deck-uuid",
      "deck_name": "My First Deck",
      "is_active": true,
      "card_slot_1": { ... },
      ...
    },
    "player2_deck": {
      "id": "deck-uuid",
      "deck_name": "My First Deck",
      ...
    },
    "server_ip": "127.0.0.1",
    "server_port": 7777,
    "server_pid": 12345,
    "status": "initializing",
    "winner_username": null,
    "created_at": "2025-11-30T14:02:48Z",
    "started_at": null,
    "ended_at": null
  }
}
```

**Implementation:**

```python
@api_view(['GET'])
@permission_classes([IsAuthenticated])
def queue_status(request):
    """
    Check player's queue status.
    
    Returns:
    - "queued": Player waiting for opponent
    - "matched": Match found, server spawned, ready to connect
    - "none": Player not in queue
    """
    
    player = request.user
    
    # Check for active queue entry
    queue_entry = MatchQueue.objects.filter(
        player=player, 
        status__in=['queued', 'matched']
    ).first()
    
    if not queue_entry:
        return Response({
            "status": "none",
            "queue_position": None,
            "match_instance": None
        })
    
    if queue_entry.status == 'queued':
        return Response({
            "status": "queued",
            "queue_position": get_queue_position(queue_entry),
            "match_instance": None
        })
    
    # Status is 'matched' - fetch match instance
    match = MatchInstance.objects.filter(
        status__in=['initializing', 'running', 'completed'],
            player1=player
        ) | MatchInstance.objects.filter(
            status__in=['initializing', 'running', 'completed'],
            player2=player
        )
    ).first()
    
    if not match:
        return Response({
            "status": "none",
            "queue_position": None,
            "match_instance": None
        })
    
    # Serialize match instance with both decks
    match_data = {
        "id": str(match.id),
        "player1_username": match.player1.username,
        "player2_username": match.player2.username,
        "player1_deck": serialize_full_deck(match.player1_deck),
        "player2_deck": serialize_full_deck(match.player2_deck),
        "server_ip": match.server_ip,
        "server_port": match.server_port,
        "server_pid": match.server_pid,
        "status": match.status,
        "winner_username": match.winner.username if match.winner else None,
        "created_at": match.created_at.isoformat(),
        "started_at": match.started_at.isoformat() if match.started_at else None,
        "ended_at": match.ended_at.isoformat() if match.ended_at else None,
    }
    
    return Response({
        "status": "matched",
        "queue_position": None,
        "match_instance": match_data
    })


def serialize_full_deck(deck):
    """Serialize full deck with all card slots."""
    return {
        "id": str(deck.id),
        "deck_name": deck.deck_name,
        "is_active": deck.is_active,
        "card_slot_1": serialize_card_slot(deck.card_slot_1),
        "card_slot_2": serialize_card_slot(deck.card_slot_2),
        "card_slot_3": serialize_card_slot(deck.card_slot_3),
        "card_slot_4": serialize_card_slot(deck.card_slot_4),
        "card_slot_5": serialize_card_slot(deck.card_slot_5),
        "card_slot_6": serialize_card_slot(deck.card_slot_6),
        "card_slot_7": serialize_card_slot(deck.card_slot_7),
        "card_slot_8": serialize_card_slot(deck.card_slot_8),
        "legend_stone": serialize_card_slot(deck.legend_stone),
        "guardian_card": serialize_card_slot(deck.guardian_card),
        "created_at": deck.created_at.isoformat(),
        "updated_at": deck.updated_at.isoformat(),
    }
```

---

### Endpoint: `POST /api/matchmaking/leave-queue/`

**Current Status:** ✅ Already implemented

---

## 3. Database Models - Verify Structure

### MatchQueue Model

```python
from django.db import models
from django.utils import timezone
import uuid

class MatchQueue(models.Model):
    STATUS_CHOICES = [
        ('queued', 'Queued'),
        ('matched', 'Matched'),
        ('cancelled', 'Cancelled'),
    ]
    
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    player = models.ForeignKey(CustomUser, on_delete=models.CASCADE)
    deck = models.ForeignKey(SavedDeck, on_delete=models.CASCADE)
    status = models.CharField(max_length=20, choices=STATUS_CHOICES, default='queued')
    
    joined_at = models.DateTimeField(auto_now_add=True)
    matched_at = models.DateTimeField(null=True, blank=True)
    
    class Meta:
        ordering = ['joined_at']
    
    def __str__(self):
        return f"{self.player.username} - {self.status}"
```

### MatchInstance Model

```python
class MatchInstance(models.Model):
    STATUS_CHOICES = [
        ('initializing', 'Initializing'),
        ('running', 'Running'),
        ('completed', 'Completed'),
        ('failed', 'Failed'),
    ]
    
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    
    player1 = models.ForeignKey(CustomUser, on_delete=models.CASCADE, related_name='matches_as_p1')
    player2 = models.ForeignKey(CustomUser, on_delete=models.CASCADE, related_name='matches_as_p2')
    
    player1_deck = models.ForeignKey(SavedDeck, on_delete=models.CASCADE, related_name='matches_p1')
    player2_deck = models.ForeignKey(SavedDeck, on_delete=models.CASCADE, related_name='matches_p2')
    
    server_ip = models.CharField(max_length=15)  # e.g., "127.0.0.1"
    server_port = models.IntegerField()          # e.g., 7777
    server_pid = models.IntegerField(null=True)  # Process ID
    
    status = models.CharField(max_length=20, choices=STATUS_CHOICES, default='initializing')
    winner = models.ForeignKey(CustomUser, on_delete=models.SET_NULL, null=True, blank=True, related_name='wins')
    
    created_at = models.DateTimeField(auto_now_add=True)
    started_at = models.DateTimeField(null=True, blank=True)
    ended_at = models.DateTimeField(null=True, blank=True)
    
    def __str__(self):
        return f"{self.player1.username} vs {self.player2.username} (Port {self.server_port})"
```

---

## 4. Settings Configuration

### File: `settings.py`

```python
# CSRF Configuration for matchmaking and headless servers
CSRF_TRUSTED_ORIGINS = [
    'http://localhost:3000',      # Local development
    'http://127.0.0.1:8000',      # Local backend
    'http://localhost:8000',       # Local backend
]

# Allow cookies for headless server communication
CSRF_COOKIE_HTTPONLY = False  # Allow JavaScript/headless servers to access
CSRF_USE_SESSIONS = False     # Use cookie-based CSRF tokens

# CORS Configuration (if using django-cors-headers)
CORS_ALLOWED_ORIGINS = [
    "http://localhost:3000",
    "http://127.0.0.1:8000",
    "http://localhost:8000",
]

CORS_ALLOW_CREDENTIALS = True
```

---

## 5. Matchmaker Service (Python Script)

### File: `management/commands/matchmaker.py`

```python
from django.core.management.base import BaseCommand
from django.utils import timezone
from matchmaking.models import MatchQueue, MatchInstance
from players.models import CustomUser, SavedDeck
import subprocess
import time
import os
import psutil
from datetime import datetime

class Command(BaseCommand):
    help = 'Continuously monitors queue and spawns headless servers for matches'
    
    BASE_PORT = 7777
    MAX_SERVERS = 24
    MAX_PORT = 7801
    POLLING_INTERVAL = 2  # Check every 2 seconds
    HEADLESS_SERVER_PATH = r"C:\Users\ahmad\My project\testbuild\Myproject.exe"
    
    active_servers = {}  # { port: { 'process': Process, 'match_id': uuid } }
    
    def handle(self, *args, **options):
        self.stdout.write(self.style.SUCCESS('✓ Matchmaker started'))
        self.stdout.write('Listening for queued players (polling every {}s)...'.format(self.POLLING_INTERVAL))
        
        try:
            while True:
                self.check_for_matches()
                self.monitor_active_servers()
                time.sleep(self.POLLING_INTERVAL)
        except KeyboardInterrupt:
            self.stdout.write(self.style.WARNING('\nShutting down all servers...'))
            self.cleanup_all_servers()
            self.stdout.write(self.style.SUCCESS('✓ Matchmaker stopped'))
    
    def check_for_matches(self):
        """Check for pairs of queued players and spawn servers."""
        queued = MatchQueue.objects.filter(status='queued').order_by('joined_at')
        
        if queued.count() > 0:
            self.stdout.write(f"[Queue: {queued.count()} player(s)]", ending=' ')
            for q in queued:
                self.stdout.write(f"{q.player.username}", ending=' ')
            self.stdout.write()
        
        # Check for pairs (at least 2 queued players)
        while queued.count() >= 2:
            player1_queue = queued[0]
            player2_queue = queued[1]
            
            # Create match
            self.spawn_match(player1_queue, player2_queue)
            
            # Refresh query
            queued = MatchQueue.objects.filter(status='queued').order_by('joined_at')
    
    def spawn_match(self, queue1, queue2):
        """Spawn a headless server for two players."""
        # Find available port
        port = self.find_available_port()
        if not port:
            self.stdout.write(self.style.ERROR('✗ No available ports!'))
            return
        
        # Create match instance
        match = MatchInstance.objects.create(
            player1=queue1.player,
            player2=queue2.player,
            player1_deck=queue1.deck,
            player2_deck=queue2.deck,
            server_ip='127.0.0.1',
            server_port=port,
            status='initializing'
        )
        
        # Build command
        cmd = [
            self.HEADLESS_SERVER_PATH,
            '-batchmode',
            '-nographics',
            '-server',
            f'-port={port}',
            f'-player1Id={queue1.player.id}',
            f'-player2Id={queue2.player.id}',
        ]
        
        try:
            # Spawn headless server
            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0
            )
            
            match.server_pid = process.pid
            match.save()
            
            self.active_servers[port] = {
                'process': process,
                'match_id': match.id,
                'start_time': datetime.now()
            }
            
            # Update queue entries
            queue1.status = 'matched'
            queue1.matched_at = timezone.now()
            queue1.save()
            
            queue2.status = 'matched'
            queue2.matched_at = timezone.now()
            queue2.save()
            
            self.stdout.write(
                self.style.SUCCESS(
                    f"✓ Created match between {queue1.player.username} and {queue2.player.username} "
                    f"on port {port} (PID: {process.pid})"
                )
            )
            
        except Exception as e:
            self.stdout.write(self.style.ERROR(f"✗ Failed to spawn server: {e}"))
            match.status = 'failed'
            match.save()
    
    def monitor_active_servers(self):
        """Check if spawned servers are still running."""
        dead_ports = []
        
        for port, server_info in self.active_servers.items():
            process = server_info['process']
            
            if not process.poll() is None:  # Process died
                exit_code = process.returncode
                match_id = server_info['match_id']
                
                self.stdout.write(
                    self.style.WARNING(
                        f"⚠ Server died on port {port} (PID: {process.pid}, exit code: {exit_code})"
                    )
                )
                
                # Update match status
                try:
                    match = MatchInstance.objects.get(id=match_id)
                    match.status = 'failed'
                    match.save()
                    
                    # Re-queue both players
                    for player in [match.player1, match.player2]:
                        queue = MatchQueue.objects.create(
                            player=player,
                            deck=player.savedeck_set.get(is_active=True),
                            status='queued'
                        )
                        self.stdout.write(f"  Re-queued {player.username}")
                
                except Exception as e:
                    self.stdout.write(self.style.ERROR(f"  Failed to re-queue: {e}"))
                
                dead_ports.append(port)
        
        # Clean up dead servers
        for port in dead_ports:
            del self.active_servers[port]
    
    def find_available_port(self):
        """Find an available port in the server range."""
        for port in range(self.BASE_PORT, self.MAX_PORT + 1):
            if port not in self.active_servers:
                return port
        return None
    
    def cleanup_all_servers(self):
        """Terminate all spawned servers on shutdown."""
        for port, server_info in self.active_servers.items():
            try:
                process = server_info['process']
                if process.poll() is None:  # Still running
                    process.terminate()
                    process.wait(timeout=5)
                    self.stdout.write(self.style.SUCCESS(f"✓ Terminated server on port {port}"))
            except Exception as e:
                self.stdout.write(self.style.ERROR(f"✗ Failed to terminate port {port}: {e}"))
```

### Register Command

**File:** `matchmaking/management/__init__.py` (create if doesn't exist)

```python
# Empty file
```

**File:** `matchmaking/management/commands/__init__.py` (create if doesn't exist)

```python
# Empty file
```

---

## 6. Run the Matchmaker Service

### Development

```bash
python manage.py matchmaker
```

### Output Example

```
✓ Matchmaker started
Listening for queued players (polling every 2s)...

[Queue: 1 player] testplayer
[Queue: 2 players] testplayer testplayer2
✓ Created match between testplayer and testplayer2 on port 7777 (PID: 31524)
[Match Running] Match on port 7777 (PID: 31524)
⚠ Server died on port 7777 (PID: 31524, exit code: 1)
  Re-queued testplayer
  Re-queued testplayer2
```

---

## 7. Testing Checklist

- [ ] Player deck endpoint is accessible from localhost without auth
- [ ] Player deck endpoint returns complete deck data (all 10 cards)
- [ ] Queue status endpoint returns nested match_instance when matched
- [ ] Match instance includes server_ip, server_port, server_pid
- [ ] Match instance includes both player1_deck and player2_deck fully serialized
- [ ] Matchmaker service runs and spawns headless servers
- [ ] Headless server can fetch both player decks via public endpoint
- [ ] Headless server stays alive and listens for client connections
- [ ] Clients can connect to headless server and start match

---

## 8. Troubleshooting

### Issue: Headless server crashes with exit code 1

**Causes:**
- ❌ Player deck endpoint returns 403 Forbidden
- ❌ Missing player ID command-line arguments
- ❌ Endpoint returns wrong JSON structure

**Solution:**
1. Verify endpoint is public for localhost
2. Check server console logs in `server_logs/`
3. Test endpoint manually: `curl http://localhost:8000/api/players/{id}/active-deck/`

### Issue: Clients can't connect to server

**Causes:**
- ❌ Server process died (check exit code)
- ❌ Wrong IP/port in match_instance response
- ❌ Firewall blocking port

**Solution:**
1. Monitor matchmaker logs
2. Check if server process is running: `netstat -ano | findstr :7777`
3. Verify ports 7777-7800 are not blocked

### Issue: Queue-status returns "none" instead of "matched"

**Causes:**
- ❌ Match instance not created properly
- ❌ Query logic incorrect in queue_status view

**Solution:**
1. Check MatchInstance table directly: `python manage.py shell`
2. Query: `MatchInstance.objects.all()` to see if match exists
3. Verify `get_queue_position()` function works

---

## 9. Migration Commands

```bash
# If you modified models, run migrations
python manage.py makemigrations
python manage.py migrate

# Create superuser for testing
python manage.py createsuperuser

# Run matchmaker (in separate terminal)
python manage.py matchmaker

# Test endpoints (in PowerShell)
.\test_matchmaking_two_players.ps1
```

---

## Summary

**What you need to do:**

1. ✅ Make `/api/players/{id}/active-deck/` public for localhost
2. ✅ Update `queue-status` to return nested `match_instance`
3. ✅ Verify MatchQueue and MatchInstance models exist
4. ✅ Create matchmaker.py management command
5. ✅ Register the command in management/commands/
6. ✅ Run: `python manage.py matchmaker`
7. ✅ Test: `.\test_matchmaking_two_players.ps1`

**Expected result:** Headless servers spawn, stay alive, and accept client connections!
