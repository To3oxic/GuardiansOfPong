// PlayFab CloudScript: Matchmaking System (Queue + VPS allocator)

// ============================================================
// CONFIG
// ============================================================

var QUEUE_KEY = "matchmaking_queue";   // Title Internal Data key holding queue JSON
var VPS_API_KEY_INTERNAL = "vps_api_key"; // Title Internal Data key holding shared secret
var VPS_URL_TITLE_DATA = "vps_url";    // Title Data key (non-secret) with base URL
var HARDCODED_VPS_SECRET = "vps_secret_key_12345"; // Shared secret with VPS server

// ============================================================
// HELPERS
// ============================================================

function _nowIso() {
    return new Date().toISOString();
}

function _safeJsonParse(str, fallback) {
    try {
        return str ? JSON.parse(str) : fallback;
    } catch (e) {
        log.error("JSON parse failed: " + e + " | str=" + str);
        return fallback;
    }
}

function _getPlayFabId(context) {
    // Classic CloudScript: this is the correct player id
    if (typeof currentPlayerId !== "undefined" && currentPlayerId) return currentPlayerId;
    if (context && context.currentPlayerId) return context.currentPlayerId;
    throw "No valid PlayFabId in CloudScript context";
}

function _getTitleInternal(key) {
    var res = server.GetTitleInternalData({ Keys: [key] });
    // For TitleInternalData, values are typically plain strings: res.Data[key]
    return (res && res.Data && res.Data[key]) ? res.Data[key] : null;
}

function _setTitleInternal(key, valueStr) {
    server.SetTitleInternalData({ Key: key, Value: valueStr });
}

function _getQueue() {
    var raw = _getTitleInternal(QUEUE_KEY);
    return _safeJsonParse(raw, {}); // queue is { playFabId: { enqueuedAt, displayName? } }
}

function _saveQueue(queueObj) {
    _setTitleInternal(QUEUE_KEY, JSON.stringify(queueObj || {}));
}

function _getPlayerDeck(playFabId) {
    // You said selected_deck is stored in InternalData
    var res = server.GetUserInternalData({ PlayFabId: playFabId, Keys: ["selected_deck"] });
    if (res && res.Data && res.Data.selected_deck && res.Data.selected_deck.Value) {
        return _safeJsonParse(res.Data.selected_deck.Value, null);
    }
    return null;
}

function _getVpsUrl() {
    // Non-secret URL can be in Title Data
    var res = server.GetTitleData({ Keys: [VPS_URL_TITLE_DATA] });
    if (res && res.Data && res.Data[VPS_URL_TITLE_DATA]) {
        return res.Data[VPS_URL_TITLE_DATA];
    }
    return "http://185.252.233.117:5000"; // fallback
}

function _getVpsApiKey() {
    // Use hardcoded secret
    return HARDCODED_VPS_SECRET;
}

function _makeMatchId() {
    // simple unique id, no "match_" prefix (we use matchKey = "match_" + id)
    return (new Date().getTime()) + "_" + Math.random().toString(36).substr(2, 9);
}

function _setPlayerMatchAssignment(playerId, assignmentObj) {
    _setTitleInternal("player_match_" + playerId, JSON.stringify(assignmentObj));
}

function _getPlayerMatchAssignment(playerId) {
    var raw = _getTitleInternal("player_match_" + playerId);
    return _safeJsonParse(raw, null);
}

// ============================================================
// PUBLIC HANDLERS
// ============================================================

/**
 * Add player to matchmaking queue and try to create a match.
 * Requires: player has selected_deck in UserInternalData.
 *
 * args: { displayName?: string }
 */
handlers.startMatchmaking = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        return { success: false, error: "" + e };
    }

    // Ensure player has selected deck (anti-cheat gate)
    var deck = _getPlayerDeck(playFabId);
    if (!deck) {
        return { success: false, queued: false, error: "No selected deck found. Call saveDeck first." };
    }

    // If already assigned to a match, return that status (unless it failed)
    var existingAssign = _getPlayerMatchAssignment(playFabId);
    if (existingAssign && existingAssign.matchId) {
        // If match failed, allow re-queueing by clearing the assignment
        if (existingAssign.status === "failed") {
            log.info("Previous match failed, allowing re-queue for player " + playFabId);
            _setPlayerMatchAssignment(playFabId, null);
            // Continue to queue below
        } else {
            return {
                success: true,
                queued: false,
                alreadyAssigned: true,
                matchId: existingAssign.matchId,
                status: existingAssign.status || "starting"
            };
        }
    }

    // Load queue
    var queue = _getQueue();

    // Add/update this player
    queue[playFabId] = {
        enqueuedAt: _nowIso(),
        displayName: (args && args.displayName) ? args.displayName : playFabId
    };

    // Save queue
    _saveQueue(queue);

    // Try create match (may start VPS)
    return tryCreateMatch(playFabId, queue);
};

