# PlayFab Card Ownership - Quick Integration Summary

## What Was Created

### 1. **CardOwnershipModels.cs**
Data classes for the card ownership system:
- `PlayerCardOwnership` - Individual card with ownership status + level
- `PlayerCardCollection` - List of all player's cards (saved to PlayFab)
- `CardLibrary` - Master list of all possible cards in the game
- `CardLibraryEntry` - Single card in the master library

### 2. **PlayFabBackendManager Extensions**
New methods for card management:
- `LoadPlayerCardCollection()` - Fetch player's owned cards from PlayFab
- `LoadCardLibrary()` - Load master card list from DeckRegistry
- `OwnsCard(cardId)` - Check if player owns a specific card
- `GetPlayerCardLevel(cardId)` - Get level of owned card
- `AwardCardToPlayer()` - Give player a new card (saves automatically)
- `GetAllCardsWithOwnershipStatus()` - Get list of all cards with owned/locked status

Internal methods:
- `InitializeNewPlayerCards()` - Auto-called for first-time players
- `SaveCardCollectionToPlayFab()` - Save player's collection
- `LoadCardLibraryFromDeckRegistry()` - Load from local DeckRegistry

### 3. **DeckSelectionUIPlayFab.cs**
New UI script that:
- Loads player's card collection on startup
- Shows ALL cards from master library
- **Greyed out** cards player doesn't own
- **White** cards player owns (interactable)
- Prevents selection of locked cards
- Validates ownership before allowing selection

---

## How It Works

### Data Flow

```
PlayFab Database
    ↓
LoadPlayerCardCollection() 
    ↓ (JSON)
PlayerCardCollection {
    cards: [
        { cardId: "brave_knight", owned: true, level: 3 },
        { cardId: "tree", owned: true, level: 1 }
    ]
}
    ↓
DeckSelectionUIPlayFab displays:
    [✓ Brave Knight] (white, clickable)
    [✓ Tree] (white, clickable)
    [✗ Stone Sentinel] (grey, locked)
```

### Storage in PlayFab

Player Data location: **player_cards** (JSON string)

```json
{
    "cards": [
        {
            "cardId": "brave_knight",
            "cardName": "Brave Knight",
            "level": 3,
            "owned": true,
            "timesAcquired": 2,
            "acquiredDate": "2026-01-06T10:30:00"
        },
        {
            "cardId": "tree",
            "cardName": "Tree",
            "level": 1,
            "owned": true,
            "timesAcquired": 1,
            "acquiredDate": "2026-01-06T09:00:00"
        }
    ]
}
```

---

## Step-by-Step Integration

### Step 1: Update Login Flow
In **MainMenuController.cs** after player logs in:

```csharp
void OnPlayFabLoginSuccess()
{
    // PlayFab login already happened
    
    // NOW load player's card collection
    PlayFabBackendManager.Instance.LoadPlayerCardCollection((collection) =>
    {
        Debug.Log($"Player owns {collection.cards.Count} cards");
        
        // Proceed to deck selection
        GoToDeckSelection();
    });
}
```

### Step 2: Use New UI
Create deck selection scene with **DeckSelectionUIPlayFab** instead of DeckSelectionUI:

```csharp
// Inspector Setup:
// - cardListContainer = Transform
// - cardButtonPrefab = GameObject  
// - legendListContainer = Transform
// - stoneListContainer = Transform
// - startMatchButton = Button
// - connectionUI = GameObject
// - deckSelectionUI = GameObject
```

The UI automatically:
- Loads card collection on connect
- Shows all available cards
- Greys out locked cards
- Validates ownership on selection

### Step 3: Award Cards (When Earned)
When player wins a match or earns a reward:

```csharp
void OnPlayerWinMatch()
{
    // Award random card
    var randomCard = GetRandomCard();
    
    PlayFabBackendManager.Instance.AwardCardToPlayer(
        cardId: randomCard.id,
        cardName: randomCard.name,
        level: 1,
        callback: (success) =>
        {
            if (success)
            {
                ShowNotification($"You earned: {randomCard.name}!");
                // Card will appear in next deck selection
            }
        }
    );
}
```

---

## Key API Methods

### Check Ownership
```csharp
bool owns = PlayFabBackendManager.Instance.OwnsCard("brave_knight");
```

### Get Level
```csharp
int level = PlayFabBackendManager.Instance.GetPlayerCardLevel("brave_knight");
```

### Get All Cards (With Status)
```csharp
var allCards = PlayFabBackendManager.Instance.GetAllCardsWithOwnershipStatus();
foreach (var card in allCards)
{
    Debug.Log($"{card.cardName}: {(card.owned ? "✓" : "✗")}");
}
```

