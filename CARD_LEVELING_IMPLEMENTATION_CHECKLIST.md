# Card Leveling System - Implementation Checklist

## Phase 1: Core System ✅ COMPLETE

### Code Changes
- [x] Update `CardOwnershipModels.cs`
  - [x] Add `quantity` field to `PlayerCardOwnership`
  - [x] Update `level` to use 0-5 scale (0=locked)
  - [x] Add `GetDisplayStatus()` method
  - [x] Add `GetNextLevelProgress()` method
  - [x] Add `GetCardQuantity()` to `PlayerCardCollection`
  - [x] Update `AcquireCard()` with quantity + auto-level
  - [x] Add `TryLevelUpCard()` method
  - [x] Add `CardLevelingConfig` class
  - [x] Update `CardLibraryEntry` with leveling costs

- [x] Update `PlayFabBackendManager.cs`
  - [x] Replace `AwardCardToPlayer()` signature (quantity-based)
  - [x] Add `LevelUpCard()` method
  - [x] Update `LoadCardLibraryFromDeckRegistry()` with configs
  - [x] Update card initialization

- [x] Verify Compilation
  - [x] `CardOwnershipModels.cs` - No errors ✅
  - [x] `PlayFabBackendManager.cs` - No errors ✅
  - [x] `DeckSelectionUIPlayFab.cs` - No errors ✅

### Documentation Created
- [x] `CARD_LEVELING_SYSTEM.md` - Complete API reference
- [x] `CARD_LEVELING_UI_QUICK_REF.md` - UI implementation guide
- [x] `CARD_LEVELING_ARCHITECTURE.md` - System design
- [x] `CARD_LEVELING_SYSTEM_SUMMARY.md` - Quick summary
- [x] `CARD_LEVELING_UI_VISUAL.md` - Visual UI guide

---

## Phase 2: UI Integration 📋 TODO

### Update DeckSelectionUIPlayFab.cs
- [ ] Modify `PopulateCardList()` to display:
  - [ ] Card level (Lv.1-5 or Locked)
  - [ ] Total copies owned (e.g., "3 copies")
  - [ ] Progress bar (3/6)
  - [ ] Owned vs locked visual states

- [ ] Update card button prefab to include:
  - [ ] CardName text
  - [ ] CardLevel text (e.g., "Lv.3")
  - [ ] CardQuantity text (e.g., "3 copies")
  - [ ] ProgressBar (Image or Slider)
  - [ ] ProgressText (e.g., "3/6")

- [ ] Modify `ToggleCard()` to check ownership:
  - [ ] Check if `card.level > 0` (owned)
  - [ ] Show error if locked
  - [ ] Only add selectable cards to deck

- [ ] Add method to refresh card displays after award

### Create Card Collection View (Optional)
- [ ] New UI panel showing all cards with:
  - [ ] Card grid/list view
  - [ ] Level badge
  - [ ] Quantity display
  - [ ] Progress to next level
  - [ ] Optional: Manual level-up button

---

## Phase 3: Loot/Reward Integration 📋 TODO

### Connect to Reward System
- [ ] Battle victory rewards:
  - [ ] Call `AwardCardToPlayer()` with looted card
  - [ ] Pass quantity (typically 1)
  - [ ] Pass leveling config
  - [ ] Show notification with new level

- [ ] Card pack opening:
  - [ ] Generate random cards
  - [ ] Award each with `AwardCardToPlayer()`
  - [ ] Show pack opening animation
  - [ ] Display cards earned + level changes

- [ ] Daily/Weekly rewards:
  - [ ] Award cards with `AwardCardToPlayer()`
  - [ ] Show in reward notification

- [ ] Quest/Campaign rewards:
  - [ ] Award cards on completion
  - [ ] Show in completion screen

- [ ] Shop/IAP purchases:
  - [ ] Award purchased cards
  - [ ] Show purchase confirmation

### Optional: Manual Level-Up Feature
- [ ] Add "Level Up" button to card display
- [ ] Show copies required vs owned
- [ ] Confirm before spending copies
- [ ] Call `LevelUpCard()` on confirmation
- [ ] Show level-up animation

---

## Phase 4: Server-Side Validation 📋 TODO

