// PlayFab Cloud Script: GameFunctions (Internal Data version)
// Secure storage in Internal Data:
// - player_cards: { [cardId]: { cardId, cardName, level, quantity, ... } }
// - player_inventory: { gems, gold }
// - selected_deck: { deckName, cardNames, lastModified }

// ============================================================
// CARD DATABASE (ID-based system)
// ============================================================
// ID FORMAT PATTERN:
// - Cards: "c{number}" or "c{name}" (e.g., "c1" for ToxinBall, "c2" for BraveKnight)
// - Legends: "l{number}" or "l{name}" (e.g., "l1" for Xod, "l2" for Boulder, "l3" for Pyro)
// - Legend Stones: "s{number}" or "s{name}" (e.g., "s1" for ForestSpirit)
//
// EXAMPLE MAPPING (follow this pattern for all new items):
// Card IDs:       "c1" = ToxinBall, "c2" = BraveKnight, "c3" = Vanish, etc.
// Legend IDs:     "l1" = Xod, "l2" = Boulder, "l3" = Pyro, etc.
// Stone IDs:      "s1" = ForestSpirit, etc.
// 
// Name field matches the display name in your Unity data exactly.
// The ID here must match the CardData.id field in Unity!
//
var CARD_DB = {
    // CARDS (Abilities/Spells)
    "c1": { name: "ToxinBall", rarity: "RARE" },
    "c7": { name: "BraveKnight", rarity: "COMMON" },
    "c2": { name: "Vanish", rarity: "COMMON" },
    "c3": { name: "LightningBall", rarity: "EPIC" },
    "c9": { name: "AncientSpirit", rarity: "EPIC" },
    "c8": { name: "AngryBear", rarity: "RARE" },
    "c6": { name: "ForestMonsters", rarity: "COMMON" },
    "c5": { name: "StoneSentinel", rarity: "RARE" },
    "c4": { name: "Tree", rarity: "COMMON" },

    // LEGENDS (Guardians)
    "g1": { name: "Xod", rarity: "EPIC" },
    "g2": { name: "Boulder", rarity: "COMMON" },
    "g3": { name: "Pyro", rarity: "RARE" },

    // LEGEND STONES (passive abilities)
    "l1": { name: "ForestSpirit", rarity: "COMMON" },
};

// Rarity list you want to support
var RARITIES = ["COMMON", "RARE", "EPIC"];

// Build index: rarity -> [cardIds]
var CARDS_BY_RARITY = (function buildIndex() {
    var idx = {};
    for (var i = 0; i < RARITIES.length; i++) idx[RARITIES[i]] = [];

    for (var id in CARD_DB) {
        if (!CARD_DB.hasOwnProperty(id)) continue;
        var r = CARD_DB[id].rarity;
        if (!idx[r]) idx[r] = [];
        idx[r].push(id);
    }
    return idx;
})();