/**
 * Check if player has been matched / server ready.
 */
handlers.checkMatchStatus = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        return { success: false, error: "" + e };
    }

    var assign = _getPlayerMatchAssignment(playFabId);
    if (assign && assign.matchId) {
        // if ready we may also have serverIp/port
        return {
            success: true,
            matched: true,
            matchId: assign.matchId,
            status: assign.status || "starting",
            serverIp: assign.serverIp,
            serverPort: assign.serverPort,
            opponent: assign.opponent
        };
    }

    // Still in queue?
    var queue = _getQueue();
    if (queue[playFabId]) {
        return { success: true, matched: false, message: "Still in queue" };
    }

    return { success: true, matched: false, message: "Not queued" };
};

/**
 * Called by your VPS orchestration service when game server is ready.
 * args: { matchId, serverIp, serverPort, apiKey }
 */
handlers.markMatchReady = function (args, context) {
    if (!args || !args.matchId || !args.serverIp || !args.serverPort || !args.apiKey) {
        return { success: false, error: "Missing matchId/serverIp/serverPort/apiKey" };
    }

    var expectedKey = _getVpsApiKey();
    if (!expectedKey || expectedKey !== args.apiKey) {
        return { success: false, error: "Invalid API key" };
    }

    var matchKey = "match_" + args.matchId;
    var rawMatch = _getTitleInternal(matchKey);
    if (!rawMatch) {
        return { success: false, error: "Match not found: " + args.matchId };
    }

    var matchInfo = _safeJsonParse(rawMatch, null);
    if (!matchInfo) {
        return { success: false, error: "Corrupt match data" };
    }

    // Update match
    matchInfo.status = "ready";
    matchInfo.serverIp = args.serverIp;
    matchInfo.serverPort = args.serverPort;
    matchInfo.readyTime = _nowIso();
    _setTitleInternal(matchKey, JSON.stringify(matchInfo));

    // Update player assignments
    _setPlayerMatchAssignment(matchInfo.player1Id, {
        matchId: args.matchId,
        status: "ready",
        serverIp: args.serverIp,
        serverPort: args.serverPort,
        opponent: matchInfo.player2Id
    });

    _setPlayerMatchAssignment(matchInfo.player2Id, {
        matchId: args.matchId,
        status: "ready",
        serverIp: args.serverIp,
        serverPort: args.serverPort,
        opponent: matchInfo.player1Id
    });

    log.info("Match " + args.matchId + " ready at " + args.serverIp + ":" + args.serverPort);

    return { success: true, message: "Match marked as ready", matchId: args.matchId };
};

/**
 * Remove player from queue (cancel).
 */
handlers.cancelMatchmaking = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        return { success: false, error: "" + e };
    }

    // Remove from queue
    var queue = _getQueue();
    if (queue[playFabId]) {
        delete queue[playFabId];
        _saveQueue(queue);
        log.info("Player " + playFabId + " removed from matchmaking queue");
    }

    return { success: true, message: "Removed from matchmaking queue" };
};

// ============================================================
// MATCH CREATION LOGIC
// ============================================================

