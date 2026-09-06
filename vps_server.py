#!/usr/bin/env python3
"""
VPS Server - Manages headless game server instances on Contabo VPS
Receives API calls to start matches and manages the game server lifecycle
"""

import os
import sys
import json
import subprocess
import threading
import time
import logging
from datetime import datetime
from pathlib import Path
from flask import Flask, request, jsonify
from dataclasses import dataclass, asdict
from typing import Dict, Optional

# Create logs directory if it doesn't exist
logs_dir = Path('logs')
logs_dir.mkdir(exist_ok=True)

# Configure logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler('vps_server.log'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)

app = Flask(__name__)

# Request logging middleware
@app.before_request
def log_request():
    """Log all incoming requests"""
    logger.debug(f"[REQUEST] {request.method} {request.path} from {request.remote_addr}")

@app.after_request
def log_response(response):
    """Log all outgoing responses"""
    logger.debug(f"[RESPONSE] {request.method} {request.path} -> {response.status_code}")
    return response

# Configuration
GAME_BUILD_PATH = os.path.expanduser(os.environ.get('GAME_BUILD_PATH', '~/game/builds/Game.x86_64'))
VPS_PUBLIC_IP = os.environ.get('VPS_PUBLIC_IP', '185.252.233.117')  # Contabo VPS IP
BASE_PORT = int(os.environ.get('BASE_PORT', 7777))
BACKEND_URL = os.environ.get('BACKEND_URL', 'http://localhost:8000')
PLAYFAB_API_URL = os.environ.get('PLAYFAB_API_URL', 'http://localhost:5000')
VPS_SECRET_KEY = "vps_secret_key_12345"  # Shared secret with PlayFab cloud script
@dataclass
class ServerInstance:
    """Represents a running game server instance"""
    match_id: str
    player1_id: str
    player2_id: str
    port: int
    process: Optional[subprocess.Popen] = None
    status: str = "starting"  # starting, ready, playing, stopped
    created_at: str = None
    ip_address: str = VPS_PUBLIC_IP
    
    def __post_init__(self):
        if self.created_at is None:
            self.created_at = datetime.now().isoformat()

# Global server instances
server_instances: Dict[str, ServerInstance] = {}
next_port = BASE_PORT

def get_available_port():
    """Get the next available port for a server instance"""
    global next_port
    port = next_port
    next_port += 1
    return port

def _convert_deck_format(deck: dict) -> dict:
    """
    Convert deck from camelCase (PlayFab format) to snake_case (DeckPayload format)
    PlayFab format: {deckName, cardIds, lastModified, ...}
    DeckPayload format: {deck_name, cards, id, legend_stone, guardian_card, ...}
    """
    if not deck:
        return deck
    
    converted = {}
    
    # Map camelCase to snake_case
    if 'deckName' in deck:
        converted['deck_name'] = deck['deckName']
    if 'id' in deck:
        converted['id'] = deck['id']
    
    # Convert cardIds list to cards list (matching DeckPayload.CardDataDto structure)
    if 'cardIds' in deck:
        cards = []
        for card_id in deck['cardIds']:
            cards.append({
                'id': card_id,
                'registry_name': card_id,
                'card_type': 'unknown',
                'level': 1
            })
        converted['cards'] = cards
    
    # Handle legend stone (guardian_card)
    if 'guardianCard' in deck:
        converted['guardian_card'] = deck['guardianCard']
    elif 'guardian_card' in deck:
        converted['guardian_card'] = deck['guardian_card']
    
    # Handle legend stone
    if 'legendStone' in deck:
        converted['legend_stone'] = deck['legendStone']
    elif 'legend_stone' in deck:
        converted['legend_stone'] = deck['legend_stone']
    
    # Copy any other fields
    for key, value in deck.items():
        if key not in ['deckName', 'cardIds', 'guardianCard', 'legendStone', 'lastModified']:
            if key not in converted:
                converted[key] = value
    
    return converted