// ============================================================
// LOOTBOX CONFIG (levels 1..10) - edit these numbers
// quantityBudget = total "card value" budget
// rarityChances must sum to 100 (or close; we normalize anyway)
// guaranteed: { RARITY: count } means at least that many *rolls* of that rarity
// baseGold + goldPercommonCopy * (Common copies awarded)
// ============================================================
var LOOTBOX_CONFIG = {
    1: { quantityBudget: 10, rarityChances: { COMMON: 100, }, guaranteed: {}, baseGold: 50, goldPercommonCopy: 15 },
    2: { quantityBudget: 18, rarityChances: { COMMON: 90, RARE: 10 }, guaranteed: {}, baseGold: 70, goldPercommonCopy: 18 },
    3: { quantityBudget: 30, rarityChances: { COMMON: 70, RARE: 30 }, guaranteed: {}, baseGold: 90, goldPercommonCopy: 20 },
    4: { quantityBudget: 45, rarityChances: { COMMON: 65, RARE: 35 }, guaranteed: { RARE: 1 }, baseGold: 120, goldPercommonCopy: 22 },
    5: { quantityBudget: 80, rarityChances: { COMMON: 60, RARE: 39, EPIC: 1 }, guaranteed: { RARE: 1 }, baseGold: 150, goldPercommonCopy: 25 },
    6: { quantityBudget: 100, rarityChances: { COMMON: 55, RARE: 43, EPIC: 2 }, guaranteed: { RARE: 1 }, baseGold: 180, goldPercommonCopy: 28 },
    7: { quantityBudget: 130, rarityChances: { COMMON: 50, RARE: 45, EPIC: 5 }, guaranteed: { EPIC: 1 }, baseGold: 220, goldPercommonCopy: 30 },
    8: { quantityBudget: 200, rarityChances: { COMMON: 45, RARE: 48, EPIC: 7 }, guaranteed: { RARE: 1, EPIC: 1 }, baseGold: 260, goldPercommonCopy: 33 },
    9: { quantityBudget: 260, rarityChances: { COMMON: 40, RARE: 51, EPIC: 9 }, guaranteed: { RARE: 2, EPIC: 1 }, baseGold: 320, goldPercommonCopy: 36 },
    10: { quantityBudget: 320, rarityChances: { COMMON: 40, RARE: 50, EPIC: 10 }, guaranteed: { RARE: 2, EPIC: 2 }, baseGold: 400, goldPercommonCopy: 40 }
};

// Cost rules from your message
var RARITY_COST = {
    COMMON: 1,
    RARE: 7,
    EPIC: 20
};
// ==================== HELPERS ====================
function _nowIso() {
    return new Date().toISOString();
}

function _toInt(value, fallback) {
    var n = parseInt(value, 10);
    return isNaN(n) ? fallback : n;
}

function _safeJsonParse(str, fallbackObj) {
    try {
        if (!str) return fallbackObj;
        return JSON.parse(str);
    } catch (e) {
        log.error("JSON parse failed: " + e);
        return fallbackObj;
    }
}

function _getPlayFabId(context) {
    // Classic CloudScript: currentPlayerId is the reliable identifier for server.* player APIs
    if (typeof currentPlayerId !== "undefined" && currentPlayerId) return currentPlayerId;
    if (context && context.currentPlayerId) return context.currentPlayerId;
    throw "No valid PlayFabId in CloudScript context";
}

function _getInternalData(playFabId, keys) {
    var res = server.GetUserInternalData({ PlayFabId: playFabId, Keys: keys });
    return (res && res.Data) ? res.Data : {};
}

function _getInternalJson(dataMap, key, fallbackObj) {
    var entry = dataMap && dataMap[key];
    var str = (entry && entry.Value) ? entry.Value : null;
    return _safeJsonParse(str, fallbackObj);
}

function _setInternalJson(playFabId, objMap) {
    // objMap: { key: object/string }
    var data = {};
    for (var k in objMap) {
        if (!objMap.hasOwnProperty(k)) continue;
        data[k] = (typeof objMap[k] === "string") ? objMap[k] : JSON.stringify(objMap[k]);
    }
    server.UpdateUserInternalData({ PlayFabId: playFabId, Data: data });
}

