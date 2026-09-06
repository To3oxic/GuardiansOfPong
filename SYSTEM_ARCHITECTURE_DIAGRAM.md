# Visual System Architecture - ID-Based Loot Box

## Complete Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           PLAYER OPENS LOOT BOX                     │
│                                                                     │
│  Game UI → LootBoxInteraction.OnLootBoxClickedAsync()              │
│                   │                                                │
│                   ├─ Disables cube rotation                       │
│                   ├─ Moves camera                                 │
│                   ├─ Calls PlayFab: openLootBox(boxLevel)        │
│                   └─ Waits for response                           │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                        PLAYFAB CLOUD SCRIPT                         │
│                     GameFunctions.openLootBox()                     │
│                                                                     │
│  1. Get boxLevel (1-10)                                            │
│  2. Load player inventory & cards from InternalData                │
│  3. Get loot box config (LOOTBOX_CONFIG[level])                    │
│  4. Roll rewards:                                                  │
│     - Build budget from quantityBudget                             │
│     - Roll rarity from rarityChances                               │
│     - Pick card ID from CARDS_BY_RARITY                            │
│     - Award 1+ copies based on budget                              │
│                                                                     │
│  CARD_DB Structure:                                                │
│  ┌──────────────────────────────────────────────────────────┐      │
│  │ "c1": { name: "ToxinBall", rarity: "RARE" }             │      │
│  │ "c2": { name: "BraveKnight", rarity: "COMMON" }         │      │
│  │ "c3": { name: "Vanish", rarity: "COMMON" }              │      │
│  │ "c4": { name: "LightningBall", rarity: "EPIC" }         │      │
│  │ ...                                                      │      │
│  │ "l1": { name: "Xod", rarity: "EPIC" }                   │      │
│  │ "l2": { name: "Boulder", rarity: "COMMON" }             │      │
│  │ "l3": { name: "Pyro", rarity: "RARE" }                  │      │
│  │ ...                                                      │      │
│  │ "s1": { name: "ForestSpirit", rarity: "COMMON" }        │      │
│  └──────────────────────────────────────────────────────────┘      │
│                                                                     │
│  5. Build grantedCardsDetailed array                               │
│  6. Save to player_cards InternalData                              │
│  7. Return response with cardIds                                   │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    PLAYFAB RESPONSE (JSON)                          │
│                                                                     │
│  {                                                                 │
│    "success": true,                                                │
│    "rewards": {                                                    │
│      "cards": [                                                    │
│        { "cardId": "c1", "cardName": "ToxinBall", ... },          │
│        { "cardId": "l1", "cardName": "Xod", ... },                │
│        { "cardId": "s1", "cardName": "ForestSpirit", ... }        │
│      ]                                                             │
│    }                                                               │
│  }                                                                 │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                      UNITY FRONTEND PARSING                         │
│                  LootBoxInteraction.OnLootBoxOpenSuccess()          │
│                                                                     │
│  1. Parse JSON with JsonUtility.FromJson<LootBoxResponse>         │
│  2. For each reward in response.rewards.cards:                    │
│     └─ Call FindRewardItemById(reward.cardId)                     │
│                                                                     │
│     Example: FindRewardItemById("c1")                             │
│        ├─ registry.GetCardById("c1")                              │
│        │  └─ Search allCards[] for card.id == "c1"                │
│        │     └─ Found: ToxinBall CardData object                  │
│        ├─ If null, try registry.GetCastingLegendById("c1")       │
│        └─ If null, try registry.GetLegendStoneById("c1")         │
│                                                                     │
│     Return: Found item object (CardData, CastingLegendData, or    │
│              LegendStoneData)                                      │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                 LOOT BOX REWARD UI DISPLAY                          │
│                      LootBoxRewardUI.DisplayReward()               │
│                                                                     │
│  1. Determine item type:                                           │
│     ├─ if CardData → instantiate LootBoxCardHolder prefab         │
│     ├─ if CastingLegendData → instantiate LootBoxLegendHolder     │
│     └─ if LegendStoneData → instantiate LootBoxLegendStoneHolder  │
│                                                                     │
│  2. Setup holder with item data                                    │
│  3. Play fade-in animation                                         │
│  4. Wait for animation complete                                    │
│  5. Show next item (or close if last)                             │
│                                                                     │
│  Each Holder displays:                                             │
│  ├─ Image (from cardData.cardImage or guardianData.image)         │
│  ├─ Name (from cardData.cardName or guardianData.name)            │
│  └─ Quantity/Rarity badges                                         │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                    INVENTORY UPDATE (Backend)                       │
│                   Player receives items via PlayFab                │
│                                                                     │
│  player_cards InternalData:                                        │
│  {                                                                 │
│    "c1": {                                                         │
│      "cardId": "c1",              ← ID field                       │
│      "cardName": "ToxinBall",                                      │
│      "rarity": "RARE",                                             │
│      "quantity": 2,              ← How many copies                 │
│      "owned": true,                                                │
│      "level": 1,                                                   │
│      "timesAcquired": 5          ← Total acquired                  │
│    },                                                              │
│    "l1": {                                                         │
│      "cardId": "l1",                                               │
│      "cardName": "Xod",                                            │
│      ...                                                           │
│    }                                                               │
│  }                                                                 │
└─────────────────────────────────────────────────────────────────────┘
```

## ID Assignment Breakdown

### Card IDs (Format: c{number})
```
User opens Loot Box
  ↓
