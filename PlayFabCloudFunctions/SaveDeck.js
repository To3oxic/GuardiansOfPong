/**
 * PlayFab Cloud Function: SaveDeck
 * 
 * Server-side deck saving with full validation:
 * - Takes card names from client
 * - Validates all cards exist in player's collection
 * - Saves deck with card IDs (not names) to prevent tampering
 * - Returns deck with card IDs to client
 * 
 * Args:
 *   - deckName: string (name of the deck, e.g., "My Deck 1")
 *   - cardNames: array of strings (card names player selected, e.g., ["Boulder Guardian", "Fire Spell"])
 */

handlers.SaveDeck = function (args, context) {
    // Validate arguments
    if (!args.deckName) {
        return { success: false, error: "deckName is required" };
    }

    if (!args.cardNames || !Array.isArray(args.cardNames)) {
        return { success: false, error: "cardNames must be an array" };
    }

    if (args.cardNames.length === 0) {
        return { success: false, error: "Deck must contain at least one card" };
    }

    var currentPlayerId = context.playerId;

    try {
        // ===== STEP 1: Load player's card collection =====
        var userDataRequest = {
            PlayFabId: currentPlayerId,
            Keys: ["player_cards"]
        };

        var userDataResult = server.GetUserReadOnlyData(userDataRequest);

        if (!userDataResult.Data || !userDataResult.Data.player_cards) {
            return { success: false, error: "Player has no cards" };
        }

        var cardCollectionJson = userDataResult.Data.player_cards.Value;
        var cardCollection = JSON.parse(cardCollectionJson);

        // ===== STEP 2: Validate all card names and convert to IDs =====
        var cardIds = [];
        var cardDetails = [];  // For response

        for (var i = 0; i < args.cardNames.length; i++) {
            var cardName = args.cardNames[i];
            var foundCard = null;

            // Find card in player's collection by name
            for (var j = 0; j < cardCollection.cards.length; j++) {
                if (cardCollection.cards[j].cardName === cardName) {
                    foundCard = cardCollection.cards[j];
                    break;
                }
            }

            if (!foundCard) {
                return {
                    success: false,
                    error: "Card not found in player collection: " + cardName
                };
            }

            // Verify player actually owns this card (level >= 1)
            if (foundCard.level < 1) {
                return {
                    success: false,
                    error: "Card is locked and cannot be used in deck: " + cardName
                };
            }

            cardIds.push(foundCard.cardId);
            cardDetails.push({
                cardId: foundCard.cardId,
                cardName: foundCard.cardName,
                level: foundCard.level,
                quantity: foundCard.quantity
            });
        }

        // ===== STEP 3: Create deck object with card IDs =====
        var deckData = {
            DeckName: args.deckName,
            CardIds: cardIds,
            CardCount: cardIds.length,
            CreatedDate: new Date().toISOString(),
            LastModified: new Date().toISOString()
        };

        // ===== STEP 4: Save deck to PlayFab =====
        var updateDataRequest = {
            PlayFabId: currentPlayerId,
            Data: {
                "selected_deck": JSON.stringify(deckData)
            }
        };

        server.UpdateUserData(updateDataRequest);

        // ===== STEP 5: Return success with deck info =====
        return {
            success: true,
            message: "Deck saved successfully",
            deck: {
                deckName: deckData.DeckName,
                cardCount: cardDetails.length,
                cards: cardDetails,
                savedAt: deckData.LastModified
            }
        };

    } catch (error) {
        return {
            success: false,
            error: "Server error: " + error.toString()
        };
    }
};