function _randInt(min, max) {
    // inclusive
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function _pickWeighted(weightsObj) {
    // weightsObj: { rarity: weight, ... } weights can be any positive numbers
    var total = 0;
    for (var k in weightsObj) if (weightsObj.hasOwnProperty(k) && weightsObj[k] > 0) total += weightsObj[k];
    if (total <= 0) return null;

    var roll = Math.random() * total;
    var acc = 0;
    for (var r in weightsObj) {
        if (!weightsObj.hasOwnProperty(r) || weightsObj[r] <= 0) continue;
        acc += weightsObj[r];
        if (roll <= acc) return r;
    }
    return null;
}

function _getAllowedRarities(remainingValue) {
    // Your rules:
    // - if remaining < 7 => no RARE
    // - if remaining < 20 => no EPIC
    var allowed = {};
    for (var i = 0; i < RARITIES.length; i++) {
        var rar = RARITIES[i];
        var cost = RARITY_COST[rar] || 1;

        if (rar === "RARE" && remainingValue < 7) continue;
        if (rar === "EPIC" && remainingValue < 20) continue;
        if (remainingValue < cost) continue;

        allowed[rar] = true;
    }
    return allowed;
}

// ==================== SAVE DECK ====================
handlers.saveDeck = function (args, context) {
    if (!args || !args.deckName || !args.cardIds || !Array.isArray(args.cardIds)) {
        return { success: false, error: "Missing deckName or cardIds (array)" };
    }

    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        log.error("saveDeck: " + e);
        return { success: false, error: "No valid player context" };
    }

    var dataMap = _getInternalData(playFabId, ["player_cards"]);
    var cards = _getInternalJson(dataMap, "player_cards", {});

    if (!cards || Object.keys(cards).length === 0) {
        return { success: false, error: "Player has no cards" };
    }

    // Separate regular cards from guardian and legend stone
    var regularCards = [];
    var guardianCard = null;
    var legendStone = null;

    // Validate each cardId in deck is owned and categorize them
    for (var i = 0; i < args.cardIds.length; i++) {
        var cardId = args.cardIds[i];
        var found = false;

        // Check if this card exists in CARD_DB
        if (!CARD_DB[cardId]) {
            return { success: false, error: "Card not found in database: " + cardId };
        }

        // Check if player owns this card
        if (cards[cardId] && cards[cardId].owned === true && _toInt(cards[cardId].level, 1) >= 1) {
            found = true;
        }

        if (!found) {
            return { success: false, error: "Player doesn't own card with ID: " + cardId };
        }

        // Categorize the card
        if (cardId.startsWith("g")) {
            // Guardian card (e.g., "g1", "g2", "g3")
            guardianCard = {
                id: cardId,
                registry_name: cardId,
                card_type: "guardian",
                level: _toInt(cards[cardId].level, 1)
            };
        } else if (cardId.startsWith("s")) {
            // Legend stone (e.g., "s1", "s2")
            legendStone = {
                id: cardId,
                registry_name: cardId,
                card_type: "stone",
                level: _toInt(cards[cardId].level, 1)
            };
        } else {
            // Regular card
            regularCards.push({
                id: cardId,
                registry_name: cardId,
                card_type: "spell",
                level: _toInt(cards[cardId].level, 1)
            });
        }
    }

    var selectedDeck = {
        deckName: args.deckName,
        cards: regularCards,
        guardian_card: guardianCard,
        legend_stone: legendStone,
        lastModified: _nowIso()
    };

    _setInternalJson(playFabId, { "selected_deck": selectedDeck });

    return {
        success: true,
        message: "Deck saved successfully",
        deckName: args.deckName,
        cardCount: regularCards.length,
        guardianCardId: guardianCard ? guardianCard.id : null,
        legendStoneId: legendStone ? legendStone.id : null,
        savedDeck: selectedDeck
    };
};