### Deck Validation
- [ ] Server receives player's selected deck
- [ ] Server calls `LoadActiveDeck()` from PlayFab
- [ ] Server compares:
  - [ ] Same cards present
  - [ ] Same card levels
  - [ ] Same card quantities
- [ ] Disconnect on mismatch + log exploit

### Card Ownership Validation
- [ ] Server loads `player_cards` from PlayFab
- [ ] Verify all cards in deck are owned (level > 0)
- [ ] Verify card levels match saved levels
- [ ] Reject if unowned cards detected

---

## Phase 5: Testing 📋 TODO

### Unit Tests
- [ ] PlayerCardOwnership methods
  - [ ] `GetDisplayStatus()` returns correct string
  - [ ] `GetNextLevelProgress()` shows correct numbers
  - [ ] `CanLevelUp()` calculates threshold correctly

- [ ] PlayerCardCollection methods
  - [ ] `AcquireCard()` with 1, 2, 3 copies
  - [ ] Auto-level triggers at correct thresholds
  - [ ] `TryLevelUpCard()` consumes copies
  - [ ] `GetCardQuantity()` returns correct value

- [ ] CardLevelingConfig
  - [ ] `GetCopiesRequiredForLevel()` returns correct values
  - [ ] `GetTotalCopiesNeededForLevel()` calculates sum correctly

### Integration Tests
- [ ] Award card → Saves to PlayFab ✅
- [ ] Load collection → Deserializes correctly ✅
- [ ] Auto-level → Persists to PlayFab ✅
- [ ] Manual level-up → Consumes copies ✅
- [ ] Multiple awards → Cumulative effect ✅

### E2E Flow Tests
- [ ] [ ] Award 1 card → Unlocks to Lv.1
- [ ] [ ] Award 2 more → Auto-levels to Lv.2
- [ ] [ ] Award 3 more → Auto-levels to Lv.3
- [ ] [ ] Award 4 more → Auto-levels to Lv.4
- [ ] [ ] Award 5 more → Auto-levels to Lv.5
- [ ] [ ] UI displays correct level
- [ ] [ ] UI displays correct quantity
- [ ] [ ] UI displays correct progress
- [ ] [ ] Locked cards unselectable
- [ ] [ ] Owned cards selectable
- [ ] [ ] Deck saves with owned cards
- [ ] [ ] Server validates correctly
- [ ] [ ] Max level prevents further leveling

### Security Tests
- [ ] [ ] Attempt to select locked card → Rejected
- [ ] [ ] Send deck with unowned cards → Server blocks
- [ ] [ ] Send deck with wrong levels → Server blocks
- [ ] [ ] Manipulate local quantity → Server catches it
- [ ] [ ] Multiple exploit attempts → Logged

### UI Tests
- [ ] [ ] Card buttons display correctly
- [ ] [ ] Locked cards are greyed out
- [ ] [ ] Owned cards are white
- [ ] [ ] Selected cards highlight
- [ ] [ ] Progress bars fill correctly
- [ ] [ ] Responsive on mobile/tablet
- [ ] [ ] Text is readable at all sizes

---

## Phase 6: Production Readiness 📋 TODO

### Configuration
- [ ] Set PlayFab title ID
- [ ] Configure card leveling costs:
  - [ ] Common cards (cheaper)
  - [ ] Rare cards (more expensive)
  - [ ] Legendary cards (very expensive)
- [ ] Set default leveling config
- [ ] Configure starter cards

### Documentation Updates
- [ ] Update player-facing documentation
- [ ] Add to wiki/help system
- [ ] Create tutorial/onboarding
- [ ] Document card rarity system

### Performance
- [ ] [ ] Profile card loading time
- [ ] [ ] Profile auto-level calculation
- [ ] [ ] Profile UI refresh time
- [ ] [ ] Optimize if needed

### Monitoring
- [ ] [ ] Set up logging for card awards
- [ ] [ ] Monitor exploit attempts
- [ ] [ ] Track leveling progression
- [ ] [ ] Alert on suspicious patterns

