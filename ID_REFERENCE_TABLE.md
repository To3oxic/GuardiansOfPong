# ID Reference Table

## Quick Lookup - All Item IDs

### Cards (Abilities/Spells) - Format: `c{number}`

| ID | Name | Rarity |
|---|---|---|
| c1 | ToxinBall | RARE |
| c2 | BraveKnight | COMMON |
| c3 | Vanish | COMMON |
| c4 | LightningBall | EPIC |
| c5 | AncientSpirit | EPIC |
| c6 | AngryBear | RARE |
| c7 | ForestMonsters | COMMON |
| c8 | StoneSentinel | RARE |
| c9 | Tree | COMMON |

### Legends (Guardians) - Format: `l{number}`

| ID | Name | Rarity |
|---|---|---|
| l1 | Xod | EPIC |
| l2 | Boulder | COMMON |
| l3 | Pyro | RARE |

### Legend Stones (Passive Abilities) - Format: `s{number}`

| ID | Name | Rarity |
|---|---|---|
| s1 | ForestSpirit | COMMON |

## Implementation Locations

### PlayFab (Backend)
- **File**: `PlayFabCloudFunctions/GameFunctions.js`
- **CARD_DB**: Lines 21-35
- **Usage**: Lines 486-550 (in openLootBox function)
- **Response Format**: Returns `cardId` in rewards array

### Unity (Frontend)
- **Data Files**:
  - `Assets/Scripts/ScriptableObjects/CardData.cs` - has `id` field
  - `Assets/Scripts/ScriptableObjects/CastingLegendData.cs` - has `id` field
  - `Assets/Scripts/ScriptableObjects/LegendStoneData.cs` - has `id` field

- **Registry**: `Assets/Scripts/DeckRegistry.cs`
  - `GetCardById(string id)` - lookup card by ID
  - `GetCastingLegendById(string id)` - lookup legend by ID
  - `GetLegendStoneById(string id)` - lookup stone by ID

- **Loot Box Logic**: `Assets/Scripts/LootBoxInteraction.cs`
  - `FindRewardItemById(string itemId)` - retrieves any item by ID
  - Calls registry methods internally

- **UI Display**: `Assets/Scripts/LootBoxRewardUI.cs`
  - Routes items to correct holder based on type
  - Displays items with animations

## Steps to Complete Setup

### 1. Update CardData Objects in Inspector
For each card prefab/asset:
- Find `id` field
- Set to corresponding value (c1, c2, c3, etc.)
- Match the table above

**Where to find**: `Assets/MainAssets/Cards/` (or wherever your cards are)

### 2. Update CastingLegendData Objects in Inspector
For each legend prefab/asset:
- Find `id` field
- Set to corresponding value (l1, l2, l3, etc.)

**Where to find**: `Assets/MainAssets/Guardians/` (or wherever your legends are)

### 3. Update LegendStoneData Objects in Inspector
For each stone prefab/asset:
- Find `id` field
- Set to corresponding value (s1, s2, etc.)

**Where to find**: `Assets/MainAssets/Stones/` (or wherever your stones are)

### 4. Verify DeckRegistry Assignment
In DeckRegistry Inspector or script:
- `allCards` array must contain all 9 cards
- `allGuardians` array must contain all 3 legends
- `allLegendStones` array must contain all stones

### 5. Test the System
1. Open the game
2. Navigate to loot box
3. Open a loot box
4. Verify no errors in console
5. Verify rewards display correctly
6. Verify items added to inventory

## Adding New Items in the Future

### Pattern: Cards
- Next card after c9 is c10
- Add to CardData in Unity with `id = "c10"`
- Add to `CARD_DB` in GameFunctions.js: `"c10": { name: "YourCard", rarity: "COMMON" }`
- Add to DeckRegistry.allCards array

### Pattern: Legends
- Next legend after l3 is l4
- Add to CastingLegendData in Unity with `id = "l4"`
- Add to `CARD_DB`: `"l4": { name: "YourLegend", rarity: "EPIC" }`
- Add to DeckRegistry.allGuardians array

### Pattern: Stones
- Next stone after s1 is s2
- Add to LegendStoneData in Unity with `id = "s2"`
- Add to `CARD_DB`: `"s2": { name: "YourStone", rarity: "COMMON" }`
- Add to DeckRegistry.allLegendStones array

## ID System Benefits

✅ **Consistent Communication** - Same IDs used everywhere (PlayFab & Unity)
✅ **Collision Prevention** - No name conflicts (multiple cards can't have same ID)
✅ **Easy Maintenance** - Short codes (c1, l2, s1) vs long names
✅ **Type Safety** - IDs uniquely identify items across all types
✅ **Performance** - Integer/string lookup faster than name comparison
✅ **Scalability** - Easy to add more items without renaming existing ones

## Verification Checklist Before Testing

- [ ] All CardData objects have `id` set (c1-c9)
- [ ] All CastingLegendData objects have `id` set (l1-l3)
- [ ] All LegendStoneData objects have `id` set (s1+)
- [ ] DeckRegistry contains all items in respective arrays
- [ ] CARD_DB in GameFunctions.js matches table above
- [ ] No syntax errors in GameFunctions.js
- [ ] No missing entries in CARD_DB
- [ ] No duplicate IDs in any data type

## Common Errors & Fixes

**Error**: "Awarded unknown cardId (missing in CARD_DB): c1"
- **Fix**: Add entry to CARD_DB - verify it's spelled correctly

**Error**: FindRewardItemById returns null
- **Fix**: Check if ID is in DeckRegistry arrays - add if missing

**Error**: Item displays wrong name
- **Fix**: Verify CARD_DB `name` field matches CardData `cardName` field

**Error**: Loot box opens but rewards don't show
- **Fix**: Check DeckRegistry has all items - add missing items to arrays

---

**System Status**: ✅ **READY FOR ID UPDATE IN INSPECTOR**

All backend code is updated and ready. Now update your Unity Inspector to set the `id` field on all data objects using the table above!