// ============================================================
// CARD LEVELING SYSTEM (Internal Data)
// ============================================================
handlers.LevelUpCard = function (args, context) {
    if (!args || !args.cardId) {
        return { success: false, error: "Missing cardId" };
    }

    var playFabId = _getPlayFabId(context);
    var cardId = args.cardId;

    var maxLevel = _toInt(args.maxLevel, 5);
    if (maxLevel < 1) maxLevel = 1;

    // Load internal data
    var dataMap = _getInternalData(playFabId, ["player_cards", "player_inventory"]);

    var playerCards = _getInternalJson(dataMap, "player_cards", {});
    var playerInventory = _getInternalJson(dataMap, "player_inventory", { gems: 0, gold: 0 });

    // Normalize
    playerInventory.gems = _toInt(playerInventory.gems, 0);
    playerInventory.gold = _toInt(playerInventory.gold, 0);

    var card = playerCards[cardId];
    if (!card) {
        return { success: false, error: "Card not found in collection" };
    }

    card.level = _toInt(card.level, 1);
    card.quantity = _toInt(card.quantity, 0);

    if (card.level >= maxLevel) {
        return { success: false, error: "Card is already at maximum level" };
    }

    // Cost table (Level N -> N+1)
    var levelingCosts = {
        1: { gems: 100, cardCopies: 2 },
        2: { gems: 200, cardCopies: 2 },
        3: { gems: 300, cardCopies: 3 },
        4: { gems: 500, cardCopies: 5 }
    };

    var currentLevel = card.level;
    var costs = levelingCosts[currentLevel];

    if (!costs) {
        return { success: false, error: "Invalid level for upgrade" };
    }

    var gemsCost = _toInt(costs.gems, 0);
    var copiesCost = _toInt(costs.cardCopies, 0);

    if (playerInventory.gems < gemsCost) {
        return {
            success: false,
            error: "Not enough gems",
            required_gems: gemsCost,
            current_gems: playerInventory.gems
        };
    }

    if (card.quantity < copiesCost) {
        return {
            success: false,
            error: "Not enough card copies",
            required_copies: copiesCost,
            current_copies: card.quantity
        };
    }

    // Perform upgrade
    playerInventory.gems -= gemsCost;
    card.quantity -= copiesCost;
    card.level += 1;
    card.lastUpgraded = _nowIso();

    playerCards[cardId] = card;

    // Save internal data back
    _setInternalJson(playFabId, {
        "player_cards": playerCards,
        "player_inventory": playerInventory
    });

    return {
        success: true,
        message: "Card leveled up successfully",
        card: card,
        inventory: playerInventory,
        levelingCost: { gems: gemsCost, cardCopies: copiesCost }
    };
};

// ==================== INITIALIZE PLAYER CARDS ====================
handlers.initializePlayerCards = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        log.error("initializePlayerCards: " + e);
        return { success: false, error: "No valid player context" };
    }

    var dataMap = _getInternalData(playFabId, ["player_cards", "player_inventory"]);
    var existingCards = _getInternalJson(dataMap, "player_cards", {});

    if (existingCards && Object.keys(existingCards).length > 0) {
        return {
            success: true,
            message: "Player cards already initialized",
            newPlayer: false,
            cardCount: Object.keys(existingCards).length
        };
    }

    // Build initial cards
    var playerCards = {};
    if (args && args.cards && Array.isArray(args.cards)) {
        for (var i = 0; i < args.cards.length; i++) {
            var c = args.cards[i];
            if (!c || !c.cardId || !c.cardName) continue;

            playerCards[c.cardId] = {
                cardId: c.cardId,
                cardName: c.cardName,
                level: 1,
                owned: true,
                quantity: _toInt(c.quantity, 1),
                timesAcquired: _toInt(c.timesAcquired, 1),
                acquiredDate: _nowIso()
            };
        }
    }

    // Initialize inventory (optional args override)
    var inv = _getInternalJson(dataMap, "player_inventory", { gems: 0, gold: 0 });
    inv.gems = _toInt((args && args.startGems), _toInt(inv.gems, 0));
    inv.gold = _toInt((args && args.startGold), _toInt(inv.gold, 0));

    _setInternalJson(playFabId, {
        "player_cards": playerCards,
        "player_inventory": inv
    });

    return {
        success: true,
        message: "Player initialized",
        newPlayer: true,
        cardCount: Object.keys(playerCards).length,
        inventory: inv
    };

};

// ==================== GET PLAYER STATE ====================
handlers.getPlayerState = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        log.error("getPlayerState: " + e);
        return { success: false, error: "No valid player context" };
    }

    var dataMap = _getInternalData(playFabId, [
        "player_cards",
        "selected_deck",
        "player_inventory"
    ]);

    var playerCards = _getInternalJson(dataMap, "player_cards", {});
    var selectedDeck = _getInternalJson(dataMap, "selected_deck", null);
    var inventory = _getInternalJson(dataMap, "player_inventory", { gems: 0, gold: 0 });

    // OPTIONAL: sanitize cards (example)
    // remove internal-only fields if you ever add any
    var cardsArray = [];
    for (var cardId in playerCards) {
        if (!playerCards.hasOwnProperty(cardId)) continue;
        cardsArray.push(playerCards[cardId]);
    }

    return {
        success: true,
        cards: cardsArray,       // list for UI
        cardsById: playerCards,  // dictionary (useful for logic)
        activeDeck: selectedDeck,
        inventory: inventory
    };
};


