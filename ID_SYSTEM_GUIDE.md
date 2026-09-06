# ID System Guide - Loot Box Feature

## Overview
The entire loot box system (Unity frontend + PlayFab backend) now uses a unified ID-based identification system instead of names. This ensures consistent communication between client and server.

## ID Format Convention

### Card IDs (Abilities/Spells)
- **Format**: `c{number}` (e.g., `c1`, `c2`, `c3`)
- **Pattern**: Start from `c1` and increment
- **Example**: `c1` = ToxinBall, `c2` = BraveKnight, `c3` = Vanish

### Legend IDs (Guardians)
- **Format**: `l{number}` (e.g., `l1`, `l2`, `l3`)
- **Pattern**: Start from `l1` and increment
- **Example**: `l1` = Xod, `l2` = Boulder, `l3` = Pyro

### Stone IDs (Legend Stones/Passive Abilities)
- **Format**: `s{number}` (e.g., `s1`, `s2`)
- **Pattern**: Start from `s1` and increment
- **Example**: `s1` = ForestSpirit

## Implementation Details

### PlayFab Backend (GameFunctions.js)

1. **CARD_DB** - Master database with ID keys:
```javascript
var CARD_DB = {
    "c1": { name: "ToxinBall", rarity: "RARE" },
    "c2": { name: "BraveKnight", rarity: "COMMON" },
    "l1": { name: "Xod", rarity: "EPIC" },
    "s1": { name: "ForestSpirit", rarity: "COMMON" },
    // ... more items
};
```

2. **Loot Box Generation** - Uses IDs throughout:
   - Picks cards from `CARDS_BY_RARITY` (indexed by ID)
   - Returns `cardId` in response JSON
   - Stores in `player_cards` with ID as key

3. **Response Format**:
```json
{
    "success": true,
    "rewards": {
        "cards": [
            { "cardId": "c1", "cardName": "ToxinBall", "rarity": "RARE", "copies": 2 },
            { "cardId": "l1", "cardName": "Xod", "rarity": "EPIC", "copies": 1 },
            { "cardId": "s1", "cardName": "ForestSpirit", "rarity": "COMMON", "copies": 3 }
        ]
    }
}
```

### Unity Frontend

1. **Data Classes** - All have ID field:
   - `CardData.cs` - `public string id;` (e.g., "c1")
   - `CastingLegendData.cs` - `public string id;` (e.g., "l1")
   - `LegendStoneData.cs` - `public string id;` (e.g., "s1")

2. **DeckRegistry.cs** - ID-based lookup methods:
```csharp
public CardData GetCardById(string id)
public CastingLegendData GetCastingLegendById(string id)
public LegendStoneData GetLegendStoneById(string id)
```

3. **LootBoxInteraction.cs** - Retrieves items by ID:
```csharp
private object FindRewardItemById(string itemId)
{
    // Tries each lookup method in order
    var card = registry.GetCardById(itemId);
    if (card != null) return card;
    
    var legend = registry.GetCastingLegendById(itemId);
    if (legend != null) return legend;
    
    var stone = registry.GetLegendStoneById(itemId);
    if (stone != null) return stone;
    
    return null;
}
```

4. **CardSelectionPopulator.cs** - Uses ID-based lookup for player inventory:
```csharp
private void SetupPlayFabCard(PlayerCard playerCard)
{
    var cardData = GetCardDataById(playerCard.cardId);
    var legendData = GetGuardianById(playerCard.cardId);
    var stoneData = GetLegendStoneById(playerCard.cardId);
    // Display appropriate item
}
```

## Data Flow - Loot Box Opening

```
1. User opens loot box in game
   ↓
2. Unity calls PlayFab function: openLootBox(boxLevel)
   ↓
3. PlayFab generates rewards using IDs:
   - Picks from CARDS_BY_RARITY (all by ID)
   - Creates CardReward objects with cardId field
   ↓
4. PlayFab returns response with rewards array:
   { cards: [{ cardId: "c1", cardName: "ToxinBall", ... }] }
   ↓
5. LootBoxInteraction parses JSON and extracts cardIds
   ↓
6. For each cardId, calls FindRewardItemById(cardId)
   ↓
7. FindRewardItemById queries DeckRegistry:
   - GetCardById("c1") → returns CardData object
   ↓
8. LootBoxRewardUI displays item with animations
   ↓
9. Player receives cards in inventory (stored by ID)
```