PlayFab rolls COMMON card
  ↓
CARDS_BY_RARITY["COMMON"] = ["c2", "c3", "c7", "c9"]
  ↓
Randomly pick "c2"
  ↓
Return cardId: "c2"
  ↓
Unity looks up: GetCardById("c2")
  ↓
Find in DeckRegistry.allCards where id == "c2"
  ↓
Display: BraveKnight card
```

### Legend IDs (Format: l{number})
```
User opens Loot Box
  ↓
PlayFab rolls EPIC legend
  ↓
CARDS_BY_RARITY["EPIC"] = ["l1", "c4", "c5"]
  ↓
Randomly pick "l1"
  ↓
Return cardId: "l1"
  ↓
Unity looks up: GetCastingLegendById("l1")
  ↓
Find in DeckRegistry.allGuardians where id == "l1"
  ↓
Display: Xod legend
```

### Stone IDs (Format: s{number})
```
User opens Loot Box
  ↓
PlayFab rolls COMMON stone
  ↓
CARDS_BY_RARITY["COMMON"] = ["c2", "c3", "s1", "c7"]
  ↓
Randomly pick "s1"
  ↓
Return cardId: "s1"
  ↓
Unity looks up: GetLegendStoneById("s1")
  ↓
Find in DeckRegistry.allLegendStones where id == "s1"
  ↓
Display: ForestSpirit stone
```

## Key Data Structures

### PlayFab CARD_DB
```javascript
var CARD_DB = {
    "c1": { name: "ToxinBall", rarity: "RARE" },        // Card
    "c2": { name: "BraveKnight", rarity: "COMMON" },    // Card
    "l1": { name: "Xod", rarity: "EPIC" },              // Legend
    "l2": { name: "Boulder", rarity: "COMMON" },        // Legend
    "s1": { name: "ForestSpirit", rarity: "COMMON" }    // Stone
};

var CARDS_BY_RARITY = {
    "COMMON": ["c2", "c3", "c7", "c9", "l2", "s1"],
    "RARE": ["c1", "c6", "c8", "l3"],
    "EPIC": ["c4", "c5", "l1"]
};
```

### Unity DeckRegistry
```csharp
public CardData[] allCards = [
    ToxinBall (id="c1"),
    BraveKnight (id="c2"),
    ... (9 total)
];

public CastingLegendData[] allGuardians = [
    Xod (id="l1"),
    Boulder (id="l2"),
    Pyro (id="l3")
];

public LegendStoneData[] allLegendStones = [
    ForestSpirit (id="s1")
];
```

## Complete System Properties

| Property | Value | Notes |
|----------|-------|-------|
| **ID Format** | Short codes (c1, l2, s1) | Easy to use, collision-proof |
| **Lookup Method** | Linear search by id field | O(n) but fast for small arrays |
| **Backend Storage** | PlayFab InternalData | Persistent, secure |
| **Frontend Lookup** | DeckRegistry arrays | In-memory, instant |
| **Network Protocol** | PlayFab JSON response | Standardized format |
| **Type Safety** | Three separate ID spaces | No mixing (c1≠l1≠s1) |
| **Scalability** | Add more IDs (c10, l4, s2) | Simple increment pattern |

## ID Lookup Flow (Technical)

```csharp
// Input: cardId from PlayFab (e.g., "c1")
string itemId = "c1";

// LootBoxInteraction calls:
object item = FindRewardItemById(itemId);

// FindRewardItemById implementation:
public object FindRewardItemById(string itemId)
{
    // Try card lookup
    var card = registry.GetCardById(itemId);
    if (card != null) return card;
    
    // Try legend lookup
    var legend = registry.GetCastingLegendById(itemId);
    if (legend != null) return legend;
    
    // Try stone lookup
    var stone = registry.GetLegendStoneById(itemId);
    if (stone != null) return stone;
    
    return null;
}

// GetCardById implementation (in DeckRegistry):
public CardData GetCardById(string id)
{
    return allCards.FirstOrDefault(c => c.id == id);
    // Search through allCards array
    // Return card where id field matches
}
```

## Success Indicators

✅ **System is working when:**
1. Loot box opens without errors
2. PlayFab returns JSON with cardIds (c1, l2, s1)
3. FindRewardItemById returns correct items
4. UI displays correct names/images
5. Items stored in inventory with IDs
6. Second loot box works correctly
7. All three types can appear

---

**This diagram shows the complete end-to-end flow of how IDs move through the system!**
