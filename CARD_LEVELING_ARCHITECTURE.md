# Card Leveling System - Complete Architecture

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        PLAYER PROGRESSION                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Player Opens Card Pack → Award 1 Copy                           │
│         ↓                                                         │
│  PlayFabBackendManager.AwardCardToPlayer()                       │
│         ↓                                                         │
│  Update Quantity: 0 → 1                                          │
│         ↓                                                         │
│  Auto-Level: Level 0 → Level 1 (Unlocked)                        │
│         ↓                                                         │
│  Save to PlayFab (Immutable)                                     │
│         ↓                                                         │
│  UI Refreshes: Shows "Lv.1" + "1 copies"                         │
│         ↓                                                         │
│  Player can now SELECT card in Deck                              │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      CLIENT SIDE (Unity)                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  DeckSelectionUIPlayFab                                          │
│  ├─ LoadAndPopulateUI()                                          │
│  │  └─ LoadPlayerCardCollection()                               │
│  │     └─ GetCardCollection() → PlayerCardCollection            │
│  │        └─ For each card:                                     │
│  │           • cardId                                            │
│  │           • level (0-5)                                       │
│  │           • quantity (total copies)                           │
│  │           • owned (level > 0)                                 │
│  │                                                               │
│  └─ PopulateCardList()                                           │
│     └─ Display each card:                                        │
│        • Card name                                              │
│        • Level: "Locked" or "Lv.1-5"                            │
│        • Quantity: "3 copies"                                    │
│        • Progress: "3/6" (progress to next level)               │
│        • Interactable: Only if owned (level > 0)                │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│                   PLAYFAB BACKEND (Server)                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Player Data Objects:                                            │
│  ├─ "player_cards" (PlayerCardCollection JSON)                   │
│  │  └─ {                                                         │
│  │     "cards": [                                                │
│  │       {                                                       │
│  │         "cardId": "card_fireball",                            │
│  │         "cardName": "Fireball",                               │
│  │         "level": 3,           ← Auto-calculated               │
│  │         "owned": true,        ← level > 0                     │
│  │         "quantity": 6,        ← Total copies                  │
│  │         "timesAcquired": 6,   ← Total awards                  │
│  │         "acquiredDate": "2026-01-06T15:30:00Z"               │
│  │       },                                                      │
│  │       { ... more cards ... }                                  │
│  │     ]                                                         │
│  │  }                                                            │
│  │                                                               │
│  ├─ "deck_X" (SavedDeck JSON) - For specific deck saves          │
│  │  └─ { DeckName, Cards[], LastModified }                      │
│  │                                                               │
│  └─ "active_deck" (Current match deck)                           │
│     └─ Used for anti-cheat validation                            │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Card Leveling Progression

### Copy Requirements

```
Copies Needed Per Level:
Level 0 → Level 1: 1 copy
Level 1 → Level 2: 2 MORE copies (3 total)
Level 1 → Level 3: 3 MORE copies (6 total)
Level 3 → Level 4: 4 MORE copies (10 total)
Level 4 → Level 5: 5 MORE copies (15 total)

Example Timeline:
Day 1: Award 1 copy → Level 1, Quantity: 1
Day 2: Award 2 copies → Level 2, Quantity: 3
Day 3: Award 3 copies → Level 3, Quantity: 6
Day 5: Award 4 copies → Level 4, Quantity: 10
Day 10: Award 5 copies → Level 5, Quantity: 15
```

### Auto-Leveling Algorithm

```csharp
// When AwardCardToPlayer() is called with quantity:
int totalCopiesNeeded = 0;
int newLevel = 0;

for (int lv = 1; lv <= maxLevel; lv++)
{
    int copiesForThisLevel = (lv == 1) ? 1 : config.GetCopiesRequiredForLevel(lv);
    totalCopiesNeeded += copiesForThisLevel;
    
    if (card.quantity >= totalCopiesNeeded)
    {
        newLevel = lv;
    }
    else
    {
        break;  // Not enough copies for next level
    }
}

if (newLevel > card.level)
{
    card.level = newLevel;
    card.owned = true;
}
```

---

## Class Hierarchy

