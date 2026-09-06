# Boulder Guardian - Complete Implementation Analysis

## 1. File Structure and Locations

### Complete File Paths Found:
```
c:\Users\ahmad\My project\Assets\MainAssets\CastingLegends\Boulder\
├── BoulderGuardianController.cs          [Main Script]
├── BoulderPrefab.prefab                  [Prefab]
├── Boulder.asset                         [ScriptableObject Data]
├── BoulderSkin.mat                       [Material]
├── Forest_Guardian_0415201703_texture.fbx [3D Model]
├── Forest_Guardian_0415201703_texture.png [Texture]
├── Animation/                            [Animation Directory]
│   ├── Forest_Guardian_0415201703_texture@Standing 1H Magic Attack 01.fbx
│   ├── Forest_Guardian_0415201703_texture@Standing 2H Magic Attack 01.fbx
│   ├── Forest_Guardian_0415201703_texture@Standing Idle.fbx
│   ├── Frank_RPG_Mage_Attack01_Demo.anim
│   ├── Standing 1H Magic Attack 01.anim
│   ├── Standing 2H Magic Attack 01.anim
│   ├── Standing Idle.anim
│   └── Take 001.anim
└── projectile/                           [Projectile Directory]
    └── stone 2.prefab                    [Boulder Projectile Prefab]
```

---

## 2. Class Hierarchy and Inheritance Structure

### Inheritance Chain:
```
NetworkBehaviour (Unity Netcode)
    ↓
GuardiansController (Base Guardian Implementation)
    ↓
BoulderGuardianController (Boulder-Specific Guardian)
```

### Key Distinction from BaseUnitController:
- **BaseUnitController**: Abstract base for placeable units (soldiers, towers, etc.)
  - Implements: `IPlaceable`, `IHasHP`
  - Movement-based unit
  - HP bar UI support
  - Spawn animation support
  
- **GuardiansController**: Guardian base class (independent from BaseUnitController)
  - Inherits from: `NetworkBehaviour`
  - Stationary ranged attacker
  - Targets BaseUnitController units
  - Animation-driven projectile firing
  
- **BoulderGuardianController**: Boulder-specific Guardian
  - Inherits from: `GuardiansController`
  - Minimal custom implementation
  - Configuration override pattern

---

## 3. Complete Source Code

### BoulderGuardianController.cs (Main Script)
```csharp
using UnityEngine;

public class BoulderGuardianController : GuardiansController
{
    public override void OnNetworkSpawn()
    {
        // Set Boulder-specific defaults BEFORE calling base
        attackRange = 50f;
        projectileDamage = 50;
        base.OnNetworkSpawn();
        // Any other Boulder-specific initialization
    }
}
```

**File Path**: `c:\Users\ahmad\My project\Assets\MainAssets\CastingLegends\Boulder\BoulderGuardianController.cs`

**File Size**: ~15 lines of code

---

## 4. Special Methods and Behaviors Unique to Boulder Guardian

### Override Pattern:
Boulder Guardian uses a **configuration override pattern** rather than custom logic:

1. **OnNetworkSpawn() Override**
   - Sets Boulder-specific attack parameters BEFORE calling base initialization
   - `attackRange = 50f` - Boulder has extended range compared to default (10f)
   - `projectileDamage = 50` - Boulder has significant damage (default: 10)
   - Calls `base.OnNetworkSpawn()` to execute parent initialization

### Inherited from GuardiansController:
- **FindTarget()**: Searches for BaseUnitController units within range, selects closest enemy
- **HandleAttackAttempt()**: Checks if guardian can attack, prevents overkill damage
- **ActualFireProjectile()**: Spawns projectile with ProjectileController component
- **PlayAttackAnimationClientRpc()**: Synchronizes attack animation across clients
- **AnimationEvent_FireProjectile()**: Animation event callback for projectile firing
- **OnDrawGizmosSelected()**: Visualizes attack range as red sphere in editor

### Specialized Behaviors:
- **Overkill Prevention Logic**: Guardian avoids targeting if next projectile would kill (unusual for guardians)
- **Animation-Driven Firing**: Projectile fires via Animation Event, not instant
- **Network Synchronization**: Client-RPC for animation synchronization
- **Closest Target Selection**: Uses LINQ to find nearest enemy within range

---

## 5. Configuration and Prefab References