def start_game_server(instance: ServerInstance, player1_deck: dict, player2_deck: dict) -> bool:
    """
    Start a Unity headless game server instance
    
    Args:
        instance: ServerInstance with player IDs and port
        player1_deck: Player 1's deck data (will be converted to DeckPayload format)
        player2_deck: Player 2's deck data (will be converted to DeckPayload format)
    
    Returns:
        True if server started successfully
    """
    if not os.path.exists(GAME_BUILD_PATH):
        logger.error(f"Game build not found at {GAME_BUILD_PATH}")
        return False
    
    try:
        # Convert deck format from PlayFab camelCase to DeckPayload snake_case
        player1_deck_converted = _convert_deck_format(player1_deck)
        player2_deck_converted = _convert_deck_format(player2_deck)
        
        # Convert decks to JSON strings for command-line args
        player1_deck_json = json.dumps(player1_deck_converted)
        player2_deck_json = json.dumps(player2_deck_converted)
        
        # Build command line arguments
        args = [
            GAME_BUILD_PATH,
            '-server',
            f'-nographics',
            f'-batchmode',
            f'-port={instance.port}',
            f'-player1Id={instance.player1_id}',
            f'-player2Id={instance.player2_id}',
            f'-matchId={instance.match_id}',
            f'-player1Deck={player1_deck_json}',
            f'-player2Deck={player2_deck_json}',
        ]
        
        logger.info(f"Starting game server on port {instance.port}")
        logger.info(f"  Match ID: {instance.match_id}")
        logger.info(f"  Player 1: {instance.player1_id}")
        logger.info(f"  Player 2: {instance.player2_id}")
        logger.info(f"  Command: {' '.join(args[:8])} [decks...]")
        
        # Start the process
        # Use line buffering (bufsize=1) to ensure output is captured immediately
        process = subprocess.Popen(
            args,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,  # Redirect stderr to stdout so we capture everything
            env=os.environ.copy(),
            text=True,
            bufsize=1,  # Line buffering
            universal_newlines=True
        )
        
        instance.process = process
        instance.status = "starting"
        
        # Create per-match log file
        match_log_path = os.path.expanduser(f"~/game/logs/match_{instance.match_id}.log")
        os.makedirs(os.path.dirname(match_log_path), exist_ok=True)
        
        logger.info(f"Game server process started with PID {process.pid}")
        logger.info(f"Match logs: {match_log_path}")
        
        # Monitor process output in background thread
        def monitor_output():
            try:
                with open(match_log_path, 'w', buffering=1) as match_log:  # Line buffering
                    match_log.write(f"=== Match {instance.match_id} Started ===\n")
                    match_log.write(f"PID: {process.pid}\n")
                    match_log.write(f"Players: {instance.player1_id} vs {instance.player2_id}\n")
                    match_log.write(f"Port: {instance.port}\n")
                    match_log.write(f"Start Time: {datetime.now().isoformat()}\n")
                    match_log.write(f"Command: {' '.join(args[:10])}\n")
                    match_log.write(f"=== SERVER OUTPUT STARTS BELOW ===\n\n")
                    match_log.flush()
                    os.fsync(match_log.fileno())  # Force flush to disk
                    
                    line_count = 0
                    ready_detected = False
                    
                    # Read combined stdout/stderr line by line
                    while instance.process.poll() is None:
                        try:
                            line = instance.process.stdout.readline()
                            if line:
                                line_count += 1
                                timestamp = datetime.now().isoformat()
                                log_entry = f"[{timestamp}] {line.rstrip()}\n"
                                
                                # Write to match log immediately
                                match_log.write(log_entry)
                                match_log.flush()
                                os.fsync(match_log.fileno())  # Force write to disk immediately
                                
                                # Also log to main logger
                                logger.info(f"[Server {instance.match_id}] {line.rstrip()}")
                                
                                # Check for ready marker (case-insensitive)
                                if not ready_detected and "[ready]" in line.lower():
                                    ready_detected = True
                                    logger.info(f"[READY DETECTED] Server {instance.match_id} is ready at line #{line_count}!")
                                    match_log.write(f"\n*** READY MARKER DETECTED AT LINE {line_count} ***\n\n")
                                    match_log.flush()
                                    os.fsync(match_log.fileno())
                                    instance.status = "ready"
                                    signal_server_ready(instance)
                        except Exception as read_err:
                            logger.debug(f"Error reading line: {read_err}")
                            continue
                    
                    # Write final status
                    returncode = process.returncode
                    match_log.write(f"\n=== Process Ended ===\n")
                    match_log.write(f"Total lines read: {line_count}\n")
                    match_log.write(f"Exit code: {returncode}\n")
                    match_log.write(f"End Time: {datetime.now().isoformat()}\n")
                    match_log.write(f"Ready marker detected: {ready_detected}\n")
                    
                    # Read any remaining output after process exits
                    try:
                        remaining = process.stdout.read()
                        if remaining:
                            match_log.write(f"\n[FINAL BUFFER - {len(remaining)} bytes]\n{remaining}\n")
                            logger.info(f"[Server {instance.match_id}] Final output ({len(remaining)} bytes): {remaining}")
                    except Exception as e:
                        logger.error(f"Error reading final output: {e}")
                    
                    match_log.flush()
                    os.fsync(match_log.fileno())
                    
            except Exception as e:
                logger.error(f"Error monitoring server output: {e}")
                with open(match_log_path, 'a') as match_log:
                    match_log.write(f"\n[ERROR] Exception in monitor_output: {e}\n")
        
        monitor_thread = threading.Thread(target=monitor_output, daemon=True)
        monitor_thread.start()
        
        return True
        
    except Exception as e:
        logger.error(f"Failed to start game server: {e}")
        instance.status = "failed"
        return False