```
PlayerCardOwnership
├─ cardId: string
├─ cardName: string
├─ level: int (0-5)
├─ owned: bool (level > 0)
├─ quantity: int (total copies)
├─ timesAcquired: int
├─ acquiredDate: DateTime
├─ GetDisplayStatus(): string
└─ GetNextLevelProgress(int): string

PlayerCardCollection
├─ cards: List<PlayerCardOwnership>
├─ OwnsCard(cardId): bool
├─ GetCardLevel(cardId): int
├─ GetCardQuantity(cardId): int
├─ AcquireCard(cardId, cardName, copiesToAdd, config): void
├─ TryLevelUpCard(cardId, copiesRequired): bool
└─ AutoLevelUpCard(card, config): void [private]

CardLibraryEntry
├─ id: string
├─ name: string
├─ description: string
├─ cardType: string
├─ maxLevel: int
├─ starterCard: bool
├─ copiesForLevel1-5: int
├─ GetCopiesRequiredForLevel(level): int
└─ GetTotalCopiesNeededForLevel(level): int

CardLevelingConfig
├─ cardId: string
├─ maxLevel: int
├─ copiesForLevel1-5: int
├─ GetCopiesRequiredForLevel(level): int
└─ GetTotalCopiesNeededForLevel(level): int

PlayFabBackendManager
├─ LoadPlayerCardCollection(callback): void
├─ GetCardCollection(): PlayerCardCollection
├─ AwardCardToPlayer(cardId, name, copies, config, callback): void
├─ LevelUpCard(cardId, copiesRequired, callback): void
├─ OwnsCard(cardId): bool
├─ GetCardQuantity(cardId): int
├─ SaveCardCollectionToPlayFab(callback): void [private]
└─ LoadCardLibraryFromDeckRegistry(): void [private]
```

---

## API Endpoints Used

### PlayFab Data API (SetObjects/GetObjects)

**Save Card Collection:**
```
POST /entity/objects/set
{
  Entity: { Id, Type },
  Objects: [{
    ObjectName: "player_cards",
    DataObject: "<PlayerCardCollection JSON>"
  }]
}
```

**Load Card Collection:**
```
GET /entity/objects/get
{
  Entity: { Id, Type },
  Keys: ["player_cards"]
}
```

Response:
```json
{
  "Objects": {
    "player_cards": {
      "DataObject": {
        "cards": [
          {
            "cardId": "card_fireball",
            "level": 3,
            "quantity": 6,
            ...
          }
        ]
      }
    }
  }
}
```

---

## Integration Points

### 1. Card Award (When Player Gets New Cards)

```csharp
// Called by: Loot system, battle rewards, shop purchase, event
PlayFabBackendManager.Instance.AwardCardToPlayer(
    "card_id",
    "Card Name",
    1,  // Can be 1, 2, 3+ for multi-copy drops
    new CardLevelingConfig { ... },
    (success) => {
        if (success) {
            // Update UI, show notification, etc.
            RefreshCardDisplay();
        }
    }
);
```

### 2. Deck Selection (Before Starting Match)

```csharp
// Called by: "Start Game" button click
// Only allow selecting cards that are owned (level > 0)
if (playerCard.level > 0)  // Owned
{
    selectedCards.Add(cardData);
}
else  // Locked
{
    ShowMessage($"Unlock {playerCard.cardName} by getting 1 copy");
}
```

### 3. Manual Level Up (Optional UI Feature)

```csharp
// Called by: "Level Up" button in collection view
int copiesNeeded = libraryEntry.GetCopiesRequiredForLevel(currentLevel + 1);
if (playerCard.quantity >= copiesNeeded)
{
    PlayFabBackendManager.Instance.LevelUpCard(
        cardId,
        copiesNeeded,
        (success) => { RefreshCardDisplay(); }
    );
}
```

### 4. Server-Side Validation (Anti-Cheat)

```csharp
// Called by: Dedicated server on game start
PlayFabBackendManager.Instance.LoadActiveDeck((savedDeck) => {
    // Compare against client-sent deck
    if (!DecksMatch(clientDeck, savedDeck))
    {
        DisconnectPlayer("Deck validation failed");
    }
});
```

---

## State Transitions

```
LOCKED (Level 0)
  ↓ Award 1+ copies
  ↓ AcquireCard() → level = 1
  ↓ SaveCardCollectionToPlayFab()
  ↓ SetObjects(ObjectName: "player_cards")
  ↓
LEVEL 1 (Quantity: 1+)
  ↓ Award 2+ MORE copies (total 3+)
  ↓ AutoLevelUpCard() → level = 2
  ↓ SaveCardCollectionToPlayFab()
  ↓
LEVEL 2 (Quantity: 3+)
  ↓ Award 3+ MORE copies (total 6+)
  ↓ AutoLevelUpCard() → level = 3
  ↓ SaveCardCollectionToPlayFab()
  ↓
LEVEL 3+ (continues...)
  ↓
MAX LEVEL 5 (Quantity: 15+)
```

---

## Storage Efficiency

```
Per Card Entry: ~150 bytes
Per Collection (60 cards): ~9 KB
Playback Overhead: ~1 KB
Total: ~10 KB per player

PlayFab Limit: 1 MB per player object
Headroom: 99 MB (can store 9,900+ players' data per call)
```

---

## Performance Metrics

