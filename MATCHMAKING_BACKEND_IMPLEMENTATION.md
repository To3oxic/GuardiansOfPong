# Matchmaking System - Backend Implementation Guide

## 📋 Overview

This guide provides step-by-step instructions for implementing a **simple matchmaking system** that:
1. Queues players looking for matches
2. Finds pairs of players
3. Spawns headless Unity servers for matches
4. Tracks match instances and player decks

---

## 🗄️ PART 1: DATABASE MODELS

### Location
`C:\Users\ahmad\GuardiansBackend\players\models.py`

### Add Two New Models

**Model 1: MatchQueue**
```python
from django.db import models
from django.utils import timezone

class MatchQueue(models.Model):
    STATUS_CHOICES = [
        ('queued', 'Queued'),
        ('matched', 'Matched'),
        ('cancelled', 'Cancelled'),
    ]
    
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    player = models.ForeignKey(CustomUser, on_delete=models.CASCADE, related_name='match_queues')
    deck = models.ForeignKey(SavedDeck, on_delete=models.CASCADE)
    status = models.CharField(max_length=20, choices=STATUS_CHOICES, default='queued')
    joined_at = models.DateTimeField(auto_now_add=True)
    matched_at = models.DateTimeField(null=True, blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    
    class Meta:
        db_table = 'match_queue'
        ordering = ['joined_at']
    
    def __str__(self):
        return f"{self.player.username} - {self.status}"
```

**Model 2: MatchInstance**
```python
class MatchInstance(models.Model):
    STATUS_CHOICES = [
        ('initializing', 'Initializing'),
        ('running', 'Running'),
        ('completed', 'Completed'),
        ('failed', 'Failed'),
    ]
    
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    player1 = models.ForeignKey(CustomUser, on_delete=models.CASCADE, related_name='matches_as_player1')
    player2 = models.ForeignKey(CustomUser, on_delete=models.CASCADE, related_name='matches_as_player2')
    player1_deck = models.ForeignKey(SavedDeck, on_delete=models.CASCADE, related_name='matches_player1')
    player2_deck = models.ForeignKey(SavedDeck, on_delete=models.CASCADE, related_name='matches_player2')
    
    server_ip = models.CharField(max_length=50)
    server_port = models.IntegerField()
    server_pid = models.IntegerField(null=True, blank=True)  # Process ID for tracking
    
    status = models.CharField(max_length=20, choices=STATUS_CHOICES, default='initializing')
    winner = models.ForeignKey(CustomUser, on_delete=models.SET_NULL, null=True, blank=True, related_name='won_matches')
    
    created_at = models.DateTimeField(auto_now_add=True)
    started_at = models.DateTimeField(null=True, blank=True)
    ended_at = models.DateTimeField(null=True, blank=True)
    
    class Meta:
        db_table = 'match_instances'
        ordering = ['-created_at']
    
    def __str__(self):
        return f"{self.player1.username} vs {self.player2.username} - {self.status}"
```

### Migration Commands
```bash
# After adding models:
python manage.py makemigrations
python manage.py migrate
```

---

## 🔌 PART 2: API ENDPOINTS

### Location
`C:\Users\ahmad\GuardiansBackend\players\views.py`

### Add Matchmaking ViewSet

