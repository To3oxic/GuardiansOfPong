# Project Overview 
- Game Title: Battle Pong / Hero Pong
- High-Level Concept: Competitive, real-time multiplayer game blending classic Pong with hero-based card strategy.
- Players: 1v1 Local / Networked Multiplayer
- Inspiration / Reference Games: Pong, Clash Royale, Hero-based card games
- Tone / Art Direction: Fantasy / Magical visual effects, arcade competitive
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: Universal Render Pipeline (URP)

# Game Mechanics 
## Core Gameplay Loop
Players move paddles horizontally to bounce a ball while using mana to cast cards that summon units, cast spells, and trigger abilities to defeat the opponent.
## Controls and Input Methods
Paddle movement via mouse/keyboard/gamepad; drag-and-drop card UI for summoning.

# UI
Not directly applicable to this particle lighting task, though the MainMenuScene includes various canvas layouts for login, matchmaking, and deck selection.

# Key Asset & Context
- **Target GameObject**: `GameObject/Particle System` in `MainMenuScene`
- **Light Template Prefab**: `Assets/shaders/particles/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/ParticlesLight.prefab`
  - A pre-configured Point Light optimized for particles (White color, Intensity 1, Range 6, Shadows: None).

# Implementation Steps

### Step 1: Set up Light Template and Module Configuration
- **Description**: Enable the Lights module on the target Particle System and assign the template prefab, tuning settings for visual quality and performance.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No
- **Action details**:
  1. Locate the `Particle System` GameObject under path `GameObject/Particle System` in the open `MainMenuScene`.
  2. Open its `Particle System` component inspector.
  3. Locate the **Lights** module and tick the checkbox to enable it.
  4. Assign the prefab at `Assets/shaders/particles/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/ParticlesLight.prefab` to the **Light** field.
  5. Set the **Ratio** to `0.2` (meaning 20% of particles will spawn lights). This is a safe baseline to avoid rendering overhead.
  6. Enable **Random Distribution** so the lights are scattered uniformly.
  7. Enable **Use Particle Color** so that the spawned lights match the color of the particles.
  8. Enable **Size Affects Range** so larger particles cast wider light.
  9. Enable **Alpha Affects Intensity** so that lights fade out smoothly as particles die.
  10. Set **Range Multiplier** to `1.0` and **Intensity Multiplier** to `1.0` as starting values.
  11. Set **Maximum Lights** to `10` (acts as a performance safety cap).

# Verification & Testing
- **Visual Check**: Open the Scene View, make sure "Scene Lighting" is toggled ON, and play/preview the Particle System. Verify that a fraction of particles emit small, soft glow lights matching their particle color.
- **Performance Check**: Play the game in the Unity Editor and inspect the game view. Ensure there are no performance spikes or warning/error logs in the Console related to light limits or render pipeline errors.
