# Card Leveling System - Implementation Summary

## What Was Implemented

A complete card leveling system where players:
- Start with cards **locked** (Level 0, Quantity 0)
- Unlock cards by getting **1 copy** (Level 1, Quantity 1)
- Level up cards by accumulating more copies:
  - **1 copy** → Level 1 (unlocked)
  - **3 total copies** → Level 2 (need 2 more)
  - **6 total copies** → Level 3 (need 3 more)
  - **10 total copies** → Level 4 (need 4 more)
  - **15 total copies** → Level 5 (max, need 5 more)

---

## Files Updated

### 1. CardOwnershipModels.cs ✅
**New Features:**
- `PlayerCardOwnership.quantity` - Total copies owned
- `PlayerCardOwnership.level` - Now 0-5 (0=locked)
- `PlayerCardOwnership.GetDisplayStatus()` - Returns "Locked" or "Lv.1"
- `PlayerCardOwnership.GetNextLevelProgress()` - Shows "2/3" progress
- `PlayerCardCollection.GetCardQuantity()` - Get total copies
- `PlayerCardCollection.AcquireCard()` - Now handles quantities + auto-leveling
- `PlayerCardCollection.TryLevelUpCard()` - Manual level up
- `CardLibraryEntry` - Added copy requirements for each level
- `CardLevelingConfig` - New class to define leveling costs

### 2. PlayFabBackendManager.cs ✅
**New Methods:**
- `AwardCardToPlayer(cardId, name, copies, config, callback)` - Award copies with auto-leveling
- `LevelUpCard(cardId, copiesRequired, callback)` - Manual level up
- Updated `LoadCardLibraryFromDeckRegistry()` - Initializes leveling configs

**Updated Methods:**
- `AcquireCard()` → `AwardCardToPlayer()` (better API)

### 3. Documentation Created ✅
- `CARD_LEVELING_SYSTEM.md` - Complete API reference
- `CARD_LEVELING_UI_QUICK_REF.md` - UI implementation guide
- `CARD_LEVELING_ARCHITECTURE.md` - System architecture

---

## How to Use

### Award Cards to Player

```csharp
// Award 1 copy (auto-levels if threshold reached)
PlayFabBackendManager.Instance.AwardCardToPlayer(
    "card_fireball",
    "Fireball",
    1,          // 1 copy
    null,       // Use default leveling config
    (success) => {
        if (success) {
            Debug.Log("Card awarded!");
            RefreshCardDisplay();
        }
    }
);

// Award 3 copies of a rare card with custom leveling
var rareConfig = new CardLevelingConfig {
    cardId = "card_legendary",
    maxLevel = 3,
    copiesForLevel1 = 1,
    copiesForLevel2 = 5,  // More expensive
    copiesForLevel3 = 10
};

PlayFabBackendManager.Instance.AwardCardToPlayer(
    "card_legendary",
    "Legendary Dragon",
    3,
    rareConfig,
    callback
);
```

### Display in UI

```csharp
var card = playerCards.cards.Find(c => c.cardId == cardId);

// What to show:
Text levelText = "Locked";  // or "Lv.1"
if (card.level > 0) {
    levelText = $"Lv.{card.level}";
}

Text quantityText = $"{card.quantity}";

// Can select?
button.interactable = (card.level > 0);  // Only if unlocked

// Progress to next level
if (card.level == 0) {
    progressText.text = "0/1";  // Need 1 to unlock
} else {
    int nextRequired = libraryEntry.GetCopiesRequiredForLevel(card.level + 1);
    progressText.text = $"{card.quantity}/{nextRequired}";
}
```

### Manual Level Up (Optional)

```csharp
void OnLevelUpClicked(string cardId)
{
    var card = playerCards.cards.Find(c => c.cardId == cardId);
    var library = PlayFabBackendManager.Instance.GetCardLibrary();
    var entry = library.GetCard(cardId);
    
    int copiesNeeded = entry.GetCopiesRequiredForLevel(card.level + 1);
    
    if (card.quantity < copiesNeeded)
    {
        ShowError($"Need {copiesNeeded}, have {card.quantity}");
        return;
    }
    
    PlayFabBackendManager.Instance.LevelUpCard(
        cardId,
        copiesNeeded,
        (success) => {
            if (success) {
                ShowMessage($"Leveled up to Lv.{card.level + 1}!");
                RefreshCardDisplay();
            }
        }
    );
}
```

---

## Key Concepts

### Locked vs Owned
```
Locked: level == 0, quantity == 0 (can't use in deck)
Owned:  level >= 1, quantity >= 1 (can use in deck)
```

### Auto-Leveling
When you call `AwardCardToPlayer()`:
1. Quantity increases by X
2. System checks: "Does quantity reach next level threshold?"
3. If yes: Level increases automatically
4. Data saved to PlayFab immediately

### Manual Leveling
Optionally, let players spend copies to level up:
1. Player has 6 copies of a Level 2 card
2. Player clicks "Level Up"
3. `LevelUpCard()` consumes 3 copies → quantity becomes 3
4. Card level becomes 3
5. Both saved to PlayFab

---

## Data Model (PlayFab)

Each player has "player_cards" object:
```json
{
  "cards": [
    {
      "cardId": "card_fireball",
      "cardName": "Fireball",
      "level": 3,
      "owned": true,
      "quantity": 6,
      "timesAcquired": 6,
      "acquiredDate": "2026-01-06T10:30:00Z"
    },
    {
      "cardId": "card_freeze",
      "cardName": "Freeze",
      "level": 0,
      "owned": false,
      "quantity": 0,
      "timesAcquired": 0,
      "acquiredDate": "1970-01-01T00:00:00Z"
    }
  ]
}
```

