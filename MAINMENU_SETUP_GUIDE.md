# MainMenu Scene Setup Instructions

## Step-by-Step Scene Creation

### Step 1: Scene Structure in Hierarchy

Create this hierarchy in your MainMenu scene:

```
MainMenuScene
├── Canvas (UI Root)
│   ├── GameBoard Zone UI Panel
│   │   ├── PlayerInfo Panel
│   │   │   ├── LevelText
│   │   │   ├── RankText
│   │   │   └── CoinsText
│   │   └── StartButton
│   └── CardSelection Zone UI Panel
│       ├── LegendNameText
│       └── DeckInfoText
├── GameBoard Zone (Parent, offset left)
│   ├── Board Visual (plane or 3D model)
│   ├── LegendSpawnPoint (empty GameObject)
│   ├── UnitSpawner (empty GameObject)
│   └── Units (dynamically spawned here)
├── CardSelection Zone (Parent, offset right, ~20 units away)
│   ├── RoomBackground (sprite or 3D backdrop)
│   ├── LegendSpawnPoint (empty GameObject)
│   ├── CardGrid Container (empty GameObject)
│   └── Legend (dynamically spawned here)
└── Main Camera
    ├── Attach ScrollController script
```

### Step 2: Setup GameObjects

1. **Create Canvas** (for UI)
   - GameObject → UI → Panel
   - Rename to "Canvas"
   - Set to full screen

2. **Create World Space Objects**
   - Create empty GameObject "GameBoardZone" at position (0, 0, 0)
   - Create empty GameObject "CardSelectionZone" at position (20, 0, 0)
   - Adjust Z if needed based on your camera setup

3. **Create Main Camera**
   - Add `ScrollController` script to it
   - Set Main Camera to orthographic or perspective (your choice)
   - Start position looking at GameBoardZone (0, 1, -10)

### Step 3: Add Scripts to GameObjects

1. **Main Camera**
   - Add `ScrollController` script
   - Configure scroll speed (5 is good default)
   - Enable mouse scroll + swipe input

2. **Create MainMenuManager object**
   - Empty GameObject in scene root
   - Add `MainMenuManager` script
   - Assign references:
     - ScrollController → Main Camera
     - GameBoardZone → GameBoardZone child
     - CardSelectionZone → CardSelectionZone child
     - StartGameButton → button in Canvas

3. **GameBoardZone**
   - Add `GameBoardZone` script
   - Create child "UnitSpawner" → Add `UnitSpawner` script

4. **CardSelectionZone**
   - Add `CardSelectionZone` script

### Step 4: Create Prefabs You'll Need

1. **Board Visual**
   - Create a simple plane or import 3D board model
   - Position at GameBoardZone

2. **Unit Spawning**
   - Reuse existing unit prefabs (BraveKnight, etc.)
   - Disable attack/networking components for menu display
   - Create simple patrol behavior

3. **Card Display Prefab**
   - Use existing CardHolderUI prefab
   - Ensure it works without drag handler

### Step 5: Layout Configuration

```
CAMERA VIEW (Orthographic ~30 units wide):

[==== GAMEBOARD ZONE ====]  [==== CARD SELECTION ZONE ====]
- Paddle 1 (visual)         - Legend Standing
- Paddle 2 (visual)         - Card Grid
- 3-5 Patrol Units          - Deck Info
- Player Info UI            - Legend Name UI
- Start Button
  (scroll right →)
```

### Step 6: Scripting Checklist

- [ ] `MainMenuManager.cs` - Main orchestrator
- [ ] `ScrollController.cs` - Camera panning
- [ ] `GameBoardZone.cs` - Left zone setup
- [ ] `CardSelectionZone.cs` - Right zone setup
- [ ] `UnitSpawner.cs` - Spawn patrol units (create next)
- [ ] `PatrolBehavior.cs` - Simple unit patrol (create next)

### Step 7: Next Steps to Implement

#### A. UnitSpawner.cs
- Responsible for spawning units in GameBoardZone
- Takes prefabs and waypoints as parameters
- Manages list of active units

#### B. PatrolBehavior.cs
- Simple state machine for units: Idle → Patrol → Idle
- Two waypoints: move between them on loop
- Play walk animation during patrol
- Play idle animation at waypoints

#### C. Player Data Manager
- Fetch player info from database (TODO)
- Cache player's deck
- Provide static access to `PlayerData.GetPlayerInfo()`

#### D. UI Updates
- Display player level, rank, coins
- Display deck card count
- Show legend name in card selection zone

### Step 8: Testing Checklist

- [ ] Scroll right/left with mouse wheel or arrow keys
- [ ] Camera smoothly transitions between zones
- [ ] Units appear and patrol in GameBoardZone
- [ ] Legend appears in CardSelectionZone
- [ ] Player info displays correctly
- [ ] Start button is clickable (log message for now)
- [ ] Card grid displays (with placeholder cards if needed)

### Step 9: Performance Optimization

- Use object pooling for units if spawning many
- Disable unit AI when not visible (OnBecameVisible/Invisible)
- Cache Animator references
- Use sprite atlases for UI cards

---

## Quick Reference: Coordinate System

```
Top-Down View:

     Z (up on screen)
     ↑
     |
     |---- X (right on screen)
    /
   Y (depth)

GameBoardZone:   (0, 0, 0) - Left side
CardSelectionZone: (20, 0, 0) - Right side
Camera Start:    (0, 1, -10)
Camera Board:    (0, 1, -10) when on GameBoard
Camera CardSel:  (20, 1, -10) when on CardSelection
```

## Database Integration Placeholders

Once you have backend ready, replace these TODOs:

```csharp
// In MainMenuManager.LoadPlayerData()
// TODO: Connect to backend API
// PlayerData.LoadFromDatabase(userId);

// In GameBoardZone.SpawnPatrollingUnits()
// TODO: Fetch unit list from PlayerData
// CardSelectionZone.SpawnLegend()
// TODO: Fetch player's selected legend

// In CardSelectionZone.CreateCardGrid()
// TODO: Load player's saved deck from PlayerData
```

---

## File Structure After Complete

```
Assets/
├── Scripts/
│   ├── UI/
│   │   └── MainMenu/
│   │       ├── MainMenuManager.cs
│   │       ├── ScrollController.cs
│   │       ├── GameBoardZone.cs
│   │       ├── CardSelectionZone.cs
│   │       ├── UnitSpawner.cs (create next)
│   │       └── PatrolBehavior.cs (create next)
│   ├── Player/
│   │   └── PlayerData.cs (integrate later)
├── Scenes/
│   └── MainMenu.unity
├── Prefabs/
│   ├── UI/
│   │   └── CardHolderUI.prefab
│   └── Units/
│       ├── BraveKnight_Menu.prefab (modified for menu)
```