### Launch Checklist
- [ ] [ ] All tests passing
- [ ] [ ] No compilation errors
- [ ] [ ] UI polished and responsive
- [ ] [ ] Documentation complete
- [ ] [ ] QA sign-off
- [ ] [ ] Performance acceptable
- [ ] [ ] Security review passed
- [ ] [ ] Ready for production!

---

## Current Status

### ✅ Complete (Phase 1)
- Core card leveling system implemented
- Auto-leveling based on quantities
- PlayFab integration for persistence
- All compilation errors resolved
- Comprehensive documentation created

### 📋 To Do (Phases 2-6)
- UI updates for quantity display
- Loot/reward system integration
- Server-side deck validation
- Comprehensive testing
- Production deployment

---

## Quick Reference

### Key Methods

**Award Card:**
```csharp
PlayFabBackendManager.Instance.AwardCardToPlayer(
    cardId, cardName, quantity, levelingConfig, callback
);
```

**Level Up Card:**
```csharp
PlayFabBackendManager.Instance.LevelUpCard(
    cardId, copiesRequired, callback
);
```

**Check Ownership:**
```csharp
bool owns = collection.OwnsCard(cardId);  // level > 0
```

**Get Quantity:**
```csharp
int qty = collection.GetCardQuantity(cardId);
```

**Display Status:**
```csharp
string status = card.GetDisplayStatus();  // "Locked" or "Lv.X"
```

---

## File Locations

### Code Files
- `Assets/Scripts/Backend/CardOwnershipModels.cs` ✅
- `Assets/Scripts/Backend/PlayFabBackendManager.cs` ✅
- `Assets/Scripts/DeckSelectionUIPlayFab.cs` (needs UI update)

### Documentation
- `CARD_LEVELING_SYSTEM.md` - API reference
- `CARD_LEVELING_UI_QUICK_REF.md` - UI guide
- `CARD_LEVELING_ARCHITECTURE.md` - System design
- `CARD_LEVELING_SYSTEM_SUMMARY.md` - Overview
- `CARD_LEVELING_UI_VISUAL.md` - Visual examples
- `PLAYFAB_DECK_SECURITY_ANTICHEAT.md` - Anti-cheat
- `PLAYFAB_DECK_SECURITY_INTEGRATION.md` - Server validation

---

## Support Resources

**For UI Implementation:**
See `CARD_LEVELING_UI_QUICK_REF.md` and `CARD_LEVELING_UI_VISUAL.md`

**For API Usage:**
See `CARD_LEVELING_SYSTEM.md`

**For System Architecture:**
See `CARD_LEVELING_ARCHITECTURE.md`

**For Anti-Cheat:**
See `PLAYFAB_DECK_SECURITY_ANTICHEAT.md`

---

## Notes

- Default leveling: 1 → 2 → 3 → 4 → 5 copies required per level
- Cards auto-level immediately when quantity threshold reached
- Manual level-up is optional (player spends copies)
- All data persisted to PlayFab immediately
- Server validates on game start (prevents cheating)
- Locked cards cannot be selected for deck
- Owned cards (level ≥ 1) can be selected

---

## Next Immediate Steps

1. **Update DeckSelectionUIPlayFab.cs** - Add quantity/level/progress display
2. **Integrate with loot system** - Award cards when players get them
3. **Test basic flow** - Award cards, verify UI updates
4. **Implement server validation** - Validate deck on game start
5. **Full QA testing** - All exploit scenarios

**Estimated Timeline:**
- UI Updates: 2-4 hours
- Loot Integration: 2-4 hours
- Testing: 4-8 hours
- Total: 1-2 days for full implementation

---

## Success Criteria

✅ **Phase 1 (DONE)**
- Core system implemented
- Code compiles without errors
- Documentation complete

✅ **Phase 2**
- UI displays all card information
- Cards show level, quantity, progress
- Locked/owned visual distinction clear

✅ **Phase 3**
- Cards awarded through gameplay
- Auto-leveling works in production
- Notifications show new levels

✅ **Phase 4**
- Server validates deck on game start
- Exploits detected and logged
- No invalid decks reach game

✅ **Phase 5**
- All tests passing
- 100% code coverage
- No known bugs

✅ **Phase 6**
- Production ready
- Monitoring active
- User documentation live

---

**System is now ready for Phase 2 UI implementation!**
