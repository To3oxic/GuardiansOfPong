# Menu Cube - Quick Start Guide

## What Changed

✅ **Old System** (Horizontal Scroll)
- Camera pans left/right
- Static zones
- Linear feel

❌ **New System** (Rotating Cube)
- Cube rotates
- Camera stays still
- 3D immersive feel
- Easier to add more "sides" later

## 3 Faces of Your Menu Cube

### Face 1: Main Menu (Front) - Y Rotation 0°
```
┌─────────────────────┐
│  MAIN MENU FACE     │
│                     │
│  ↙ Board Visual ↙   │
│  ↙ Patrol Units ↙   │
│                     │
│ Player: Lv10 $500  │
│                     │
│   [START GAME]      │
│                     │
│ "→ Deck | Shop ←"  │
└─────────────────────┘
```

### Face 2: Deck Selection (Right) - Y Rotation -90°
```
┌─────────────────────┐
│ DECK SELECTION FACE │
│                     │
│  Legend Standing    │
│  (Idle Animation)   │
│                     │
│  ┌─────────────┐    │
│  │ Card1 │ C2  │    │
│  │ Card3 │ C4  │    │
│  └─────────────┘    │
│                     │
│ "→ Shop | Menu ←"  │
└─────────────────────┘
```

### Face 3: Shop (Left) - Y Rotation +90°
```
┌─────────────────────┐
│    SHOP FACE        │
│                     │
│  Coins: $500        │
│                     │
│  ┌─────────────┐    │
│  │ Item1 $100  │    │
│  │ Item2 $250  │    │
│  │ Item3 $500  │    │
│  └─────────────┘    │
│                     │
│ "→ Menu | Deck ←"  │
└─────────────────────┘
```

## Cube Hierarchy Structure

```
MenuCube (Parent - ROTATES)
├── MainMenuFace
│   ├── Board Visual
│   ├── UnitSpawner
│   ├── Canvas (UI)
│   └── HintText
├── DeckSelectionFace
│   ├── Background
│   ├── LegendSpawnPoint
│   ├── CardGridParent
│   ├── Canvas (UI)
│   └── HintText
└── ShopFace
    ├── ShopItemsGrid
    ├── Canvas (UI)
    └── HintText
```

## Camera Setup

```
Position: (0, 1.5, -15)
Rotation: (0, 0, 0)
Type: Perspective or Orthographic

This keeps camera still while cube rotates in front
```

## Rotation Controls

| Input | Action |
|-------|--------|
| Right Arrow | Next face (clockwise) |
| Left Arrow | Previous face (counter-clockwise) |
| Mouse Wheel Up | Next face |
| Mouse Wheel Down | Previous face |
| Key 1 | Jump to Main Menu |
| Key 2 | Jump to Deck Selection |
| Key 3 | Jump to Shop |

## Rotation Sequence

```
Press Right/Mouse Up:
Main Menu → Deck Selection → Shop → Main Menu (loops)

Press Left/Mouse Down:
Main Menu → Shop → Deck Selection → Main Menu (loops)
```

## Scripts Summary

| Script | Purpose | Attached To |
|--------|---------|------------|
| **MenuCubeRotator** | Handles cube rotation, input | MenuCube (parent) |
| **GameBoardZone** | Main menu content | MainMenuFace |
| **CardSelectionZone** | Deck selection content | DeckSelectionFace |
| **ShopZone** | Shop content | ShopFace |
| **MainMenuManager** | Orchestrates all zones | Separate GameObject |

## Setup Checklist

- [ ] Create MenuCube empty GameObject
- [ ] Create 3 face children: MainMenuFace, DeckSelectionFace, ShopFace
- [ ] Position faces: Main(0,0,5), Deck(5,0,0), Shop(-5,0,0)
- [ ] Add MenuCubeRotator script to MenuCube
- [ ] Add GameBoardZone to MainMenuFace
- [ ] Add CardSelectionZone to DeckSelectionFace
- [ ] Add ShopZone to ShopFace
- [ ] Add MainMenuManager to separate GameObject
- [ ] Assign references in Inspector
- [ ] Create Camera looking at (0, 1.5, -15)
- [ ] Test rotation with arrow keys

## Next: Create Remaining Scripts

- [ ] **UnitSpawner.cs** - Spawn patrol units
- [ ] **PatrolBehavior.cs** - Unit patrol logic
- [ ] **ShopItem.cs** - Individual shop item behavior
- [ ] **PlayerData.cs** - Cache player info

## File Locations

```
Assets/Scripts/UI/MainMenu/
├── MenuCubeRotator.cs ✓
├── MainMenuManager.cs ✓
├── GameBoardZone.cs ✓
├── CardSelectionZone.cs ✓
├── ShopZone.cs ✓
├── UnitSpawner.cs (next)
├── PatrolBehavior.cs (next)
└── ShopItem.cs (next)
```

## Why Cube is Better

1. **3D Immersive** - Feels more polished
2. **Future Proof** - Easy to add more faces (4th, 5th side)
3. **Compact** - All menu content in one rotating object
4. **Touch Friendly** - Swipe gestures natural for rotation
5. **Mobile Ready** - Scales well to different screen sizes

## Performance Tips

- Each face can have independent Canvas for UI rendering
- Disable unused face colliders if using physics
- Use object pooling for card displays
- Cache all animator references
- Consider LOD for 3D models if needed
