// PlayFab Cloud Function: InitializePlayerCards
// Purpose: Initialize a new player's card collection with all cards from DeckRegistry
// Called on first login to populate player inventory

handlers.initializePlayerCards = function (args, context) {
    var dataApi = require("PlayFab").data;

    // Get or create the player's card collection
    var getRequest = {
        Entity: {
            Id: context.entityId,
            Type: context.entityType
        },
        Keys: ["player_cards"]
    };

    var existing = dataApi.GetObjects(getRequest);

    // If cards already exist, don't reinitialize
    if (existing && existing.Objects && existing.Objects.player_cards &&
        Object.keys(existing.Objects.player_cards.DataObject).length > 0) {
        return {
            success: true,
            message: "Player cards already initialized",
            newPlayer: false,
            cardCount: Object.keys(existing.Objects.player_cards.DataObject).length
        };
    }

    // Initialize with all cards from args
    // Format: { cards: [ { cardId: "card_001", cardName: "Fireball", ... }, ... ] }
    var playerCards = {};

    if (args.cards && Array.isArray(args.cards)) {
        for (var i = 0; i < args.cards.length; i++) {
            var card = args.cards[i];
            playerCards[card.cardId] = {
                cardId: card.cardId,
                cardName: card.cardName,
                level: 1,        // Start at level 1 (unlocked)
                owned: true,
                quantity: 1,      // Start with 1 copy
                timesAcquired: 1,
                acquiredDate: new Date().toISOString()
            };
        }
    }

    // Save player cards
    var setRequest = {
        Entity: {
            Id: context.entityId,
            Type: context.entityType
        },
        Objects: [
            {
                ObjectName: "player_cards",
                DataObject: playerCards
            }
        ]
    };

    var result = dataApi.SetObjects(setRequest);

    if (result) {
        return {
            success: true,
            message: "Player cards initialized with all cards",
            newPlayer: true,
            cardCount: Object.keys(playerCards).length
        };
    } else {
        return {
            success: false,
            message: "Failed to initialize player cards"
        };
    }
};


