# Menu Cube Setup Guide

## Architecture Overview

Instead of scrolling horizontally, the entire menu is a **rotating cube** with 3 sides:

```
              ┌─────────────┐
              │             │
              │    SHOP     │ (X+) Rotate Right
              │             │
    ┌─────────┼─────────────┼─────────┐
    │         │             │         │
    │  SHOP   │  MAIN MENU  │  DECK   │
    │(X-)     │   (Front)   │SELECTION│ (X+)
    │         │             │         │
    └─────────┼─────────────┼─────────┘
              │             │
              │    DECK     │ (Y-) Rotate Down
              │SELECTION    │
              └─────────────┘

Main Menu Face (Front - Z+):
- Game board with patrol units
- Player info display
- Start Game button
- Rotation hints (← Deck | Shop →)

Deck Selection Face (Right - X+):
- Legend guardian standing
- Card grid display
- Deck info and stats

Shop Face (Left - X-):
- Shop items/cosmetics
- Currency display
- Purchase buttons
```

## Step-by-Step Setup

### Step 1: Create Cube Object

1. Create empty GameObject "MenuCube" at (0, 0, 0)
2. This will be the parent that rotates
3. Create three child empty GameObjects:
   - `MainMenuFace` (Position: 0, 0, 0)
   - `DeckSelectionFace` (Position: 10, 0, 0)
   - `ShopFace` (Position: -10, 0, 0)

```
MenuCube (rotation root)
├── MainMenuFace (Front)
│   ├── Board Visual
│   ├── Units Container
│   ├── MainMenuUI Canvas
│   └── UI Panel (Player Info, Start Button)
├── DeckSelectionFace (Right, X+ offset)
│   ├── Legend Spawn Point
│   ├── Card Grid Container
│   ├── DeckUI Canvas
│   └── UI Panel (Legend Name, Deck Info)
└── ShopFace (Left, X- offset)
    ├── Shop Items Grid
    ├── ShopUI Canvas
    └── UI Panel (Currency, Purchase Buttons)
```

### Step 2: Position Faces for 3D Look

Each face should be positioned so they form cube sides when rotated:

**Positioning (critical for 3D cube appearance):**
- MainMenuFace: Local rotation (0, 0, 0), Local position (0, 0, 5)
- DeckSelectionFace: Local rotation (0, -90, 0), Local position (5, 0, 0)
- ShopFace: Local rotation (0, 90, 0), Local position (-5, 0, 0)

This creates a cube with 5-unit radius.

### Step 3: Setup Content in Each Face

#### MainMenuFace Content:
```
MainMenuFace/
├── Board (Plane or 3D Model)
├── Units (Patrol units)
├── UI Canvas
│   ├── PlayerInfo Panel
│   │   ├── LevelText
│   │   ├── RankText
│   │   └── CoinsText
│   └── ControlButton
│       └── StartGameButton
└── HintText (← Deck | Deck →)
```

#### DeckSelectionFace Content:
```
DeckSelectionFace/
├── Background (Room sprite/3D)
├── LegendSpawnPoint (Transform)
├── CardGridParent
├── UI Canvas
│   ├── LegendNameText
│   ├── DeckInfoPanel
│   └── ConfirmButton (if needed)
└── HintText (← Main Menu | Shop →)
```

#### ShopFace Content:
```
ShopFace/
├── ShopItemsGrid
├── UI Canvas
│   ├── CurrencyDisplay
│   ├── ShopItemsList
│   └── PurchaseButtons
└── HintText (← Deck | Main Menu →)
```

### Step 4: Attach Scripts

1. **MenuCube (parent)**
   - Add `MenuCubeRotator` script
   - Assign face references:
     - Main Menu Face → mainMenuFace
     - Deck Selection Face → deckSelectionFace
     - Shop Face → shopFace
   - Set rotation speed (2 is good default)
   - Set rotation duration (0.8 seconds)

2. **MainMenuFace**
   - Add `GameBoardZone` script (modified)
   - Initialize board visuals and unit spawning