// ============================================================
// OPEN LOOT BOX (boxLevel 1..10)
// - Uses InternalData (secure)
// - Awards cards (player_cards) + gold/gems (player_inventory)
// - Anti-cheat: server rolls everything
// ============================================================
handlers.openLootBox = function (args, context) {
    if (!args || args.boxLevel === undefined || args.boxLevel === null) {
        return { success: false, error: "Missing boxLevel" };
    }

    var level = _toInt(args.boxLevel, -1);
    if (level < 1 || level > 10) {
        return { success: false, error: "boxLevel must be between 1 and 10" };
    }

    var config = LOOTBOX_CONFIG[level];
    if (!config) return { success: false, error: "Lootbox config missing for level " + level };

    var playFabId = _getPlayFabId(context);

    // Load player state from InternalData
    var dataMap = _getInternalData(playFabId, ["player_cards", "player_inventory"]);

    var playerCards = _getInternalJson(dataMap, "player_cards", {});
    var inventory = _getInternalJson(dataMap, "player_inventory", { gems: 0, gold: 0 });

    inventory.gems = _toInt(inventory.gems, 0);
    inventory.gold = _toInt(inventory.gold, 0);

    // Quantity budget = "card value" budget
    var remaining = _toInt(config.quantityBudget, 0);
    if (remaining <= 0) return { success: false, error: "Invalid quantityBudget for level " + level };

    // Track loot
    var awarded = {}; // cardId -> copies
    var awardedcommonCopies = 0;

    // --------- Guarantees (at least 1 card of certain rarity at some levels) ----------
    // Guarantee means: do one roll of that rarity with 1 copy (cost applies).
    var guaranteed = config.guaranteed || {};
    for (var gr in guaranteed) {
        if (!guaranteed.hasOwnProperty(gr)) continue;

        var guaranteeCount = _toInt(guaranteed[gr], 0);
        if (guaranteeCount <= 0) continue;

        for (var g = 0; g < guaranteeCount; g++) {
            var costG = RARITY_COST[gr] || 1;

            // Enforce your "don't roll if remaining below threshold"
            if (gr === "RARE" && remaining < 7) break;
            if (gr === "EPIC" && remaining < 20) break;

            if (remaining < costG) break;

            var poolG = CARDS_BY_RARITY[gr] || [];
            if (poolG.length === 0) {
                // If no cards exist for this rarity, skip guarantee (or error; your choice)
                log.error("No cards available for guaranteed rarity: " + gr);
                continue;
            }

            var chosenIdG = poolG[_randInt(0, poolG.length - 1)];

            // award exactly 1 copy for guarantee (simple + predictable)
            awarded[chosenIdG] = (awarded[chosenIdG] || 0) + 1;
            remaining -= costG;

            if (gr === "COMMON") awardedcommonCopies += 1;
        }
    }

    // --------- Main rolling loop ----------
    // Keep rolling until we can't afford any eligible rarity
    var safety = 0;
    while (remaining > 0 && safety < 500) {
        safety++;

        // Build allowed + weighted rarity table for this remaining budget
        var allowed = _getAllowedRarities(remaining);

        // Filter configured chances by allowed rarities + by existing pools
        var weights = {};
        var chances = config.rarityChances || {};
        var any = false;

        for (var rar in chances) {
            if (!chances.hasOwnProperty(rar)) continue;
            if (!allowed[rar]) continue;

            var pool = CARDS_BY_RARITY[rar] || [];
            if (pool.length === 0) continue;

            var w = chances[rar];
            if (w > 0) {
                weights[rar] = w;
                any = true;
            }
        }

        if (!any) break;

        // Roll rarity
        var rarity = _pickWeighted(weights);
        if (!rarity) break;

        var cost = RARITY_COST[rarity] || 1;

        // Apply your thresholds again (strict)
        if (rarity === "RARE" && remaining < 7) continue;
        if (rarity === "EPIC" && remaining < 20) continue;

        // If we can't afford even 1 copy, stop
        if (remaining < cost) break;

        var poolR = CARDS_BY_RARITY[rarity] || [];
        if (poolR.length === 0) continue;

        // Pick a card of that rarity
        var chosenId = poolR[_randInt(0, poolR.length - 1)];

        // Roll amount (copies) <= remaining budget / cost
        var maxCopies = Math.floor(remaining / cost);
        if (maxCopies <= 0) break;

        var copies = _randInt(1, maxCopies);

        awarded[chosenId] = (awarded[chosenId] || 0) + copies;
        remaining -= copies * cost;

        if (rarity === "COMMON") awardedcommonCopies += copies;
    }

    // --------- Apply loot to player_cards (InternalData) ----------
    var grantedCardsDetailed = [];
    for (var cardId in awarded) {
        if (!awarded.hasOwnProperty(cardId)) continue;

        var copiesToAdd = _toInt(awarded[cardId], 0);
        if (copiesToAdd <= 0) continue;

        var meta = CARD_DB[cardId];
        if (!meta) {
            // If cardId not in DB, skip (or error). Better to skip & log.
            log.error("Awarded unknown cardId (missing in CARD_DB): " + cardId);
            continue;
        }

        // Ensure card exists in player collection
        if (!playerCards[cardId]) {
            playerCards[cardId] = {
                cardId: cardId,
                cardName: meta.name,
                rarity: meta.rarity,
                level: 1,
                owned: true,
                quantity: 0,
                timesAcquired: 0,
                acquiredDate: _nowIso()
            };
        }

        var pc = playerCards[cardId];
        pc.owned = true;
        pc.cardName = pc.cardName || meta.name;
        pc.rarity = pc.rarity || meta.rarity;

        pc.quantity = _toInt(pc.quantity, 0) + copiesToAdd;
        pc.timesAcquired = _toInt(pc.timesAcquired, 0) + copiesToAdd;

        playerCards[cardId] = pc;

        grantedCardsDetailed.push({
            cardId: cardId,
            cardName: pc.cardName,
            rarity: pc.rarity,
            copies: copiesToAdd
        });
    }

    // --------- Gold reward scaling with Common copies ----------
    var baseGold = _toInt(config.baseGold, 0);
    var goldPercommon = _toInt(config.goldPercommonCopy, 0);

    var goldGranted = baseGold + (awardedcommonCopies * goldPercommon);
    if (goldGranted < 0) goldGranted = 0;

    inventory.gold += goldGranted;

    // (Optional) If you also want gems granted by box level, add config.gems and do:
    // var gemsGranted = _toInt(config.gems, 0);
    // inventory.gems += gemsGranted;

    // Save InternalData back
    _setInternalJson(playFabId, {
        "player_cards": playerCards,
        "player_inventory": inventory
    });

    return {
        success: true,
        message: "Loot box opened",
        boxLevel: level,
        configUsed: {
            quantityBudget: config.quantityBudget,
            rarityChances: config.rarityChances,
            guaranteed: config.guaranteed || {},
            baseGold: baseGold,
            goldPercommonCopy: goldPercommon
        },
        rewards: {
            cards: grantedCardsDetailed,
            goldGranted: goldGranted
            // gemsGranted: gemsGranted
        },
        inventory: inventory
    };
};


