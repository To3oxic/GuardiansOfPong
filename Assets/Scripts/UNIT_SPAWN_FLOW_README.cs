/**
 * UNIT SPAWN ANIMATION FLOW
 * =========================
 * 
 * This system handles the complete card placement and unit spawn sequence:
 * 
 * STEP 1: CLIENT DRAG ENDS (CardDragHandler.OnEndDrag)
 * - Player finishes dragging a card to the board
 * - Creates a transparent preview of the unit (y=2, alpha=0.3)
 * - Preview shows for 0.5 seconds then destroys
 * - Simultaneously sends ServerRpc to apply card effect
 * 
 * STEP 2: SERVER RECEIVES CALL (GameManager.ApplyCardEffectServerRpc)
 * - Validates placement and mana cost on server
 * - Calls CardEffect.ApplyEffect() to spawn the actual networked unit
 * - Unit spawns at the specified position (y=0 by default)
 * - Server then sends ClientRpc to all clients
 * 
 * STEP 3: ALL CLIENTS RECEIVE ANIMATION SIGNAL (GameManager.PlayCardEffectClientRpc)
 * - CardEffect.ApplyVisualEffect() is called to spawn particle effects
 * - GameManager.AnimateSpawnedUnitsAtPosition() finds units at that location
 * - Triggers UnitSpawnAnimator.PlaySpawnAnimation() on the spawned unit
 * 
 * STEP 4: UNIT SPAWN ANIMATION (UnitSpawnAnimator.PlaySpawnAnimation)
 * - Unit is positioned at y=2 (raised high)
 * - Particle effect spawns at unit's position with 0.5s duration
 * - Unit drops smoothly from y=2 to y=0 over 0.5 seconds (eased acceleration)
 * - Unit becomes fully opaque and interactive when animation completes
 * 
 * REQUIRED SETUP
 * ==============
 * 1. Add UnitSpawnAnimator component to all unit prefabs
 * 2. Assign ParticleSystem prefab to each CardEffect's cardParticleEffectPrefab field
 * 3. Units should have CanvasGroup component (added automatically if missing)
 * 
 * CLASSES INVOLVED
 * ================
 * - CardDragHandler: Creates preview and sends placement ServerRpc
 * - GameManager: Coordinates server spawning and client animation
 * - CardEffect: Provides particle effect prefab and base animation logic
 * - UnitSpawnAnimator: Handles drop animation and particle spawning
 * - UnitSpawnPreview: (Optional utility class for previews)
 * 
 * TIMING SUMMARY
 * ==============
 * - Client preview duration: 0.5 seconds
 * - Unit drop duration: 0.5 seconds
 * - Particle effect duration: 0.5 seconds
 * Total from card placement to unit ready: ~1 second
 */