3. **DeckSelectionFace**
   - Add `CardSelectionZone` script (modified)
   - Initialize legend and card grid

4. **ShopFace**
   - Create `ShopZone` script (new)
   - Initialize shop items and currency display

### Step 5: Modify Existing Scripts

**MainMenuManager.cs** needs update:
```csharp
[SerializeField] private MenuCubeRotator menuCubeRotator;

// Instead of:
// scrollController.Initialize(gameBoardZone.transform, cardSelectionZone.transform);

// Do:
// menuCubeRotator.Initialize();
// This handles all face initialization internally
```

**ScrollController.cs** is no longer needed - replace with MenuCubeRotator

**GameBoardZone.cs** - No changes needed, just attach to MainMenuFace

**CardSelectionZone.cs** - No changes needed, just attach to DeckSelectionFace

### Step 6: Camera Setup

Camera stays stationary looking at cube center:

```csharp
// Camera position
Position: (0, 1.5, -15)
Rotation: (0, 0, 0)
FOV: 60

// This allows cube to rotate in front of camera
// All faces are visible as it rotates
```

### Step 7: Rotation Control

**Input Methods:**
- Mouse Wheel Up/Down
- Right Arrow / Left Arrow
- Number Keys: 1 (MainMenu), 2 (Deck), 3 (Shop)
- Mobile Swipe (add later)

**Rotation Behavior:**
```
Current Face         Right Arrow          Left Arrow
─────────────────────────────────────────────────────
Main Menu    →  Deck Selection   /   Shop
Deck Selection →  Shop           /   Main Menu
Shop         →  Main Menu        /   Deck Selection
```

### Step 8: Visual Polish

Add visual indicators for better UX:

```csharp
// On each face, add subtle text hints:
MainMenuFace:   "→ Deck Selection | Shop ←"
DeckSelectionFace: "→ Shop | Main Menu ←"
ShopFace:       "→ Main Menu | Deck Selection ←"

// Add rotation sound effect (optional)
// Play whoosh sound when cube rotates
```

### Step 9: Testing Checklist

- [ ] Cube rotates smoothly when pressing arrow keys
- [ ] Cube rotates smoothly with mouse wheel
- [ ] Number keys 1, 2, 3 jump to correct faces
- [ ] All content is visible on each face
- [ ] Camera stays centered while cube rotates
- [ ] Can rotate back and forth without issues
- [ ] UI is readable on all faces
- [ ] Units patrol correctly on MainMenuFace
- [ ] Legend displays on DeckSelectionFace
- [ ] Shop displays on ShopFace

## File Structure After Complete

```
Assets/
├── Scripts/
│   ├── UI/
│   │   └── MainMenu/
│   │       ├── MainMenuManager.cs
│   │       ├── MenuCubeRotator.cs (new)
│   │       ├── GameBoardZone.cs
│   │       ├── CardSelectionZone.cs
│   │       ├── ShopZone.cs (create next)
│   │       ├── UnitSpawner.cs
│   │       └── PatrolBehavior.cs
├── Scenes/
│   └── MainMenu.unity
└── Prefabs/
    ├── UI/
    └── Units/
```

## Advanced Features (Later)

- [ ] Cube face peeking (slightly rotate to see next face)
- [ ] Swipe gestures on mobile
- [ ] Particle effects on rotation
- [ ] Sound effects (whoosh on rotate, click on select)
- [ ] Smooth transition animations between faces
- [ ] Parallax effect (background rotates slower)
- [ ] Face unlock animations (new shop items)

## Quick Reference: Rotation Values

```
Facing       Y Rotation    Input Sequence
─────────────────────────────────────────
Main Menu         0°       Press 1 (or start)
Deck Selection   -90°      Press 2 (or Right Arrow)
Shop             +90°      Press 3 (or Left Arrow)

Lerp smoothly between these rotations for organic feel
```

## Performance Optimization

- Each face can have its own Canvas for independent UI rendering
- Disable physics/scripts on inactive faces (optional optimization)
- Use object pooling for card displays
- Cache material references
- Consider LOD on 3D models if performance needed
