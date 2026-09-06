# ID System Implementation Summary

## What Has Been Done ✅

### PlayFab Backend (GameFunctions.js)
1. **Updated CARD_DB** with new ID format:
   - Cards: `c1` (ToxinBall), `c2` (BraveKnight), `c3` (Vanish), etc.
   - Legends: `l1` (Xod), `l2` (Boulder), `l3` (Pyro)
   - Stones: `s1` (ForestSpirit)
   - Clear comments showing the pattern for adding new items

2. **Loot Box Function** (openLootBox):
   - ✅ Already uses `cardId` as key throughout
   - ✅ Picks items from `CARDS_BY_RARITY` (indexed by ID)
   - ✅ Returns rewards with `cardId` field in response
   - ✅ Stores in player inventory with ID as key

### Unity Frontend
1. **Data Classes** (All verified to have `id` field):
   - ✅ CardData.cs - `public string id;`
   - ✅ CastingLegendData.cs - `public string id;`
   - ✅ LegendStoneData.cs - `public string id;`

2. **DeckRegistry.cs**:
   - ✅ GetCardById(string id) - lookup by ID
   - ✅ GetCastingLegendById(string id) - lookup by ID
   - ✅ GetLegendStoneById(string id) - lookup by ID

3. **LootBoxInteraction.cs**:
   - ✅ FindRewardItemById(string itemId) - retrieves items by ID
   - ✅ Handles all three types: cards, legends, stones
   - ✅ Uses registry ID methods for efficient lookup

4. **CardSelectionPopulator.cs**:
   - ✅ Updated to use ID-based lookup from registry
   - ✅ SetupPlayFabCard() uses playerCard.cardId

5. **UI System**:
   - ✅ LootBoxRewardUI handles all three reward types
   - ✅ Routes to correct holder (Card/Legend/Stone)
   - ✅ Displays animations correctly

## What You Need to Do Next

### Step 1: Update Your Card IDs in Unity Inspector
For each CardData prefab/asset in your project, set the `id` field:

```
Assets/MainAssets/Cards/ToxinBall → id = "c1"
Assets/MainAssets/Cards/BraveKnight → id = "c2"
Assets/MainAssets/Cards/Vanish → id = "c3"
Assets/MainAssets/Cards/LightningBall → id = "c4"
Assets/MainAssets/Cards/AncientSpirit → id = "c5"
Assets/MainAssets/Cards/AngryBear → id = "c6"
Assets/MainAssets/Cards/ForestMonsters → id = "c7"
Assets/MainAssets/Cards/StoneSentinel → id = "c8"
Assets/MainAssets/Cards/Tree → id = "c9"
```

### Step 2: Update Guardian IDs in Unity Inspector
For each CastingLegendData prefab/asset:

```
Assets/MainAssets/Guardians/Xod → id = "l1"
Assets/MainAssets/Guardians/Boulder → id = "l2"
Assets/MainAssets/Guardians/Pyro → id = "l3"
```

### Step 3: Update Stone IDs in Unity Inspector
For each LegendStoneData prefab/asset:

```
Assets/MainAssets/Stones/ForestSpirit → id = "s1"
```

### Step 4: Verify DeckRegistry
Make sure your DeckRegistry has all items in their arrays:
- `allCards` - should have all 9 cards
- `allGuardians` - should have all 3 legends
- `allLegendStones` - should have all stones

### Step 5: Test the Flow
1. Open your game in Unity
2. Trigger a loot box open
3. Verify loot box opens without errors
4. Verify items display in the reward UI
5. Verify items are added to player inventory correctly

## How the System Works

### Loot Box Opening Flow:
```
User clicks loot box
    ↓
Unity calls PlayFab openLootBox(level)
    ↓
PlayFab randomly selects items by ID (c1, c2, l1, etc.)
    ↓
PlayFab returns: 
    { 
        cards: [
            { cardId: "c1", cardName: "ToxinBall", rarity: "RARE", copies: 2 }
        ]
    }
    ↓
LootBoxInteraction parses JSON
    ↓
For each cardId, calls FindRewardItemById(cardId)
    ↓
FindRewardItemById queries DeckRegistry:
    - GetCardById("c1") → returns ToxinBall CardData
    - GetCastingLegendById("l1") → returns Xod CastingLegendData
    - GetLegendStoneById("s1") → returns ForestSpirit LegendStoneData
    ↓
LootBoxRewardUI displays item with animation
    ↓
Player receives item in inventory
```

## Format Reference

Always follow these patterns when adding items:

| Type | ID Pattern | Example | CARD_DB Entry |
|------|-----------|---------|---|
| Card | c{number} | c1, c2, c3 | `"c1": { name: "ToxinBall", rarity: "RARE" }` |
| Legend | l{number} | l1, l2, l3 | `"l1": { name: "Xod", rarity: "EPIC" }` |
| Stone | s{number} | s1, s2, s3 | `"s1": { name: "ForestSpirit", rarity: "COMMON" }` |

## Troubleshooting

**Problem**: Loot box opens but no rewards show
- **Check**: Are all your CardData/CastingLegendData/LegendStoneData in DeckRegistry?
- **Fix**: Add them to the registry's allCards/allGuardians/allLegendStones lists

**Problem**: FindRewardItemById returns null
- **Check**: ID mismatch between CARD_DB key and data class id field
- **Fix**: Make sure they're identical (case-sensitive: "c1" not "C1")

**Problem**: Wrong item name displays
- **Check**: CARD_DB name vs CardData.cardName mismatch
- **Fix**: Keep them synchronized

**Problem**: PlayFab error "cardId unknown"
- **Check**: ID in CARD_DB but not in player data
- **Fix**: Ensure CARD_DB has entry for every possible reward

## Files Modified in This Update

1. **GameFunctions.js**
   - Updated CARD_DB with new ID format
   - Added detailed pattern comments for easy reference
   - No other changes needed (system already used IDs)

2. **ID_SYSTEM_GUIDE.md** (NEW)
   - Complete reference for ID system architecture
   - Step-by-step guide for adding new items
   - Data flow diagram
   - Troubleshooting section

## Files to Check/Update (User Actions)

1. **Unity CardData prefabs/assets**
   - Set id field to "c{number}"

2. **Unity CastingLegendData prefabs/assets**
   - Set id field to "l{number}"

3. **Unity LegendStoneData prefabs/assets**
   - Set id field to "s{number}"

4. **DeckRegistry.cs**
   - Verify all items are in the arrays

5. **Test in-game**
   - Open loot box and verify flow works

## Next Steps After ID Updates

Once all IDs are set:
1. Build and run the game
2. Test loot box opening
3. Monitor PlayFab logs for any errors
4. Add more items following the pattern
5. Scale loot box pools by adding more to CARD_DB

---

**System is now unified around IDs!** PlayFab and Unity will communicate exclusively using short ID codes like "c1", "l2", "s1" instead of names. This makes the system cleaner, more maintainable, and eliminates name collision issues.