---

## Integration Points

### Loot System
```csharp
// When player opens card pack or wins match:
List<string> lootCardIds = GenerateLoot();  // e.g., ["card_1", "card_2", "card_3"]

foreach (var cardId in lootCardIds)
{
    var library = PlayFabBackendManager.Instance.GetCardLibrary();
    var entry = library.GetCard(cardId);
    
    PlayFabBackendManager.Instance.AwardCardToPlayer(
        cardId,
        entry.name,
        1,  // 1 copy per drop
        null,  // Default config
        null
    );
}

// Refresh UI once
Invoke(nameof(RefreshCardDisplay), 0.5f);
```

### Deck Selection
```csharp
// When building a deck:
void ToggleCard(CardData cardData)
{
    var ownership = playerCards.cards.Find(c => c.cardId == cardData.cardName);
    
    if (ownership.level == 0)  // Locked
    {
        ShowMessage($"Unlock {cardData.cardName} first!");
        return;
    }
    
    // Allowed - add to deck
    selectedCards.Add(cardData);
}
```

### Deck Validation
```csharp
// Server-side (prevent cheating):
void ValidatePlayerDeck(string playerId, DeckPayload clientDeck)
{
    PlayFabBackendManager.Instance.LoadPlayerCardCollection((collection) => {
        foreach (var card in clientDeck.Cards)
        {
            if (!collection.OwnsCard(card.id))  // level > 0
            {
                DisconnectPlayer(playerId, "Unowned card in deck");
                return;
            }
        }
        
        // All cards owned - start game
        StartGame(playerId);
    });
}
```

---

## Compilation Status ✅

Both files compile without errors:
- `CardOwnershipModels.cs` ✅
- `PlayFabBackendManager.cs` ✅

---

## Next Steps

1. **Update DeckSelectionUIPlayFab.cs**
   - Display card quantities (e.g., "3 copies")
   - Show level (e.g., "Lv.2")
   - Show progress to next level (e.g., "3/6")
   - See `CARD_LEVELING_UI_QUICK_REF.md` for implementation

2. **Integrate with Loot System**
   - When players get cards, call `AwardCardToPlayer()`
   - Cards automatically level up based on quantity
   - See examples above

3. **Add Optional UI Features**
   - Manual level-up buttons (let player spend copies)
   - Collection browser showing all cards + progress bars
   - Level-up animations/notifications

4. **Server-Side Validation**
   - Verify deck contains only owned cards
   - See `PLAYFAB_DECK_SECURITY_ANTICHEAT.md`

5. **Testing**
   - Award cards, verify quantity increases
   - Check UI updates correctly
   - Validate server rejects unlocked cards
   - Test max level behavior

---

## Configuration

### Default Leveling (Used if CardLevelingConfig is null)
```
Level 1: 1 copy
Level 2: 2 MORE copies (3 total)
Level 3: 3 MORE copies (6 total)
Level 4: 4 MORE copies (10 total)
Level 5: 5 MORE copies (15 total)
```

### Custom Leveling Example (Rare Cards)
```csharp
var rareConfig = new CardLevelingConfig
{
    cardId = "card_legendary",
    maxLevel = 3,
    copiesForLevel1 = 1,    // 1 to unlock
    copiesForLevel2 = 5,    // 5 MORE for level 2 (6 total)
    copiesForLevel3 = 10    // 10 MORE for level 3 (16 total)
};
```

---

## Files to Update (UI Implementation)

Update these methods in `DeckSelectionUIPlayFab.cs`:

1. `PopulateCardList()` - Add quantity display
2. `PopulateLegendList()` - Add quantity display
3. `PopulateStoneList()` - Add quantity display
4. Card button prefab - Add fields for quantity/progress display

See `CARD_LEVELING_UI_QUICK_REF.md` for exact code.

---

## Documentation Files

- **CARD_LEVELING_SYSTEM.md** - Complete API reference with examples
- **CARD_LEVELING_UI_QUICK_REF.md** - UI implementation guide
- **CARD_LEVELING_ARCHITECTURE.md** - System architecture and design
- **PLAYFAB_DECK_SECURITY_INTEGRATION.md** - Server-side validation
- **PLAYFAB_DECK_SECURITY_ANTICHEAT.md** - Anti-cheat mechanisms

---

## Testing Checklist

- [ ] Award 1 card → Level 1, Quantity 1
- [ ] Award 2 more cards → Auto-level to 2, Quantity 3
- [ ] Award 3 more cards → Auto-level to 3, Quantity 6
- [ ] Award 4 more cards → Auto-level to 4, Quantity 10
- [ ] Award 5 more cards → Auto-level to 5, Quantity 15
- [ ] UI shows correct level ("Locked" or "Lv.X")
- [ ] UI shows correct quantity ("3 copies")
- [ ] UI shows correct progress ("3/6")
- [ ] Greyed out cards can't be selected
- [ ] Owned cards are selectable
- [ ] Deck saves with owned cards only
- [ ] Server validates deck correctly
- [ ] Manual level-up works (if implemented)

---

## Support

For questions about:
- **Card mechanics**: See CARD_LEVELING_SYSTEM.md
- **UI implementation**: See CARD_LEVELING_UI_QUICK_REF.md
- **System architecture**: See CARD_LEVELING_ARCHITECTURE.md
- **Server validation**: See PLAYFAB_DECK_SECURITY_INTEGRATION.md

All documentation is in the project root folder.