### Award New Card
```csharp
PlayFabBackendManager.Instance.AwardCardToPlayer(
    "new_card_id", 
    "New Card Name", 
    level: 1,
    callback: (success) => Debug.Log(success ? "Awarded!" : "Failed")
);
```

### Load Collection (Usually done auto, but manual load)
```csharp
PlayFabBackendManager.Instance.LoadPlayerCardCollection((collection) =>
{
    Debug.Log($"Loaded {collection.cards.Count} cards");
});
```

---

## Customization

### Set Starter Cards
In `LoadCardLibraryFromDeckRegistry()`, mark which cards all new players get:

```csharp
// Give 3 random cards to new players
cachedCardLibrary.cards.Add(new CardLibraryEntry
{
    id = cardName.ToLower().Replace(" ", "_"),
    name = cardName,
    starterCard = true,  // <-- New players get this
    // ...
});
```

### Change Colors
In **DeckSelectionUIPlayFab.cs**:

```csharp
public Color ownedCardColor = Color.white;      // Unlocked
public Color unownedCardColor = Color.gray;     // Locked
```

Or set in Inspector after creating the UI.

### Custom ID Format
If your card IDs don't match the format used here, update the matching logic in `DeckSelectionUIPlayFab.cs`:

```csharp
// OLD (uses cardName as ID)
var cardData = DeckRegistry.Instance.allCards.Find(c => 
    c.cardName.ToLower().Replace(" ", "_") == cardOwnership.cardId);

// NEW (if you have explicit cardId field)
var cardData = DeckRegistry.Instance.allCards.Find(c => 
    c.cardId == cardOwnership.cardId);
```

---

## Testing

### Test New Player Flow
1. Create new PlayFab account
2. Login
3. Check console: "Initialized new player with starter cards"
4. Go to deck selection
5. Should see starter cards as unlocked, others as locked

### Test Award Card
1. Logged-in player
2. Run: `PlayFabBackendManager.Instance.AwardCardToPlayer("card_id", "Card Name", 1)`
3. Check PlayFab Console > Data > player_cards JSON
4. Should show new card in `cards` array

### Test Persistence
1. Award card to player
2. Close game completely
3. Reopen and login
4. Card should still be there (show as unlocked)

---

## Production Checklist

- [ ] Set which cards are starter cards (`starterCard: true`)
- [ ] Create card reward system (what cards earned from what activities)
- [ ] Update MainMenuController to load cards after login
- [ ] Switch to DeckSelectionUIPlayFab in production build
- [ ] Add card acquisition notifications/UI
- [ ] Test with multiple player accounts
- [ ] Set up CloudScript for server-side card validation (Phase 3)
- [ ] Add card shop/marketplace (future)

---

## Files Modified/Created

✅ **CardOwnershipModels.cs** (NEW)
- PlayerCardOwnership
- PlayerCardCollection
- CardLibrary
- CardLibraryEntry

✅ **PlayFabBackendManager.cs** (EXTENDED)
- Added card ownership methods
- Added LoadPlayerCardCollection()
- Added AwardCardToPlayer()
- Added GetAllCardsWithOwnershipStatus()

✅ **DeckSelectionUIPlayFab.cs** (NEW)
- Card ownership-aware UI
- Greyed-out locked cards
- Interactable owned cards

📖 **PLAYFAB_CARD_OWNERSHIP_SETUP.md** (Documentation)

---

## Common Issues & Solutions

### "All cards show as locked"
→ Check DeckRegistry has your cards
→ Verify IDs match cardName (or update matching logic)

### "New player gets no starter cards"
→ Set `starterCard: true` for at least one card

### "Cards don't persist after logout"
→ Check PlayFab Data shows "player_cards" JSON
→ Verify SaveCardCollectionToPlayFab() called successfully

### "AwardCardToPlayer returns false"
→ Ensure player is logged in with valid EntityToken
→ Check PlayFab Player Data API enabled in title

---

## Next Steps

1. **Complete the Integration**
   - Update MainMenuController to load cards
   - Switch UI to DeckSelectionUIPlayFab
   - Test with real PlayFab account

2. **Add Card Rewards**
   - Design what cards players earn from matches
   - Implement AwardCardToPlayer() calls
   - Add UI notifications

3. **Server Validation (Phase 3)**
   - Create CloudScript to validate decks
   - Ensure players only use owned cards
   - Prevent client-side cheating

4. **Card Shop (Future)**
   - Store currency (gems/coins)
   - Let players buy cards
   - Track purchases in Player Data