```python
from rest_framework import viewsets, status
from rest_framework.decorators import action
from rest_framework.response import Response
from rest_framework.permissions import IsAuthenticated
from django.utils import timezone
from .models import MatchQueue, MatchInstance, SavedDeck
from .serializers import MatchQueueSerializer, MatchInstanceSerializer

class MatchmakingViewSet(viewsets.ViewSet):
    permission_classes = [IsAuthenticated]
    
    @action(detail=False, methods=['post'])
    def join_queue(self, request):
        """
        POST /api/matchmaking/join_queue/
        Body: { "deck_id": "uuid" }
        Response: { "queued": true, "queue_position": 5, "message": "..." }
        """
        deck_id = request.data.get('deck_id')
        
        if not deck_id:
            return Response(
                {'error': 'deck_id is required'}, 
                status=status.HTTP_400_BAD_REQUEST
            )
        
        try:
            deck = SavedDeck.objects.get(id=deck_id, player=request.user)
        except SavedDeck.DoesNotExist:
            return Response(
                {'error': 'Deck not found or does not belong to you'}, 
                status=status.HTTP_404_NOT_FOUND
            )
        
        # Check if player already in queue
        existing = MatchQueue.objects.filter(
            player=request.user,
            status='queued'
        ).first()
        
        if existing:
            queue_count = MatchQueue.objects.filter(status='queued').count()
            return Response({
                'queued': True,
                'queue_position': queue_count,
                'message': 'Already in queue'
            })
        
        # Add to queue
        queue_entry = MatchQueue.objects.create(
            player=request.user,
            deck=deck,
            status='queued'
        )
        
        queue_count = MatchQueue.objects.filter(status='queued').count()
        
        return Response({
            'queued': True,
            'queue_entry_id': str(queue_entry.id),
            'queue_position': queue_count,
            'message': 'Successfully joined queue'
        }, status=status.HTTP_201_CREATED)
    
    @action(detail=False, methods=['get'])
    def queue_status(self, request):
        """
        GET /api/matchmaking/queue_status/
        Response: 
        - If queued: { "status": "queued", "queue_position": 5 }
        - If matched: { 
            "status": "matched", 
            "match_id": "uuid",
            "opponent": "player2_username",
            "server_ip": "127.0.0.1",
            "server_port": 7777,
            "player1_deck": {...},
            "player2_deck": {...}
          }
        - If not in queue: { "status": "none" }
        """
        queue_entry = MatchQueue.objects.filter(
            player=request.user,
            status__in=['queued', 'matched']
        ).first()
        
        if not queue_entry:
            return Response({'status': 'none'})
        
        if queue_entry.status == 'queued':
            queue_count = MatchQueue.objects.filter(status='queued').count()
            return Response({
                'status': 'queued',
                'queue_position': queue_count
            })
        
        # Status is 'matched' - find the match instance
        match = MatchInstance.objects.filter(
            models.Q(player1=request.user) | models.Q(player2=request.user),
            status='initializing'
        ).first()
        
        if match:
            opponent = match.player2 if match.player1 == request.user else match.player1
            opponent_deck = match.player2_deck if match.player1 == request.user else match.player1_deck
            
            return Response({
                'status': 'matched',
                'match_id': str(match.id),
                'opponent': opponent.username,
                'server_ip': match.server_ip,
                'server_port': match.server_port,
                'player1_deck': MatchInstanceSerializer().get_player1_deck(match),
                'player2_deck': MatchInstanceSerializer().get_player2_deck(match)
            })
        
        return Response({'status': 'none'})
    
    @action(detail=False, methods=['post'])
    def leave_queue(self, request):
        """
        POST /api/matchmaking/leave_queue/
        Response: { "success": true }
        """
        queue_entry = MatchQueue.objects.filter(
            player=request.user,
            status='queued'
        ).first()
        
        if queue_entry:
            queue_entry.status = 'cancelled'
            queue_entry.save()
            return Response({'success': True})
        
        return Response({'success': False, 'message': 'Not in queue'})
```

### Update URL Routing

In `C:\Users\Ahmad\GuardiansBackend\guardians_backend\urls.py`:

```python
from rest_framework.routers import DefaultRouter
from players.views import PlayerViewSet, MatchmakingViewSet

router = DefaultRouter()
router.register(r'players', PlayerViewSet)
router.register(r'matchmaking', MatchmakingViewSet, basename='matchmaking')

urlpatterns = [
    path('admin/', admin.site.urls),
    path('api/', include(router.urls)),
]
```

---

## 🎯 PART 3: SERIALIZERS

### Location
`C:\Users\ahmad\GuardiansBackend\players\serializers.py`

### Add Serializers

```python
from rest_framework import serializers
from .models import MatchQueue, MatchInstance

class MatchQueueSerializer(serializers.ModelSerializer):
    player_username = serializers.CharField(source='player.username', read_only=True)
    deck_name = serializers.CharField(source='deck.deck_name', read_only=True)
    
    class Meta:
        model = MatchQueue
        fields = ['id', 'player_username', 'deck_name', 'status', 'joined_at']

class MatchInstanceSerializer(serializers.ModelSerializer):
    player1_username = serializers.CharField(source='player1.username', read_only=True)
    player2_username = serializers.CharField(source='player2.username', read_only=True)
    
    class Meta:
        model = MatchInstance
        fields = [
            'id', 'player1_username', 'player2_username',
            'server_ip', 'server_port', 'status', 'created_at'
        ]
    
    def get_player1_deck(self, obj):
        return SavedDeckSerializer(obj.player1_deck).data
    
    def get_player2_deck(self, obj):
        return SavedDeckSerializer(obj.player2_deck).data
```

---

## ⚙️ PART 4: MATCHMAKER BACKGROUND SERVICE

### Location
`C:\Users\ahmad\GuardiansBackend\players\management\commands\matchmaker.py`

### Create Matchmaker Service