### Boulder Guardian Settings:
| Parameter | Value | Purpose |
|-----------|-------|---------|
| `attackRange` | 50f | Detection and firing range in units |
| `projectileDamage` | 50 | Damage per projectile hit |
| `attackTriggerName` | "Attack" (inherited) | Animator parameter for attack animation |
| `fireRate` | 1f (inherited) | Attacks per second (default) |
| `isFlyingUnit` | false (inherited) | Boulder Guardian is ground-based |

### Prefab Components:
**BoulderPrefab.prefab** contains:
- Mesh (Forest_Guardian_0415201703_texture.fbx)
- Material (BoulderSkin.mat)
- Animator (with LegendAnimator.controller)
- BoulderGuardianController script component
- Projectile spawn point reference
- Animation state machine with Attack trigger

### Projectile System:
- **Projectile Prefab**: `stone 2.prefab` (in projectile/ directory)
- **Projectile Controller**: Uses universal `ProjectileController` component
- **Projectile Damage**: Configured as `projectileDamage = 50`
- **Projectile Type**: Stone/Boulder themed projectile

---

## 6. Key Differences from Regular BaseUnitController Units

### Guardian vs Unit Comparison:

| Aspect | GuardiansController | BaseUnitController |
|--------|---------------------|-------------------|
| **Movement** | Stationary (no movement) | Mobile (moves on field) |
| **Base Class** | NetworkBehaviour | NetworkBehaviour + IPlaceable, IHasHP |
| **Attack Style** | Ranged projectile only | Mixed (can be melee/ranged) |
| **Target Selection** | Closest BaseUnitController | Strategic/tactical |
| **Attack Priority** | Avoids overkill | Direct targeting |
| **Animation Trigger** | SetTrigger (attack) | State machine based |
| **HP Bar** | Not visible (inherited optional) | Mandatory UI bar |
| **Spawn Animation** | Not supported | Supported with particles |
| **Role** | Defensive/offensive turret | Dynamic field unit |
| **Placement** | Strategic board position | Dynamic field placement |

### Code Structure Differences:

**GuardiansController Hierarchy**:
- Inherits directly from `NetworkBehaviour`
- No `IPlaceable` or `IHasHP` interfaces
- No movement system
- Firing point-based attack

**BaseUnitController Hierarchy**:
- Implements `IPlaceable`, `IHasHP` interfaces
- Movement direction and speed system
- Target search radius for dynamic encounters
- Projectile spawn point for unit attacks

---

## 7. Guardian Controller Base Implementation Details

### GuardiansController.cs (Parent Class)
**File Path**: `c:\Users\ahmad\My project\Assets\MainAssets\CastingLegends\GuardiansController.cs`

#### Key Fields:
```csharp
[Header("Guardian Settings")]
public float attackRange = 10f;              // Default range
public float fireRate = 1f;                  // Attacks per second
protected float nextFireTime = 0f;           // Cooldown tracking

[Header("Projectile")]
public GameObject projectilePrefab;          // Projectile to spawn
public Transform firePoint;                  // Spawn location
public int projectileDamage = 10;            // Projectile damage

[Header("Animation")]
public string attackTriggerName = "Attack";  // Animator parameter

protected Transform currentTarget;           // Current locked target
protected Animator animator;                 // Animation controller
```

#### Core Methods:

1. **OnNetworkSpawn()**
   - Gets Animator component
   - Disables on non-server clients (server-only logic)
   - Sets firePoint to transform if not assigned

2. **Update() - Protected Virtual**
   - Finds targets each frame
   - Attempts attacks on cooldown
   - Server-only execution

3. **FindTarget() - Protected Virtual**
   - Finds all BaseUnitController units
   - Filters by ownership and range
   - Uses LINQ OrderBy for closest selection

4. **HandleAttackAttempt() - Protected Virtual**
   - Checks attack cooldown
   - Prevents overkill targeting
   - Triggers animator and animation
   - Updates nextFireTime

5. **ActualFireProjectile() - Protected Virtual**
   - Instantiates projectile at firePoint
   - Spawns on network (all clients)
   - Initializes ProjectileController with target/damage/owner
   - Error handling for misconfigured projectiles

6. **AnimationEvent_FireProjectile()**
   - Called by Animation Event in animator
   - Server-only execution
   - Calls ActualFireProjectile()

7. **PlayAttackAnimationClientRpc()**
   - Syncs animation across clients
   - Non-server clients execute animator trigger
   - Server skips (already triggered)