def stop_game_server(match_id: str) -> bool:
    """Stop a running game server instance"""
    if match_id not in server_instances:
        logger.warning(f"Server instance {match_id} not found")
        return False
    
    instance = server_instances[match_id]
    
    if instance.process:
        try:
            instance.process.terminate()
            instance.process.wait(timeout=5)
            logger.info(f"Server {match_id} terminated")
        except subprocess.TimeoutExpired:
            instance.process.kill()
            logger.info(f"Server {match_id} killed")
        except Exception as e:
            logger.error(f"Error stopping server {match_id}: {e}")
    
    instance.status = "stopped"
    return True

def signal_server_ready(instance: ServerInstance):
    """Mark server instance as ready when game server logs [READY] marker"""
    try:
        logger.info(f"[SIGNAL-READY] Game server {instance.match_id} is ready!")
        logger.info(f"[SIGNAL-READY] Setting instance status to 'ready'")
        
        instance.status = "ready"
        
        logger.info(f"[SIGNAL-READY] Match {instance.match_id} ready on {instance.ip_address}:{instance.port}")
        logger.info(f"[SIGNAL-READY] Clients can now connect to server at http://{instance.ip_address}:{instance.port}")
        
        # Note: Eventually you'd call PlayFab's markMatchReady cloud function here
        # For now, the clients can query /api/match/{match_id}/status to get the server info
        
    except Exception as e:
        logger.error(f"[SIGNAL-READY] Unexpected error: {e}")
        import traceback
        logger.error(traceback.format_exc())

# ============================================================================
# API ENDPOINTS
# ============================================================================

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({'status': 'healthy', 'timestamp': datetime.now().isoformat()})

