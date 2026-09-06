# PlayFab Card Ownership System - Setup Guide

## Overview

The card ownership system manages which cards each player owns. It works like this:

1. **Master Card Library** - All possible cards in the game (loaded from DeckRegistry)
2. **Player Card Collection** - Which cards the specific player owns (stored in PlayFab Player Data)
3. **Card Selection UI** - Shows all cards, greyed out if player doesn't own them

---

## Architecture

### Data Structure

**PlayerCardOwnership** (individual card)
```csharp
{
    cardId: "brave_knight",
    cardName: "Brave Knight",
    level: 3,
    owned: true,
    timesAcquired: 2,
    acquiredDate: "2026-01-06"
}
```

**PlayerCardCollection** (all owned cards)
```csharp
{
    cards: [
        { cardId: "brave_knight", owned: true, level: 3 },
        { cardId: "tree", owned: true, level: 1 },
        { cardId: "stone_sentinel", owned: false, level: 0 }
    ]
}
```

Stored in PlayFab under: **Player Data > "player_cards"**

### Master Card Library

```csharp
{
    cards: [
        {
            id: "brave_knight",
            name: "Brave Knight",
            cardType: "unit",
            maxLevel: 5,
            starterCard: true
        },
        {
            id: "stone_sentinel",
            name: "Stone Sentinel",
            cardType: "unit",
            maxLevel: 5,
            starterCard: false
        }
    ]
}
```

Loaded from: **DeckRegistry** (Resources/DeckRegistry)

---

## Integration Steps

### Step 1: Update MainMenuController

After player logs in with PlayFab, load their card collection:

```csharp
public void OnLoginSuccess()
{
    // Login happened in PlayFabBackendManager
    
    // Load player's card collection
    PlayFabBackendManager.Instance.LoadPlayerCardCollection((collection) =>
    {
        Debug.Log($"Player owns {collection.cards.Count} cards");
        // Proceed to deck selection
        StartCoroutine(LoadGameScene());
    });
}
```

### Step 2: Use DeckSelectionUIPlayFab

Replace your current DeckSelectionUI with DeckSelectionUIPlayFab:

1. Create a new Canvas/Panel for deck selection
2. Attach `DeckSelectionUIPlayFab` script
3. Assign references:
   - cardListContainer (Transform)
   - cardButtonPrefab (GameObject)
   - legendListContainer, stoneListContainer (similar)
   - startMatchButton, connectionUI, deckSelectionUI (as before)

```csharp
// In your UI setup
deckSelectionUI.GetComponent<DeckSelectionUIPlayFab>().PopulateCards();
```

### Step 3: Start Game with PlayFab

When player clicks "Start Game", use the selected cards:

```csharp
public void OnStartGameClicked()
{
    var ui = GetComponent<DeckSelectionUIPlayFab>();
    var selectedCards = ui.GetSelectedCards();
    var selectedLegend = ui.GetSelectedLegend();
    var selectedStone = ui.GetSelectedStone();
    
    // Send to dedicated server
    NetworkUI.InitiateMatchmakingAndLoadScene(selectedCards, selectedLegend, selectedStone);
}
```

---

## Card Ownership Operations

### Award a Card to Player

```csharp
PlayFabBackendManager.Instance.AwardCardToPlayer(
    cardId: "new_card_id",
    cardName: "New Card Name",
    level: 1,
    callback: (success) =>
    {
        if (success)
            Debug.Log("Card awarded!");
    }
);
```

### Check if Player Owns Card

```csharp
bool owns = PlayFabBackendManager.Instance.OwnsCard("brave_knight");
```

### Get Card Level

```csharp
int level = PlayFabBackendManager.Instance.GetCardLevel("brave_knight");
```

### Get All Cards with Ownership Status

```csharp
List<PlayerCardOwnership> allCards = 
    PlayFabBackendManager.Instance.GetAllCardsWithOwnershipStatus();

foreach (var cardOwnership in allCards)
{
    Debug.Log($"{cardOwnership.cardName}: {(cardOwnership.owned ? "Owned" : "Locked")}");
}
```

---

## New Player Initialization

When a player logs in for the first time:

1. `LoadPlayerCardCollection()` returns empty collection
2. `InitializeNewPlayerCards()` is called automatically
3. All cards marked as `starterCard: true` are awarded
4. Collection is saved to PlayFab

Currently, no cards are marked as starter cards. To change this:

```csharp
// In LoadCardLibraryFromDeckRegistry()
cachedCardLibrary.cards.Add(new CardLibraryEntry
{
    id = cardData.cardName.ToLower().Replace(" ", "_"),
    name = cardData.cardName,
    starterCard = true,  // <-- New players get this
    // ... other fields
});
```

