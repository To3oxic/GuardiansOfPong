/**
 * PlayFab Cloud Function: LevelUpCard
 * 
 * Server-side card leveling with full validation:
 * - Verifies player owns the card
 * - Validates card is not at max level
 * - Deducts currency from player account
 * - Increments card level
 * - Persists to PlayFab
 * 
 * Args:
 *   - cardId: string (card identifier)
 *   - currencyCost: int (amount of currency to deduct)
 *   - maxLevel: int (max level card can reach, default 5)
 */

handlers.LevelUpCard = function (args, context) {
    // Validate arguments
    if (!args.cardId) {
        return { success: false, error: "cardId is required" };
    }

    var currencyCost = args.currencyCost || 100;  // Default cost
    var maxLevel = args.maxLevel || 5;             // Default max level
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

        // ===== STEP 2: Find and validate the card =====
        var cardIndex = -1;
        var card = null;

        for (var i = 0; i < cardCollection.cards.length; i++) {
            if (cardCollection.cards[i].cardId === args.cardId) {
                cardIndex = i;
                card = cardCollection.cards[i];
                break;
            }
        }

        if (!card) {
            return { success: false, error: "Card not found in player collection" };
        }

        if (card.level >= maxLevel) {
            return { success: false, error: "Card is already at maximum level " + maxLevel };
        }

        // ===== STEP 3: Validate player has enough currency =====
        var playerProfileRequest = {
            PlayFabId: currentPlayerId,
            ProfileConstraints: {
                ShowVirtualCurrencies: true
            }
        };

        var playerProfile = server.GetPlayerProfile(playerProfileRequest);
        var virtualCurrencies = playerProfile.PlayerProfile.VirtualCurrencyBalances || {};
        var currentCurrency = virtualCurrencies.GD || 0;  // GD = Game Diamonds (adjust currency key as needed)

        if (currentCurrency < currencyCost) {
            return {
                success: false,
                error: "Insufficient currency. Need " + currencyCost + " but have " + currentCurrency
            };
        }

        // ===== STEP 4: Deduct currency =====
        var deductRequest = {
            PlayFabId: currentPlayerId,
            VirtualCurrency: "GD",  // Adjust if using different currency code
            Amount: currencyCost
        };

        server.SubtractUserVirtualCurrency(deductRequest);

        // ===== STEP 5: Level up the card =====
        card.level++;

        // ===== STEP 6: Save updated collection back to PlayFab =====
        var updateDataRequest = {
            PlayFabId: currentPlayerId,
            Data: {
                "player_cards": JSON.stringify(cardCollection)
            }
        };

        server.UpdateUserData(updateDataRequest);

        // ===== STEP 7: Return success with updated card data =====
        return {
            success: true,
            message: "Card leveled up successfully",
            card: card,
            newLevel: card.level,
            currencyDeducted: currencyCost,
            remainingCurrency: currentCurrency - currencyCost
        };

    } catch (error) {
        return {
            success: false,
            error: "Server error: " + error.toString()
        };
    }
};
