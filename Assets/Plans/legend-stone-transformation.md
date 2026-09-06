# Project Overview
- Game Title: Battle Pong (Hero Pong)
- High-Level Concept: Competitive multiplayer Pong with hero cards, units, and spells.
- Players: Online Multiplayer (NGO)
- Target Platform: PC / Mobile (Both Input systems active)
- Screen Orientation: Landscape
- Render Pipeline: URP (Mobile_RPAsset)

# Game Mechanics
## Core Gameplay Loop
Players control paddles to bounce a ball. They use mana (regenerated over time) to play cards that spawn units or cast spells. The game ends when a paddle's HP reaches zero or their Legend Stone is destroyed.

## Legend Stone Transformation
When a paddle's HP drops below 50%, the Legend Stone "transforms":
1. It moves to a "Safe Point" behind the goal.
2. The player receives a passive ability (e.g., speed boost, mana regen).
3. The transformation is a turning point in the match, making the player harder to eliminate.

# UI
- HP bars for both players (already partially implemented).
- Score text (already partially implemented).
- Mana visualizer (already partially implemented).
- NEW: "TRANSFORMED!" notification UI element or effect.

# Key Asset & Context
- `GameManager.cs`: Central hub for game state and transformation logic.
- `PaddleController.cs`: Handles paddle movement, HP, and buffs.
- `LegendStoneController.cs`: Handles the stone's movement and visual state.
- `LegendStoneData.cs`: ScriptableObject containing passive stats (Speed Bonus, Damage Resistance).

# Implementation Steps
## Phase 1: Gameplay Logic (Server-Side)
1. **Enhance PaddleController state**:
    - Add `public NetworkVariable<bool> IsTransformed` to `PaddleController`.
    - Description: Track the transformation state across the network.
    - Assigned role: developer
    - Dependencies: None
2. **Implement Passive Buffs in GameManager**:
    - Modify `ActivatePassiveAbilityForClient(ulong clientId)` in `GameManager.cs`.
    - Logic: Look up `LegendStoneData` for the client's stone, apply `paddleSpeedBonus` to the paddle, and implement a mana regeneration boost (either by decreasing `manaRegenInterval` or increasing `manaRegenRate`).
    - Assigned role: developer
    - Dependencies: Step 1
3. **Integrate Damage Resistance**:
    - Modify `PaddleController.ReduceHP(float damage)`.
    - Logic: If `IsTransformed` is true, reduce incoming damage using a resistance factor from `LegendStoneData`.
    - Assigned role: developer
    - Dependencies: Step 1

## Phase 2: Visuals & Feedback (Client-Side)
4. **Transformation Effects**:
    - Implement `PlayLegendStoneTransformationEffectsClientRpc` in `GameManager.cs`.
    - Logic: Trigger a "Transformation" particle effect at the paddle/stone position and play a dramatic sound.
    - Assigned role: developer
    - Dependencies: None
5. **UI Notification**:
    - Add a simple screen-space "TRANSFORMED" text popup or health bar highlight when transformation occurs.
    - Assigned role: developer
    - Dependencies: Step 4

## Phase 3: Polish & Edge Cases
6. **Losing Condition Integrity**:
    - Verify that `TriggerLegendStoneDefeat` is correctly guarded/handled when the stone is at the Safe Point.
    - Assigned role: developer
    - Dependencies: None

# Verification & Testing
- **Manual Test**: Run the game in Host/Client mode. Reduce a player's HP below 50%.
- **Check**: Legend stone moves to the safe point.
- **Check**: Paddle speed noticeably increases.
- **Check**: Mana regen rate increases.
- **Check**: "TRANSFORMED" visual feedback triggers on both clients.
- **Edge Case**: Ensure HP can still drop to 0 after transformation (unless specified otherwise).