#### Virtual Methods for Override:
- `OnNetworkSpawn()` - Boulder Guardian overrides this
- `Update()` - Available for custom update logic
- `FindTarget()` - Available for custom targeting
- `HandleAttackAttempt()` - Available for custom attack behavior
- `ActualFireProjectile()` - Available for custom projectile spawning
- `OnDrawGizmosSelected()` - Editor visualization

---

## 8. Network Architecture

### Network Synchronization:
- **Server Authority**: GuardiansController only executes attack logic on server
  - Non-server clients disable the script (`enabled = false`)
  - Firing logic isolated to server
  
- **Animation Synchronization**: Via `PlayAttackAnimationClientRpc()`
  - Server plays animation locally
  - Clients receive RPC to play animation
  - Prevents animation desync

- **Projectile Spawning**: NetworkObject-based
  - Projectile spawned on server
  - `projectileNO.Spawn(true)` - Spawns for all clients
  - `ChangeOwnership(OwnerClientId)` - Sets projectile owner

---

## 9. Animation System

### Boulder Animation Files:
```
Standing Idle.anim                          [Idle state]
Standing 1H Magic Attack 01.anim            [Attack animation #1]
Standing 2H Magic Attack 01.anim            [Attack animation #2]
Frank_RPG_Mage_Attack01_Demo.anim           [Alternative attack]
Take 001.anim                               [Additional animation]
```

### Animation Integration:
- **Controller**: `LegendAnimator.controller`
- **Trigger Parameter**: `Attack` (configurable via `attackTriggerName`)
- **Animation Events**: Contains `AnimationEvent_FireProjectile()` event
- **Animation Timing**: Projectile fires at specific frame during attack animation

### Boulder-Specific Animation Setup:
- Uses "Standing" idle animations (stationary guardian)
- 1H and 2H magic attack variants suggest elemental/magical rock attacks
- Animation controller must have "Attack" trigger parameter
- Animation event "AnimationEvent_FireProjectile" must be placed at projectile frame

---

## 10. How It Differs from BraveKnight and Other Units

### Comparison with BraveKnight (BraveKnightController):
```
BraveKnightController Inherits: BaseUnitController
  - Mobile unit that moves on field
  - Melee/Physical attack pattern
  - HP tracking and death system
  - Spawn animation support
  - Placed dynamically on board

BoulderGuardianController Inherits: GuardiansController
  - Stationary defensive structure
  - Ranged attack only
  - No movement system
  - Fixed board position
  - Automatic target acquisition
```

### Guardian Role:
- **Purpose**: Board defense/offense turret
- **Placement**: Strategic board position (assigned at spawn)
- **Behavior**: Automatic target and fire
- **Resources**: Passive (always active when spawned)
- **Removal**: Destroyed when target unit dies or removed

---

## 11. Complete File Inventory

### C# Scripts:
- `BoulderGuardianController.cs` - 15 lines - Main implementation

### 3D Assets:
- `Forest_Guardian_0415201703_texture.fbx` - Base model
- `Forest_Guardian_0415201703_texture.png` - Texture

### Prefabs:
- `BoulderPrefab.prefab` - Main guardian prefab
- `projectile/stone 2.prefab` - Boulder projectile

### Materials:
- `BoulderSkin.mat` - Boulder texture material

### Animations (8 total):
- Standing animations (idle, walk, etc.)
- Attack animations (1H and 2H variants)
- Alternative attack animations

### Data:
- `Boulder.asset` - ScriptableObject configuration

### Metadata:
- Multiple `.meta` files for Unity asset tracking

---

## 12. Summary

**Boulder Guardian** is a **minimal-implementation Guardian** that leverages the robust `GuardiansController` base class. Its implementation follows a **configuration-override pattern** where:

1. It defines Boulder-specific stats (50f range, 50 damage)
2. Relies entirely on parent class for all attack/targeting logic
3. Doesn't override any attack behavior, only configuration
4. Inherits complete animation-driven projectile system
5. Uses network synchronization from GuardiansController
6. Demonstrates clean inheritance design by minimal code duplication

**Key Features**:
- Extended attack range (50f vs default 10f)
- High projectile damage (50 vs default 10)
- Stone/boulder-themed visuals and projectile
- Stationary turret-style gameplay
- Automatic enemy detection and targeting
- Animation-synchronized firing
- Full network multiplayer support

**Unique Characteristics Compared to BaseUnitController**:
- No movement system (stationary)
- Automatic server-side targeting logic
- Animation event-driven projectile spawning
- Guardian-specific overkill-prevention logic
- Client-side animation synchronization via RPC