/// PlayFab CloudScript: Matchmaking System (Queue + VPS allocator)

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

function _stringifySafe(x) {
    try {
        if (x === null || x === undefined) return "" + x;
        if (typeof x === "string") return x;
        return JSON.stringify(x);
    } catch (e) {
        return "" + x;
    }
}

function _saveVpsResponse(matchId, payloadObj) {
    var key = "vps_response_" + matchId;
    _setTitleInternal(key, JSON.stringify(payloadObj));
}

function _getVpsResponse(matchId) {
    var key = "vps_response_" + matchId;
    var raw = _getTitleInternal(key);
    return _safeJsonParse(raw, null);
}

function _nowIso() {
    return new Date().toISOString();
}

function _stringifyError(e) {
    try {
        if (e === null || e === undefined) return "" + e;
        if (typeof e === "string") return e;
        if (e.message) return e.message + " | " + JSON.stringify(e);
        return JSON.stringify(e);
    } catch (ex) {
        return "Unstringifiable error: " + ("" + e);
    }
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
        var matchId = assign.matchId;
        var vpsUrl = _getVpsUrl();
        var statusUrl = vpsUrl + "/api/match/" + matchId + "/status";

        try {
            log.info("Checking match status with VPS: " + statusUrl);
            var statusResponse = http.request(statusUrl, "get", "", "application/json", {});
            log.info("VPS status response: " + statusResponse);

            var vpsStatus = _safeJsonParse(statusResponse, null);
            if (vpsStatus && vpsStatus.success) {
                return {
                    success: true,
                    matched: true,
                    matchId: matchId,
                    status: vpsStatus.status,
                    serverIp: vpsStatus.ip_address,
                    serverPort: vpsStatus.port,
                    opponent: assign.opponent
                };
            } else {
                log.error("VPS returned error: " + statusResponse);
                return { success: false, error: "Failed to get match status from VPS" };
            }
        } catch (e) {
            log.error("Exception checking match status: " + e);
            return { success: false, error: "Error checking match status: " + e };
        }
    }

    // Still in queue?
    var queue = _getQueue();
    if (queue[playFabId]) {
        return { success: true, matched: false, message: "Still in queue" };
    }

    return { success: true, matched: false, message: "Not queued" };
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
    var vpsApiKey = _getVpsApiKey();
    var vpsUrl = _getVpsUrl();               // e.g. "http://185.252.233.117:5000"
    var url = vpsUrl + "/api/start-match";   // ✅ include path
    var headers = {};                        // ✅ MUST be object (not "")

    var payloadObj = {
        match_id: matchId,
        player1_id: player1Id,
        player2_id: player2Id,
        player1_deck: player1Deck,
        player2_deck: player2Deck,
        api_key: vpsApiKey
    };

    var body = JSON.stringify(payloadObj);

    try {
        log.info("Calling VPS to start match: " + url);

        // ✅ Call VPS to start server (returns immediately)
        var responseText = http.request(
            url,
            "post",
            body,
            "application/json",
            headers
        );

        // Save response for debugging
        _saveVpsResponse(matchId, {
            savedAt: _nowIso(),
            url: url,
            requestBodyPreview: body.substring(0, 2000),
            responseText: responseText
        });

        log.info("VPS start-match response: " + responseText);

        // Parse initial VPS response
        var vpsResponse = _safeJsonParse(responseText, null);
        log.info("VPS response parsed: " + JSON.stringify(vpsResponse));

        if (!vpsResponse || !vpsResponse.success) {
            throw "VPS returned success=false: " + responseText;
        }

        // VPS has accepted the request and is starting the server
        // Status will still be "starting" from VPS
        // Clients will poll checkMatchStatus which will poll VPS for readiness
        var serverIp = vpsResponse.ip_address;
        var serverPort = vpsResponse.port;

        // Update match info with initial response
        matchInfo.status = "starting";
        matchInfo.serverIp = serverIp;
        matchInfo.serverPort = serverPort;
        matchInfo.vpsStartedAt = _nowIso();
        _setTitleInternal(matchKey, JSON.stringify(matchInfo));

        log.info("Match " + matchId + " server started asynchronously on " + serverIp + ":" + serverPort + " (status: " + vpsResponse.status + ")");

        // Update both players' match assignments to show server details
        // (status will remain "starting" - clients will update it when they poll)
        _setPlayerMatchAssignment(player1Id, {
            matchId: matchId,
            status: "starting",
            serverIp: serverIp,
            serverPort: serverPort,
            opponent: player2Id
        });

        _setPlayerMatchAssignment(player2Id, {
            matchId: matchId,
            status: "starting",
            serverIp: serverIp,
            serverPort: serverPort,
            opponent: player1Id
        });

    } catch (e) {
        var errStr = (typeof e === "string") ? e : JSON.stringify(e);

        _saveVpsResponse(matchId, {
            savedAt: _nowIso(),
            url: url,
            requestBodyPreview: body.substring(0, 2000),
            exception: errStr
        });

        log.error("Exception calling VPS: " + errStr);

        // Mark match failed so players can re-queue
        matchInfo.status = "failed";
        matchInfo.failReason = errStr;
        _setTitleInternal(matchKey, JSON.stringify(matchInfo));

        _setPlayerMatchAssignment(player1Id, { matchId: matchId, status: "failed", opponent: player2Id });
        _setPlayerMatchAssignment(player2Id, { matchId: matchId, status: "failed", opponent: player1Id });

        return { success: false, error: "Exception calling VPS: " + errStr };
    }

    return {
        success: true,
        matched: true,
        queued: false,
        matchId: matchId,
        serverIp: matchInfo.serverIp,
        serverPort: matchInfo.serverPort,
        message: "Match found! Server is ready.",
        opponent: (requestingPlayerId === player1Id) ? p2Name : p1Name
    };
}