---

## UI Display

### Color Scheme

- **Owned Cards** (White) - Interactable, clickable
- **Locked Cards** (Greyed Out) - Non-interactable, shows "(Locked)"

### Customization

```csharp
public Color ownedCardColor = Color.white;
public Color unownedCardColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
```

Change these in Inspector or code:

```csharp
var ui = GetComponent<DeckSelectionUIPlayFab>();
ui.ownedCardColor = Color.green;
ui.unownedCardColor = Color.red;
```

---

## Card Acquisition Flow

### In Your Game Logic

When player wins a match or completes a task:

```csharp
// Award random card
string[] cardIds = { "brave_knight", "tree", "stone_sentinel" };
string randomCard = cardIds[Random.Range(0, cardIds.Length)];

PlayFabBackendManager.Instance.AwardCardToPlayer(
    randomCard, 
    "Random Card", 
    level: 1,
    callback: (success) =>
    {
        if (success)
            Debug.Log("Congratulations! You earned a new card!");
    }
);
```

### Or Via CloudScript

Server-side award cards for match wins:

```javascript
// CloudScript
handlers.awardCardForVictory = function(args, context)
{
    var randomCard = cards[Math.floor(Math.random() * cards.length)];
    
    // Award via PlayFab API
    return {
        success: true,
        cardAwarded: randomCard
    };
};
```

---

## Testing Checklist

- [ ] **Login & Load Cards**
  - Player logs in
  - `LoadPlayerCardCollection()` called
  - Cards loaded from PlayFab

- [ ] **First-Time Player**
  - New player logs in
  - Automatically gets starter cards
  - Collection saved to PlayFab

- [ ] **Card Display**
  - All cards shown in UI
  - Owned cards are white and interactable
  - Locked cards are greyed and non-interactable

- [ ] **Award Card**
  - Award new card via `AwardCardToPlayer()`
  - Card appears in next session
  - Level is correct

- [ ] **Deck Selection**
  - Can only select owned cards
  - Attempting to select locked card fails gracefully

- [ ] **Persistence**
  - Close game and reopen
  - Card collection still there
  - Same owned/locked status

---

## Common Issues

### Issue: All cards show as locked
**Solution:** Check that your cards exist in DeckRegistry and have proper `cardId` format

### Issue: New player gets no starter cards
**Solution:** Set `starterCard = true` in LoadCardLibraryFromDeckRegistry for at least one card

### Issue: Card level always 0
**Solution:** Ensure card is awarded with `level > 0`, or set properly in PlayFab

### Issue: "PlayerCardCollection not serializable"
**Solution:** Ensure it has `[System.Serializable]` attribute and JsonUtility can parse it

---

## Next: Server-Side Validation

For security, validate card ownership on the server:

```csharp
// CloudScript - verify player owns card before using it
handlers.validateDeck = function(args, context)
{
    var playerCards = context.playerData;
    var selectedCards = args.cards;
    
    // Check each selected card exists in player collection
    // Return error if any card is not owned
    
    return { valid: true };
};
```

---

## Data Flow Diagram

```
Player Login
    ↓
PlayFabBackendManager.LoginWithEmailAndPassword()
    ↓
LoadPlayerCardCollection() ← Fetch from PlayFab Player Data
    ↓
Is Empty? → InitializeNewPlayerCards() → Award starter cards
    ↓
DeckSelectionUIPlayFab.LoadAndPopulateUI()
    ↓
LoadCardLibrary() ← Load master list from DeckRegistry
    ↓
PopulateUI() → Show owned cards (white) + locked cards (grey)
    ↓
Player clicks card → ToggleCard() → Check OwnsCard()
    ↓
Player clicks Start → Send selected cards to server
    ↓
Server validates via CloudScript
    ↓
Game starts!
```

---

## Files Created/Modified

- **CardOwnershipModels.cs** - Data classes (PlayerCardOwnership, PlayerCardCollection, CardLibrary)
- **PlayFabBackendManager.cs** - Extended with card ownership methods
- **DeckSelectionUIPlayFab.cs** - New UI that respects card ownership
- **MainMenuController.cs** - (Update to load cards after login)

---

## Future Enhancements

1. **Card Shop** - Buy cards with in-game currency
2. **Card Crafting** - Combine cards to get rarer ones
3. **Seasonal Rewards** - Limited-time cards
4. **Battle Pass** - Unlock cards through progression
5. **Marketplace** - Trade cards with other players (via CloudScript)