```python
import os
import sys
import subprocess
import time
import psutil
from django.core.management.base import BaseCommand
from players.models import MatchQueue, MatchInstance
from django.utils import timezone

class Command(BaseCommand):
    help = 'Background service that matches queued players and spawns headless servers'
    
    HEADLESS_SERVER_PATH = r'C:\path\to\GuardiansOfPong\GuardiansOfPong.exe'  # CHANGE THIS
    BASE_PORT = 7777
    MAX_SERVERS = 24
    POLLING_INTERVAL = 2  # seconds
    
    def add_arguments(self, parser):
        parser.add_argument('--buildpath', type=str, help='Path to headless build executable')
    
    def handle(self, *args, **options):
        if options['buildpath']:
            self.HEADLESS_SERVER_PATH = options['buildpath']
        
        self.stdout.write(self.style.SUCCESS('Matchmaker service started'))
        self.stdout.write(f'Build path: {self.HEADLESS_SERVER_PATH}')
        self.stdout.write(f'Base port: {self.BASE_PORT}')
        
        # Dictionary to track active server processes
        active_servers = {}
        
        try:
            while True:
                try:
                    # Clean up dead processes
                    self.cleanup_dead_processes(active_servers)
                    
                    # Check for matches to make
                    self.find_and_create_matches(active_servers)
                    
                    # Brief pause before next check
                    time.sleep(self.POLLING_INTERVAL)
                    
                except Exception as e:
                    self.stdout.write(self.style.ERROR(f'Error in matchmaking loop: {str(e)}'))
                    time.sleep(self.POLLING_INTERVAL)
        
        except KeyboardInterrupt:
            self.stdout.write(self.style.WARNING('Shutting down matchmaker...'))
            self.cleanup_all_servers(active_servers)
    
    def find_and_create_matches(self, active_servers):
        """Find waiting players and create matches"""
        queued_players = MatchQueue.objects.filter(status='queued').order_by('joined_at')
        
        if len(queued_players) < 2:
            return  # Not enough players
        
        # Get first two players
        player1_queue = queued_players[0]
        player2_queue = queued_players[1]
        
        self.stdout.write(
            f'[MATCH] Found match: {player1_queue.player.username} vs {player2_queue.player.username}'
        )
        
        # Find available port
        available_port = self.find_available_port()
        if not available_port:
            self.stdout.write(self.style.ERROR('No available ports for new match'))
            return
        
        # Spawn headless server
        try:
            process = self.spawn_headless_server(available_port)
            if not process:
                return
            
            # Create match instance
            match = MatchInstance.objects.create(
                player1=player1_queue.player,
                player2=player2_queue.player,
                player1_deck=player1_queue.deck,
                player2_deck=player2_queue.deck,
                server_ip='127.0.0.1',  # Or use public IP
                server_port=available_port,
                server_pid=process.pid,
                status='initializing'
            )
            
            # Update queue entries
            player1_queue.status = 'matched'
            player1_queue.matched_at = timezone.now()
            player1_queue.save()
            
            player2_queue.status = 'matched'
            player2_queue.matched_at = timezone.now()
            player2_queue.save()
            
            # Track the process
            active_servers[available_port] = {
                'process': process,
                'match_id': str(match.id),
                'created_at': timezone.now()
            }
            
            self.stdout.write(
                self.style.SUCCESS(
                    f'[SPAWNED] Server on port {available_port} for match {match.id}'
                )
            )
        
        except Exception as e:
            self.stdout.write(self.style.ERROR(f'Failed to spawn server: {str(e)}'))
    
    def spawn_headless_server(self, port):
        """Spawn a new headless server instance"""
        try:
            # Verify build exists
            if not os.path.exists(self.HEADLESS_SERVER_PATH):
                self.stdout.write(self.style.ERROR(f'Build not found: {self.HEADLESS_SERVER_PATH}'))
                return None
            
            # Create log file
            log_file = f'server_{port}.log'
            
            # Build command
            cmd = [
                self.HEADLESS_SERVER_PATH,
                '-batchmode',
                '-nographics',
                '-server',
                f'-port {port}',
                f'-logFile {log_file}'
            ]
            
            # Spawn process
            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == 'win32' else 0
            )
            
            self.stdout.write(f'[PROCESS] Started PID {process.pid} on port {port}')
            return process
        
        except Exception as e:
            self.stdout.write(self.style.ERROR(f'Exception spawning server: {str(e)}'))
            return None
    
    def find_available_port(self):
        """Find an available port in the range"""
        for port in range(self.BASE_PORT, self.BASE_PORT + self.MAX_SERVERS):
            try:
                # Check if port is in use
                connections = psutil.net_connections()
                in_use = any(conn.laddr.port == port for conn in connections)
                if not in_use:
                    return port
            except:
                return port  # Fallback
        
        return None
    
    def cleanup_dead_processes(self, active_servers):
        """Remove dead processes from tracking"""
        dead_ports = []
        
        for port, server_info in active_servers.items():
            process = server_info['process']
            
            # Check if process is still alive
            if process.poll() is not None:  # Process has terminated
                dead_ports.append(port)
                
                # Update match instance to failed
                try:
                    match = MatchInstance.objects.get(id=server_info['match_id'])
                    match.status = 'failed'
                    match.ended_at = timezone.now()
                    match.save()
                    
                    # Re-queue the players
                    MatchQueue.objects.filter(
                        player__in=[match.player1, match.player2],
                        status='matched'
                    ).update(status='queued', matched_at=None)
                    
                    self.stdout.write(
                        self.style.WARNING(f'[CLEANUP] Server on port {port} died, re-queueing players')
                    )
                except:
                    pass
        
        # Remove dead ports
        for port in dead_ports:
            del active_servers[port]
    
    def cleanup_all_servers(self, active_servers):
        """Kill all running servers gracefully"""
        for port, server_info in active_servers.items():
            try:
                process = server_info['process']
                process.terminate()
                process.wait(timeout=5)
                self.stdout.write(f'[CLEANUP] Terminated server on port {port}')
            except:
                try:
                    process.kill()
                except:
                    pass
```