/**
 * Player-callable debug function:
 * Logs the stored VPS response for the player's current match.
 * Optionally returns it too.
 */
handlers.logLastVpsResponse = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        return { success: false, error: "" + e };
    }

    var assign = _getPlayerMatchAssignment(playFabId);
    if (!assign || !assign.matchId) {
        log.info("logLastVpsResponse: player has no active match assignment: " + playFabId);
        return { success: false, error: "Player has no active match" };
    }

    var matchId = assign.matchId;
    var vpsResp = _getVpsResponse(matchId);

    if (!vpsResp) {
        log.info("logLastVpsResponse: no VPS response saved for matchId=" + matchId);
        return { success: false, error: "No VPS response saved for this match yet", matchId: matchId };
    }

    // Log it so you can see it in PlayFab execution logs
    log.info("=== VPS SAVED RESPONSE for match " + matchId + " ===");
    log.info(_stringifySafe(vpsResp));
    log.info("=== END VPS SAVED RESPONSE ===");

    // Returning can help Unity show it too
    return {
        success: true,
        matchId: matchId,
        vpsResponse: vpsResp
    };
};

handlers.unassignPlayerFromMatch = function (args, context) {
    var playFabId;
    try {
        playFabId = _getPlayFabId(context);
    } catch (e) {
        return { success: false, error: "" + e };
    }

    // Remove player match assignment
    var assignmentKey = "player_match_" + playFabId;
    var rawAssign = _getTitleInternal(assignmentKey);

    if (!rawAssign) {
        return {
            success: true,
            message: "Player had no active match assignment",
            unassigned: false
        };
    }

    var assignment = _safeJsonParse(rawAssign, null);

    // Clear assignment
    _setTitleInternal(assignmentKey, "");

    // Also remove player from matchmaking queue if present
    var queue = _getQueue();
    if (queue[playFabId]) {
        delete queue[playFabId];
        _saveQueue(queue);
    }

    log.info("Player " + playFabId + " unassigned from match");

    return {
        success: true,
        unassigned: true,
        previousMatchId: assignment ? assignment.matchId : null
    };
};



handlers.testExternalHttp = function (args, context) {
    try {
        var url = "https://httpbin.org/status/200";
        var method = "get";
        var body = "";
        var contentType = "text/plain";
        var headers = {}; // IMPORTANT: object, not ""

        var responseString = http.request(url, method, body, contentType, headers);

        // responseString is the response body (string)
        log.info("httpbin response: " + responseString);

        return { success: true, response: responseString };
    } catch (e) {
        // show full object
        var err = (typeof e === "string") ? e : JSON.stringify(e);
        log.error("testExternalHttp error: " + err);
        return { success: false, error: err };
    }
};