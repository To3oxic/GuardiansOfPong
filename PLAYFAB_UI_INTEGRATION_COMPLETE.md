# PlayFab Integration - Existing UI Classes Updated

## Classes Modified to Support PlayFab

All existing MainMenu UI classes now support BOTH old backend and new PlayFab backend without rebuilding.

### 1. SelectionCardManager.cs
**Purpose:** Manages card selection and deck submission

**Changes:**
- ✅ `SubmitDeck()` now auto-detects backend (PlayFab or Old)
- ✅ If PlayFab available: Calls `PlayFabBackendManager.SaveDeck()` with card names
- ✅ If old backend: Falls back to original `BackendManager.SaveSelectedDeck()`
- ✅ Works with existing UI without changes

**API Call:**
```csharp
// Automatically chooses backend
SubmitDeck();
```

---

### 2. CardSelectionPopulator.cs
**Purpose:** Populates card list UI with all player cards

**Changes:**
- ✅ Listens for BOTH old backend login AND PlayFab login
- ✅ Auto-loads from PlayFab on startup if available
- ✅ New method `PopulateFromPlayFab()` handles PlayFab card data
- ✅ New helper methods: `GetCardDataByName()`, `GetGuardianByName()`, `GetLegendStoneByName()`

**Flow:**
```
Scene loads
    ↓
CardSelectionPopulator starts
    ↓
Checks if PlayFabBackendManager exists
    ↓
If yes → Loads cards from PlayFab collection
If no → Waits for old BackendManager.OnLoginSuccess
    ↓
Populates UI with cards
```

**Key Feature:** Works with card NAMES instead of registry_name, so it's compatible with PlayFab's simpler card system.

---

### 3. DeckSubmitButton.cs
**No changes needed** - Works with SelectionCardManager which now supports both backends

---

### 4. SelectionUI.cs
**No changes needed** - Works with SelectionCardManager which now supports both backends

---

## How It Works Together

```
┌─────────────────────────────────────────────┐
│         Game Start                          │
└────────────┬────────────────────────────────┘
             ↓
    ┌────────────────────┐
    │ CardSelectionUI    │
    │ (Container)        │
    └────────┬───────────┘
             ↓
    ┌────────────────────────────┐
    │ CardSelectionPopulator     │ ← Populates cards
    │ - Checks PlayFab backend   │
    │ - Loads cards              │
    │ - Creates UI               │
    └────────┬───────────────────┘
             ↓
    ┌────────────────────────────┐
    │ SelectionCardManager       │ ← Manages selection
    │ - User selects cards       │
    │ - Validates deck           │
    └────────┬───────────────────┘
             ↓
    ┌────────────────────────────┐
    │ DeckSubmitButton           │ ← Submit deck
    │ - Calls SubmitDeck()       │
    │ - Auto-detects backend     │
    │ - Calls cloud function     │
    └────────┬───────────────────┘
             ↓
    ┌────────────────────────────┐
    │ Backend Saves Deck         │
    │ PlayFab: SaveDeck()        │
    │ Old: SaveSelectedDeck()    │
    └────────────────────────────┘
```

---

## Setup Instructions

### For PlayFab:
1. Add `PlayFabBackendManager` to scene with Title ID set
2. Run game
3. CardSelectionPopulator auto-detects and loads from PlayFab
4. Select cards
5. Click Submit → Calls PlayFab cloud function

### For Old Backend:
1. Keep old `BackendManager` in scene
2. Run game
3. CardSelectionPopulator waits for login
4. Select cards
5. Click Submit → Calls old backend

### Mixed Mode:
1. Add BOTH managers to scene
2. PlayFab takes priority (checked first)
3. Falls back to old backend if PlayFab not found

---

## Backward Compatibility

✅ **All existing code works unchanged**
- No rebuild of UI needed
- No prefab changes needed
- Just add PlayFabBackendManager to scene

✅ **Graceful fallback**
- If PlayFab missing → Uses old backend
- If old backend missing → Uses PlayFab
- If both missing → Shows error

---

## What Data Structure Each Backend Uses

### Old Backend (PlayerData)
```csharp
playerData.all_cards[]
├─ card (CardData)
│  ├─ cardName
│  ├─ registry_name
│  └─ card_type
├─ level
└─ quantity
```

### PlayFab Backend (PlayerCardCollection)
```csharp
cardCollection.cards[]
├─ cardName      // Simple name, no registry
├─ level
├─ quantity
├─ owned
└─ cardId        // Internal ID for lookups
```

**CardSelectionPopulator** bridges both formats by:
- Taking old backend's `registry_name` and looking up by name
- Taking PlayFab's `cardName` and looking up by name
- Both eventually find same `CardData` in `DeckRegistry`

---

## Testing Checklist

- [ ] Old backend still works (if you have it)
- [ ] PlayFab backend loads cards correctly
- [ ] Cards display in selection UI
- [ ] Can select cards
- [ ] Submit deck saves to correct backend
- [ ] Check console for debug logs

---

## Files Modified

```
Assets/Scripts/UI/MainMenu/
├─ SelectionCardManager.cs ← UPDATED
├─ CardSelectionPopulator.cs ← UPDATED
├─ DeckSubmitButton.cs (unchanged)
└─ SelectionUI.cs (unchanged)
```

---

## Key Methods Added/Modified

### SelectionCardManager
```csharp
public void SubmitDeck()
  // Auto-detects backend and saves deck
```

### CardSelectionPopulator
```csharp
private void PopulateFromPlayFab(PlayerCardCollection collection)
  // Load cards from PlayFab

private void SetupPlayFabCard(SelectionCardHolder holder, PlayerCardOwnership card)
  // Setup UI for PlayFab card

private CardData GetCardDataByName(string cardName)
  // Find card by name in registry

private CastingLegendData GetGuardianByName(string guardianName)
  // Find guardian by name

private LegendStoneData GetLegendStoneByName(string stoneName)
  // Find stone by name
```

---

## Summary

✅ All existing MainMenu UI classes now work with **both backends**
✅ No UI rebuilding needed
✅ Automatic backend detection
✅ Graceful fallback
✅ PlayFab takes priority if both exist
✅ Backward compatible with old backend

**Just add PlayFabBackendManager to scene and everything works!**