@app.route('/api/start-match', methods=['POST'])
def start_match():
    """
    Start a new game server for a match
    
    Expected request body:
    {
        "match_id": "match_123",
        "player1_id": "player_1",
        "player2_id": "player_2",
        "player1_deck": {...},
        "player2_deck": {...},
        "api_key": "secret_key"
    }
    """
    try:
        # Log incoming request
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        log_filename = logs_dir / f"start_match_{timestamp}.log"
        
        # Get request data
        data = request.get_json()
        
        # Validate API key
        api_key = data.get('api_key') if data else None
        if not api_key:
            logger.error("[START-MATCH] Missing API key in request")
            return jsonify({
                'success': False,
                'error': 'Missing API key'
            }), 401
        
        # Validate against shared secret
        if api_key != VPS_SECRET_KEY:
            logger.error(f"[START-MATCH] Invalid API key: {api_key}")
            return jsonify({
                'success': False,
                'error': 'Invalid API key'
            }), 401
        
        logger.info("[START-MATCH] API key validated successfully")
        
        # Create detailed request log
        with open(log_filename, 'w') as f:
            f.write("=== START MATCH REQUEST LOG ===\n")
            f.write(f"Timestamp: {datetime.now().isoformat()}\n")
            f.write(f"Request IP: {request.remote_addr}\n")
            f.write(f"Request URL: {request.url}\n")
            f.write(f"Request Method: {request.method}\n\n")
            
            f.write("REQUEST DATA:\n")
            f.write(json.dumps(data, indent=2))
            f.write("\n\n")
            
            # Log each field separately
            f.write("PARSED FIELDS:\n")
            if data:
                f.write(f"  match_id: {data.get('match_id', 'MISSING')}\n")
                f.write(f"  player1_id: {data.get('player1_id', 'MISSING')}\n")
                f.write(f"  player2_id: {data.get('player2_id', 'MISSING')}\n")
                f.write(f"  player1_deck: {json.dumps(data.get('player1_deck', {}), indent=4)}\n")
                f.write(f"  player2_deck: {json.dumps(data.get('player2_deck', {}), indent=4)}\n")
        
        logger.info(f"[START-MATCH] Request received - Log file: {log_filename}")
        logger.debug(f"[START-MATCH] Request data: {data}")
        
        # Validate required fields
        required_fields = ['match_id', 'player1_id', 'player2_id', 'player1_deck', 'player2_deck']
        missing_fields = [f for f in required_fields if f not in data]
        
        if missing_fields:
            error_msg = f'Missing required fields: {", ".join(missing_fields)}'
            logger.error(f"[START-MATCH] Validation failed: {error_msg}")
            
            with open(log_filename, 'a') as f:
                f.write(f"ERROR: {error_msg}\n")
            
            return jsonify({
                'success': False,
                'error': error_msg
            }), 400
        
        match_id = data['match_id']
        
        # Check if match already exists
        if match_id in server_instances:
            error_msg = f'Match {match_id} already exists'
            logger.error(f"[START-MATCH] {error_msg}")
            
            with open(log_filename, 'a') as f:
                f.write(f"ERROR: {error_msg}\n")
            
            return jsonify({
                'success': False,
                'error': error_msg
            }), 409
        
        # Create server instance
        port = get_available_port()
        instance = ServerInstance(
            match_id=match_id,
            player1_id=data['player1_id'],
            player2_id=data['player2_id'],
            port=port
        )
        
        logger.info(f"[START-MATCH] Creating instance - Match: {match_id}, Port: {port}, Players: {data['player1_id']} vs {data['player2_id']}")
        
        # Start the server
        success = start_game_server(instance, data['player1_deck'], data['player2_deck'])
        
        if success:
            server_instances[match_id] = instance
            
            # Return immediately - don't wait for server to be ready
            # PlayFab will poll /api/match/{match_id}/status to check readiness
            logger.info(f"[START-MATCH] Server started asynchronously, returning immediately")
            response = {
                'success': True,
                'match_id': match_id,
                'ip_address': instance.ip_address,
                'port': instance.port,
                'status': instance.status
            }
            
            logger.info(f"[START-MATCH] Responding immediately - Response: {response}")
            
            with open(log_filename, 'a') as f:
                f.write("\nSUCCESS (ASYNC):\n")
                f.write(json.dumps(response, indent=2))
                f.write(f"\nServer Process PID: {instance.process.pid if instance.process else 'N/A'}\n")
                f.write(f"Status will be polled via /api/match/{match_id}/status\n")
            
            return jsonify(response), 200
        else:
            error_msg = 'Failed to start game server'
            logger.error(f"[START-MATCH] {error_msg}")
            
            with open(log_filename, 'a') as f:
                f.write(f"\nFAILURE: {error_msg}\n")
            
            return jsonify({
                'success': False,
                'error': error_msg
            }), 500
            
    except Exception as e:
        error_msg = f"Exception in /api/start-match: {str(e)}"
        logger.error(error_msg, exc_info=True)
        
        # Log the exception to file
        try:
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            log_filename = logs_dir / f"start_match_{timestamp}_ERROR.log"
            with open(log_filename, 'w') as f:
                f.write(f"EXCEPTION LOG\n")
                f.write(f"Timestamp: {datetime.now().isoformat()}\n")
                f.write(f"Error: {error_msg}\n")
                f.write(f"Request IP: {request.remote_addr}\n")
                import traceback
                f.write(f"\nTraceback:\n{traceback.format_exc()}\n")
        except:
            pass
        
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500

