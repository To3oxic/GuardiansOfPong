This technical overview provides an in-depth look at the architecture and systems of the current Unity project, which appears to be a competitive multiplayer "Battle Pong" or "Hero Pong" game.

## 1. Project Description
This project is a competitive, real-time multiplayer game that blends classic Pong mechanics with hero-based card strategy. Players control paddles to bounce a ball while simultaneously playing cards from a deck to summon units, cast spells, and trigger legendary abilities. The goal is to defend your goal line while reducing the opponent's paddle HP or destroying their "Legend Stone." It features a cloud-integrated backend for progression, deck management, and matchmaking.

## 2. Gameplay Flow / User Loop
1.  **Authentication**: Users log in via email/password using the `PlayFabBackendManager`.
2.  **Preparation**: In the Main Menu, players select or customize their deck of cards, choose a "Casting Legend," and a "Legend Stone."
3.  **Matchmaking**: Players enter a queue managed by PlayFab CloudScripts. The `HeadlessServer` facilitates the connection to a dedicated game instance.
4.  **Battle Loop**: 
    -   **Movement**: Players move their paddles horizontally to intercept the ball.
    -   **Resource Management**: Mana generates over time (managed by `GameManager`).
    -   **Card Play**: Players drag cards onto the board to spawn units or effects.
    -   **Combat**: Spawned units automatically navigate toward the opponent's side, attacking enemy units or the opponent's paddle.
5.  **State Transition**: If a paddle's HP drops below 50%, the `LegendStoneController` triggers a "transformation," granting a passive ability.
6.  **Resolution**: The match ends when a timer expires (score-based), a paddle's HP reaches zero, or a Legend Stone is destroyed.

## 3. Architecture
The project uses a **Server-Authoritative** model powered by **Unity Netcode for GameObjects (NGO)**.

-   **Game Management**: `GameManager` acts as the central hub, managing game state, scoring, timers, and high-level RPC coordination. It is a singleton `NetworkBehaviour`.
-   **Backend Integration**: `PlayFabBackendManager` handles all communication with PlayFab services (Auth, Data, CloudScript). It caches player data and handles secure deck validation.
-   **Networking**: `HeadlessServer` manages the lifecycle of game instances and client connections. `NetworkTrafficOptimizer` is present to handle sync frequency.
-   **Data-Driven Design**: Most game entities (Cards, Legends, Units) are defined via `ScriptableObjects`, allowing for easy tuning and expansion without code changes.

`Location: Assets/Scripts`

## 4. Game Systems & Domain Concepts

### Combat & Unit System
The game uses a hierarchical unit system where all summoned entities derive from a base class.
-   `BaseUnitController`: The core class for all summoned units. It handles HP (via `IHasHP`), manual movement, target searching (`Physics.OverlapSphere`), and animation-driven attacks.
-   `IPlaceable`: An interface implemented by any entity that can be targeted and damaged on the board.
-   **Spawn Flow**: A multi-step process involving client-side previews, server-side validation, and networked animation drop-ins.

`Location: Assets/Scripts/Cards` (Derived units) and `Assets/Scripts/BaseUnitController.cs`

### Card & Deck System
-   `CardManager`: Manages the player's "hand" (represented as a circular deck). It handles input for playing cards and communicates with the server via `ApplyCardEffectServerRpc`.
-   `CardData`: A `ScriptableObject` containing mana cost, prefab references, and an associated `CardEffect`.
-   `CardEffect`: An abstract class that defines what happens when a card is played (e.g., spawning a unit, applying a buff).

`Location: Assets/Scripts/ScriptableObjects` and `Assets/Scripts/CardManager.cs`

### Paddle & Ball Mechanics
-   `PaddleController`: Handles player movement, mana storage, and local player input. It tracks HP and triggers "Legend Stone" transformations.
-   `BallController`: Manages the physics-defying Pong ball. It uses manual position updates rather than pure Rigidbody physics for deterministic networking.

`Location: Assets/Scripts/PaddleController.cs`, `Assets/Scripts/BallController.cs`

## 5. Scene Overview
-   **MainMenu**: The entry point. Handles login, deck selection, and matchmaking.
-   **GameScene**: The primary gameplay scene containing the arena, goals, and game-specific UI.
-   **HeadlessServer**: A specialized scene for running the game on a dedicated server without a GUI.

`Location: Assets/Scenes`

## 6. UI System
The project primarily uses **UGUI** for its interface.
-   **Main Menu UI**: Managed by `MainMenuManager` and `MatchmakingUIManager`. It handles the transition from login to deck selection.
-   **In-Battle HUD**: `GameHUD` and `ManaVisualizer` provide real-time feedback on HP and Mana.
-   **Card UI**: `CardHolderUI` and `CardDragHandler` handle the visual representation of cards in the player's hand and the drag-and-drop logic for playing them.
-   **Health Bars**: Units use a world-space Canvas for health bars, while paddles use screen-space bars managed by the `GameManager`.

`Location: Assets/Scripts/UI`

## 7. Asset & Data Model
-   **DeckRegistry**: A central `ScriptableObject` that acts as a database for all Cards, Legends, and Stones in the game.
-   **PlayerDeckData**: Stores the player's current selection of cards.
-   **PlayFab Data**: Player progression (card levels, quantities, and currency) is stored in PlayFab's "Internal Data" and retrieved via CloudScript to prevent cheating.
-   **Prefabs**: Units, projectiles, and UI elements are heavily prefabbed to support the `DeckRegistry` lookup system.

`Location: Assets/Scripts/ScriptableObjects` and `Assets/Resources`

## 8. Notes, Caveats & Gotchas
-   **Manual Movement**: Units and the Ball use manual `transform.position` updates instead of Rigidbody velocities to ensure consistency across the network.
-   **Z-Axis Convention**: Player 1 (Host) is usually positioned at the bottom of the screen (positive Z movement toward opponent), while Player 2 is at the top.
-   **Spawn Timing**: There is a specific 5-second "spawning" state for units during which they are non-interactive and play a drop animation.
-   **Mana Sync**: Mana is calculated on the server and synced to clients via `OnManaGeneratedClientRpc`. Using local mana for anything other than UI prediction will cause desyncs.
-   **Card Levels**: Card levels are applied server-side during the `ApplyCardEffectServerRpc` by looking up the player's deck payload. Always ensure the `DeckRegistry` is initialized before accessing card data.