### Run the Matchmaker

```bash
# In terminal:
python manage.py matchmaker

# Or with custom build path:
python manage.py matchmaker --buildpath "C:\path\to\build.exe"
```

---

## 📝 DISCONNECTION HANDLING

### Instant Removal from Queue

When a player **disconnects from backend** (network loss, logout, etc):

**Option 1: Polling Cleanup** (Automatic every 5 minutes)
```python
# In matchmaker.py
def cleanup_stale_queue_entries():
    """Remove queue entries older than 5 minutes with no activity"""
    five_minutes_ago = timezone.now() - timedelta(minutes=5)
    
    MatchQueue.objects.filter(
        status='queued',
        joined_at__lt=five_minutes_ago
    ).delete()
```

**Option 2: Client-Initiated Leave** (Recommended - Instant)
- Client calls: `POST /api/matchmaking/leave_queue/` when:
  - User clicks "Cancel" button
  - App minimizes
  - Network disconnects
  - Scene changes

This **instantly removes** the entry from queue.

---

## 🧪 TESTING COMMANDS

### Test Locally

```bash
# 1. Make migrations
python manage.py makemigrations
python manage.py migrate

# 2. Create test players (or use existing)
python manage.py seed_sample_player

# 3. In one terminal - start matchmaker
python manage.py matchmaker --buildpath "path/to/build.exe"

# 4. In another terminal - test API
curl -X POST http://localhost:8000/api/matchmaking/join_queue/ \
  -H "Content-Type: application/json" \
  -d '{"deck_id": "your-deck-id"}' \
  -H "Cookie: sessionid=your-session-id"

# 5. Check queue status
curl http://localhost:8000/api/matchmaking/queue_status/ \
  -H "Cookie: sessionid=your-session-id"
```

---

## 📋 VERIFICATION CHECKLIST

- [ ] Models created and migrated
- [ ] ViewSet endpoints accessible via API
- [ ] Serializers return correct JSON format
- [ ] Matchmaker service runs without errors
- [ ] Headless server spawning works
- [ ] Port allocation works correctly
- [ ] Queue entries matched and updated to 'matched'
- [ ] Match instances created with correct data
- [ ] Process tracking (PID) working
- [ ] Dead process cleanup working
- [ ] Stale queue entry cleanup working

---

## ⚠️ IMPORTANT NOTES

1. **Headless Build Path**: Update `HEADLESS_SERVER_PATH` in `matchmaker.py` to point to your actual build
2. **IP Address**: If deploying publicly, replace `'127.0.0.1'` with your actual server IP
3. **Port Range**: Adjust `BASE_PORT` and `MAX_SERVERS` based on your needs
4. **Process Cleanup**: The matchmaker automatically cleans up dead processes
5. **Database Cleanup**: Set up a daily cron job to clean stale queue entries

---

## 🔗 NEXT STEPS (Unity Client)

After backend is ready:
1. Create `MatchmakingManager.cs`
2. Implement `JoinQueue()` → POST `/api/matchmaking/join_queue/`
3. Implement polling `CheckMatchStatus()` → GET `/api/matchmaking/queue_status/`
4. When matched, connect NetworkManager to returned IP:port
5. Load game scene with both players' decks