@app.route('/api/match/<match_id>/status', methods=['GET'])
def get_match_status(match_id):
    """Get the status of a match"""
    if match_id not in server_instances:
        return jsonify({
            'success': False,
            'error': f'Match {match_id} not found'
        }), 404
    
    instance = server_instances[match_id]
    return jsonify({
        'success': True,
        'match_id': match_id,
        'status': instance.status,
        'ip_address': instance.ip_address,
        'port': instance.port
    }), 200

@app.route('/api/match/<match_id>/stop', methods=['POST'])
def stop_match(match_id):
    """Stop a running match"""
    success = stop_game_server(match_id)
    
    if success:
        return jsonify({
            'success': True,
            'message': f'Match {match_id} stopped'
        }), 200
    else:
        return jsonify({
            'success': False,
            'error': f'Failed to stop match {match_id}'
        }), 500

@app.route('/api/matches', methods=['GET'])
def list_matches():
    """List all active matches"""
    matches = []
    for match_id, instance in server_instances.items():
        matches.append({
            'match_id': match_id,
            'player1_id': instance.player1_id,
            'player2_id': instance.player2_id,
            'status': instance.status,
            'ip_address': instance.ip_address,
            'port': instance.port,
            'created_at': instance.created_at
        })
    
    return jsonify({
        'success': True,
        'matches': matches,
        'count': len(matches)
    }), 200

@app.route('/api/cleanup', methods=['POST'])
def cleanup_stopped():
    """Clean up stopped server instances"""
    try:
        stopped_matches = []
        
        for match_id in list(server_instances.keys()):
            instance = server_instances[match_id]
            
            # Check if process is still running
            if instance.process and instance.process.poll() is not None:
                # Process has ended
                if instance.status != "stopped":
                    instance.status = "stopped"
                    stopped_matches.append(match_id)
        
        logger.info(f"Cleanup: Found {len(stopped_matches)} stopped matches")
        
        return jsonify({
            'success': True,
            'cleaned_matches': stopped_matches
        }), 200
        
    except Exception as e:
        logger.error(f"Error in cleanup: {e}")
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500
# ============================================================================
# CATCH-ALL ROUTE FOR DEBUGGING
# ============================================================================

@app.route('/', defaults={'path': ''}, methods=['GET', 'POST', 'PUT', 'DELETE', 'PATCH'])
@app.route('/<path:path>', methods=['GET', 'POST', 'PUT', 'DELETE', 'PATCH'])
def catch_all(path):
    """Catch all routes for debugging"""
    logger.debug(f"[CATCH-ALL] Method={request.method}, Path={path}, Full Path={request.full_path}")
    logger.debug(f"[CATCH-ALL] Remote Addr={request.remote_addr}")
    logger.debug(f"[CATCH-ALL] Content-Type={request.content_type}")
    if request.data:
        logger.debug(f"[CATCH-ALL] Body={request.data.decode('utf-8')}")
    return jsonify({'error': f'Route not found: {path}'}), 404

# ============================================================================
# STARTUP
# ============================================================================

if __name__ == '__main__':
    logger.info("Starting VPS Game Server Manager")
    logger.info(f"Game build path: {GAME_BUILD_PATH}")
    logger.info(f"Public IP: {VPS_PUBLIC_IP}")
    logger.info(f"Base port: {BASE_PORT}")
    
    # Run Flask app
    app.run(
        host='0.0.0.0',
        port=5000,
        debug=False,
        threaded=True
    )