## Adding New Items - Step by Step

### To add a new Card:

1. **Update CardData in Unity**:
   - Create new CardData prefab/asset
   - Set `id = "c{next_number}"` (e.g., "c10" if c1-c9 exist)
   - Set `cardName` for display
   - Add to DeckRegistry.allCards

2. **Update PlayFab CARD_DB**:
   ```javascript
   "c10": { name: "NewCard", rarity: "RARE" }
   ```

3. **Test**:
   - Open loot box
   - Should be able to receive "c10"
   - LootBoxInteraction finds it via GetCardById("c10")
   - Displays correctly in UI

### To add a new Legend:

1. **Update CastingLegendData in Unity**:
   - Set `id = "l{next_number}"` (e.g., "l4" if l1-l3 exist)
   - Add to DeckRegistry.allGuardians

2. **Update PlayFab CARD_DB**:
   ```javascript
   "l4": { name: "NewLegend", rarity: "EPIC" }
   ```

3. **Test**: Same as cards, but uses GetCastingLegendById("l4")

### To add a new Stone:

1. **Update LegendStoneData in Unity**:
   - Set `id = "s{next_number}"` (e.g., "s2")
   - Add to DeckRegistry.allLegendStones

2. **Update PlayFab CARD_DB**:
   ```javascript
   "s2": { name: "NewStone", rarity: "COMMON" }
   ```

3. **Test**: Uses GetLegendStoneById("s2")

## Verification Checklist

- [ ] All CardData objects have unique `id` field (c1, c2, c3, ...)
- [ ] All CastingLegendData objects have unique `id` field (l1, l2, l3, ...)
- [ ] All LegendStoneData objects have unique `id` field (s1, s2, ...)
- [ ] DeckRegistry.allCards contains all cards with IDs
- [ ] DeckRegistry.allGuardians contains all legends with IDs
- [ ] DeckRegistry.allLegendStones contains all stones with IDs
- [ ] GameFunctions.js CARD_DB has matching IDs for each item
- [ ] CARD_DB name field matches display name in Unity
- [ ] Loot box opens without errors
- [ ] Received items display correctly in LootBoxRewardUI
- [ ] Items added to player inventory with correct ID

## Troubleshooting

**Issue**: Item doesn't appear in loot box
- **Check**: ID mismatch between CARD_DB key and CardData.id
- **Fix**: Ensure they're identical (case-sensitive!)

**Issue**: FindRewardItemById returns null
- **Check**: DeckRegistry doesn't contain the ID
- **Fix**: Verify item is added to allCards/allGuardians/allLegendStones

**Issue**: Wrong item name displays
- **Check**: CARD_DB name field doesn't match CardData.cardName
- **Fix**: Update CARD_DB to use correct display name

**Issue**: PlayFab error about missing CARD_DB entry
- **Check**: Item ID in loot box but not in CARD_DB
- **Fix**: Add entry to CARD_DB with matching ID

## File References

- **PlayFab**: `PlayFabCloudFunctions/GameFunctions.js` (CARD_DB, openLootBox function)
- **Unity Data**: 
  - `Assets/.../CardData.cs` (id field)
  - `Assets/.../CastingLegendData.cs` (id field)
  - `Assets/.../LegendStoneData.cs` (id field)
- **Unity Logic**:
  - `Assets/.../DeckRegistry.cs` (ID lookup methods)
  - `Assets/.../LootBoxInteraction.cs` (FindRewardItemById)
  - `Assets/.../CardSelectionPopulator.cs` (ID-based inventory lookup)
  - `Assets/.../LootBoxRewardUI.cs` (Display logic)