function tryCreateMatch(requestingPlayerId, queue) {
    // queue object already contains requesting player, but we re-load & save carefully
    var playerIds = Object.keys(queue || {});
    if (playerIds.length < 2) {
        return {
            success: true,
            queued: true,
            message: "Waiting for opponent",
            playersInQueue: playerIds.length
        };
    }

    // Pick two players (simple FIFO-ish)
    var player1Id = playerIds[0];
    var player2Id = playerIds[1];

    // Load decks
    var player1Deck = _getPlayerDeck(player1Id);
    var player2Deck = _getPlayerDeck(player2Id);

    if (!player1Deck || !player2Deck) {
        log.error("Failed to get decks for match creation");
        return { success: false, error: "Could not load player decks" };
    }

    // Capture display names BEFORE deleting
    var p1Name = (queue[player1Id] && queue[player1Id].displayName) ? queue[player1Id].displayName : player1Id;
    var p2Name = (queue[player2Id] && queue[player2Id].displayName) ? queue[player2Id].displayName : player2Id;

    var matchId = _makeMatchId();
    var matchKey = "match_" + matchId;

    var matchInfo = {
        matchId: matchId,
        player1Id: player1Id,
        player2Id: player2Id,
        player1Name: p1Name,
        player2Name: p2Name,
        player1Deck: player1Deck,
        player2Deck: player2Deck,
        status: "starting",
        createdTime: _nowIso(),
        vpsUrl: _getVpsUrl()
    };

    // Save match
    _setTitleInternal(matchKey, JSON.stringify(matchInfo));

    // Remove both players from queue and save
    delete queue[player1Id];
    delete queue[player2Id];
    _saveQueue(queue);

    // Assign match to both players (starting)
    _setPlayerMatchAssignment(player1Id, { matchId: matchId, status: "starting", opponent: player2Id });
    _setPlayerMatchAssignment(player2Id, { matchId: matchId, status: "starting", opponent: player1Id });

    // Call VPS to start match server
    var vpsUrl = _getVpsUrl();
    var vpsApiKey = _getVpsApiKey(); // include secret so VPS can verify request is real
    if (!vpsApiKey) {
        log.error("Missing vps_api_key in Title Internal Data");
        return { success: false, error: "Server config missing vps_api_key" };
    }

    try {
        var payload = {
            match_id: matchId,
            player1_id: player1Id,
            player2_id: player2Id,
            player1_deck: player1Deck,
            player2_deck: player2Deck,
            api_key: vpsApiKey
        };

        var payloadStr = JSON.stringify(payload);
        var url = vpsUrl + "/api/start-match";

        log.info("DEBUG: Full URL = " + url);
        log.info("DEBUG: Payload = " + payloadStr);

        var httpResponse = http.request(
            url,
            "POST",
            "application/json",
            payloadStr,
            "",        // headers
            false,     // compress
            "utf-8",   // encoding
            true       // logRequestAndResponse
        );

        log.info("VPS responseCode: " + httpResponse.responseCode);
        log.info("VPS responseText: " + httpResponse.responseText);

        if (httpResponse.responseCode !== 200) {
            throw "VPS returned non-200: " + httpResponse.responseCode + " | " + httpResponse.responseText;
        }

        // Parse VPS response
        var vpsResponse = JSON.parse(httpResponse.responseText);
        log.info("VPS response parsed: " + JSON.stringify(vpsResponse));

        if (!vpsResponse.success) {
            throw "VPS returned success=false: " + httpResponse.responseText;
        }

        // Extract server info from VPS response
        var serverIp = vpsResponse.ip_address;
        var serverPort = vpsResponse.port;
        var serverStatus = vpsResponse.status;

        log.info("Server allocated: IP=" + serverIp + " Port=" + serverPort + " Status=" + serverStatus);

        // Update match info with server details
        matchInfo.status = "ready";
        matchInfo.serverIp = serverIp;
        matchInfo.serverPort = serverPort;
        matchInfo.readyTime = _nowIso();
        _setTitleInternal(matchKey, JSON.stringify(matchInfo));

        // Update both players' match assignments
        _setPlayerMatchAssignment(player1Id, {
            matchId: matchId,
            status: "ready",
            serverIp: serverIp,
            serverPort: serverPort,
            opponent: player2Id
        });

        _setPlayerMatchAssignment(player2Id, {
            matchId: matchId,
            status: "ready",
            serverIp: serverIp,
            serverPort: serverPort,
            opponent: player1Id
        });

        log.info("Match " + matchId + " is now READY!");
        log.info("Clients can connect to " + serverIp + ":" + serverPort);

    } catch (e) {
        log.error("Exception calling VPS: " + e);

        matchInfo.status = "failed";
        matchInfo.failReason = "Exception calling VPS: " + String(e);
        _setTitleInternal(matchKey, JSON.stringify(matchInfo));

        _setPlayerMatchAssignment(player1Id, { matchId: matchId, status: "failed", opponent: player2Id });
        _setPlayerMatchAssignment(player2Id, { matchId: matchId, status: "failed", opponent: player1Id });

        return { success: false, error: "Exception calling VPS: " + e };
    }

    return {
        success: true,
        matched: true,
        queued: false,
        matchId: matchId,
        message: "Match found! Server is ready.",
        serverIp: vpsResponse.ip_address,
        serverPort: vpsResponse.port,
        opponent: (requestingPlayerId === player1Id) ? p2Name : p1Name
    };
}