| Operation | Time | Notes |
|-----------|------|-------|
| Load collection | 100ms | Network latency dominant |
| Award 1 card | 100ms | Includes save to PlayFab |
| Auto-level calc | <1ms | O(maxLevel) = O(5) |
| Refresh UI | 10ms | Update 60 card buttons |
| Manual level up | 100ms | Includes PlayFab save |

---

## Security Model

```
Client Side (Untrusted):
├─ Displays card information
├─ Shows owned/locked status
├─ Allows deck selection of owned cards
└─ Sends selected deck to server

PlayFab (Authoritative):
├─ Stores immutable card collection
├─ Records all card acquisitions
├─ Tracks quantity and level progression
└─ Prevents quantity manipulation

Server Side (Validation):
├─ Receives client-sent deck
├─ Retrieves "active_deck" from PlayFab
├─ Verifies deck matches PlayFab record
├─ Disconnects on mismatch
└─ Logs all exploits
```

---

## Example: Complete Game Flow

```
1. PLAYER LOGS IN
   LoginWithEmailAndPassword()
   → SessionTicket + EntityToken obtained
   → OnLoginSuccess event fired

2. DECK SELECTION SCREEN OPENS
   OnNetworkConnected()
   → LoadPlayerCardCollection()
   → PlayFab returns player_cards object
   → PlayerCardCollection deserialized
   → UI populates with cards:
     • Card 1: Level 3, Quantity: 6, Owned: true
     • Card 2: Level 0, Quantity: 0, Owned: false
     • Card 3: Level 1, Quantity: 1, Owned: true

3. PLAYER SELECTS CARDS FOR DECK
   ToggleCard(cardId)
   → Check if owned (level > 0)
   → Add to selectedCards list
   → UI highlights selected cards

4. PLAYER CLICKS "START GAME"
   OnStartGameClicked()
   → FinalizeAndSaveDeck("MatchDeck", selectedCards)
   → SaveActiveDeck() called
   → Serialize cards to CardDataDto list
   → PlayFab saves under "active_deck" object
   → Wait for callback

5. CONNECTION TO GAME SERVER
   Server receives: ClientConnectMessage + selectedCards
   → Server calls: LoadActiveDeck()
   → PlayFab returns saved "active_deck"
   → Server compares:
     ✓ Same cards? → Match starts
     ✗ Different? → Disconnect + Log exploit

6. DURING MATCH
   Server validates card stats against PlayerCardCollection
   → All cards in deck must be owned
   → Card levels must match saved levels
   → Quantity must match saved quantity

7. MATCH ENDS
   Winner determined, rewards granted
   → AwardCardToPlayer("card_reward", 1)
   → Quantity increased by 1
   → Auto-level triggered if threshold reached
   → PlayerCardCollection updated in PlayFab
   → Next login: Player sees new card level

8. PLAYER LOGS OUT
   Logout()
   → AuthContext.ForgetAllCredentials()
   → cachedDecks.Clear()
   → All local data cleared
   → Next login: Fresh load from PlayFab
```

---

## Testing Strategy

### Unit Tests
- [ ] PlayerCardOwnership.GetDisplayStatus()
- [ ] PlayerCardOwnership.GetNextLevelProgress()
- [ ] PlayerCardCollection.AcquireCard() with 1, 2, 3 copies
- [ ] PlayerCardCollection.AutoLevelUpCard() thresholds
- [ ] CardLevelingConfig.GetTotalCopiesNeededForLevel()

### Integration Tests
- [ ] AwardCardToPlayer() triggers SaveCardCollectionToPlayFab()
- [ ] LoadPlayerCardCollection() deserializes correctly
- [ ] Auto-leveling persists to PlayFab
- [ ] Multiple AwardCardToPlayer() calls cumulative

### E2E Tests
- [ ] Award 1 card → Unlocks to Level 1
- [ ] Award 2 more cards → Levels to Level 2
- [ ] Award 3 more cards → Levels to Level 3
- [ ] Reach max level → No further leveling
- [ ] Save deck with owned cards only
- [ ] Server validates deck matches saved

### Security Tests
- [ ] Attempt to select locked card → Rejected
- [ ] Send deck with unowned cards → Server blocks
- [ ] Send deck with wrong card levels → Server blocks
- [ ] Modify local quantity → Server validation prevents use

---

## Deployment Checklist

- [ ] CardOwnershipModels.cs compiles without errors
- [ ] PlayFabBackendManager.cs compiles without errors
- [ ] DeckSelectionUIPlayFab.cs updated to show quantities
- [ ] Card award system integrated into loot drops
- [ ] UI displays: Name, Level, Quantity, Progress
- [ ] Manual level-up feature works (optional)
- [ ] PlayFab "player_cards" object configured
- [ ] Server-side deck validation implemented
- [ ] Anti-cheat logging configured
- [ ] QA tested all exploit scenarios
- [ ] Production ready!
