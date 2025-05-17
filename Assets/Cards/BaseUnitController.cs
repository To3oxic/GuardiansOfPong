using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

// Removed the Units namespace as the file is moved to Assets/Cards
public abstract class BaseUnitController : NetworkBehaviour, IPlaceable
{
    [Header("Unit Type Settings")]
    public bool isFlyingUnit = false;
    public bool canAttackFlyingUnits = false;
    public bool isRangedUnit = false;
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    // Protected fields that will be set by child classes
    [System.NonSerialized] protected float moveSpeed;
    [System.NonSerialized] protected float attackRange;
    [System.NonSerialized] protected float attackCooldown;
    [System.NonSerialized] protected float targetSearchRadius;
    [System.NonSerialized] protected int attackDamage;
    [System.NonSerialized] protected int maxHP;

    // Flag to indicate this unit is in the process of dying
    [System.NonSerialized] protected bool isDying = false;

    // Flag to indicate this unit is actively avoiding a collision
    [System.NonSerialized] protected bool isAvoidingCollision = false;
    [System.NonSerialized] protected float collisionAvoidanceTimer = 0f;

    protected Rigidbody rb;
    protected Animator animator;
    protected NetworkAnimator networkAnimator;
    protected Transform currentTarget;
    protected float cooldownTimer = 0f;

    // NetworkVariables for synchronization
    protected NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    protected NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    protected NetworkVariable<bool> networkIsFlying = new NetworkVariable<bool>();

    // NetworkVariable for HP
    protected NetworkVariable<int> currentHP = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    protected Collider unitCollider;
    protected Vector3 lastValidPosition;
    protected const float COLLISION_RESOLUTION_FORCE = 5f; // Increased from 5f to 15f
    protected const float COLLISION_EMERGENCY_FORCE = 10f; // New constant for severe overlaps

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        unitCollider = GetComponent<Collider>();
        lastValidPosition = transform.position;

        if (IsServer)
        {
            currentHP.Value = maxHP;
        }
    }

    protected virtual void Update()
    {
        if (!IsServer)
        {
            // Smoothly interpolate position and rotation for clients
            transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation.Value, Time.deltaTime * 10f);
            return;
        }

        // Update cooldown timer
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // Update collision avoidance timer
        if (isAvoidingCollision)
        {
            collisionAvoidanceTimer -= Time.deltaTime;
            if (collisionAvoidanceTimer <= 0)
            {
                isAvoidingCollision = false;
            }
        }

        // Only check for new targets if we're not actively avoiding a collision
        if (!isAvoidingCollision)
        {
            FindNewTarget();
        }

        // Handle movement and targeting
        if (currentTarget != null)
        {
            HandleTargeting();
        }
        else
        {
            // No target, keep moving forward
            Vector3 forwardMovementDirection = transform.forward;
            if (!isFlyingUnit)
            {
                forwardMovementDirection.y = 0;
            }
            rb.linearVelocity = forwardMovementDirection.normalized * moveSpeed;
            if (animator != null)
            {
                animator.SetBool("IsWalking", true);
            }
        }

        // Check for overlapping colliders and resolve them
        ResolveColliderOverlaps();
    }

    protected virtual void HandleTargeting()
    {
        if (currentTarget == null) return;

        Vector3 directionToTarget = (currentTarget.position - transform.position);
        if (!isFlyingUnit)
        {
            directionToTarget.y = 0;
        }
        float distance = directionToTarget.magnitude;

        if (distance <= attackRange)
        {
            // Ensure velocity is zero when in attack range
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }

            // Always face the target when attacking
            if (directionToTarget != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(directionToTarget.normalized);
            }

            // Set animation state
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }

            // Attack if cooldown is ready
            if (cooldownTimer <= 0f)
            {
                AttackTarget();
                cooldownTimer = attackCooldown;
            }
        }
        else
        {
            // Move towards target
            if (rb != null)
            {
                rb.linearVelocity = directionToTarget.normalized * moveSpeed;
            }

            // Set animation state
            if (animator != null)
            {
                animator.SetBool("IsWalking", true);
            }

            // Rotate to face movement direction
            if (directionToTarget.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(directionToTarget.normalized);
            }
        }
    }

    protected virtual void ResolveColliderOverlaps()
    {
        if (unitCollider == null) return;

        // Get all colliders that overlap with this unit
        Collider[] overlappingColliders = Physics.OverlapBox(
            unitCollider.bounds.center,
            unitCollider.bounds.extents,
            transform.rotation
        );

        foreach (var otherCollider in overlappingColliders)
        {
            // Skip our own collider
            if (otherCollider == unitCollider) continue;
            
            // Get the bounds of both colliders
            Bounds ourBounds = unitCollider.bounds;
            Bounds theirBounds = otherCollider.bounds;

            // Only handle actual collisions (no prediction)
            if (!ourBounds.Intersects(theirBounds)) continue;
            
            // Log collision for debugging
            Debug.Log($"[BaseUnitController] Collision detected with {otherCollider.name}");
            
            // Get centers
            Vector3 ourCenter = ourBounds.center;
            Vector3 theirCenter = theirBounds.center;
            Vector3 directionBetweenCenters = (ourCenter - theirCenter).normalized;
            
            // Default is we should adjust
            bool shouldAdjust = true;
            bool shouldOtherAdjust = false;
            
            // Special handling for paddle collisions
            if (otherCollider.TryGetComponent<PaddleController>(out _))
            {
                // For paddles, always move away along Z axis
                float paddleZ = theirCenter.z;
                float ourZ = ourCenter.z;
                
                // Calculate which side of paddle we're on
                float zDirection = (ourZ > paddleZ) ? 1 : -1;
                
                // Move away from paddle
                Vector3 paddleSeparationVector = new Vector3(0, 0, zDirection * 0.1f);
                transform.position += paddleSeparationVector;
                if (networkPosition != null) networkPosition.Value = transform.position;
                if (lastValidPosition != null) lastValidPosition = transform.position;
                
                // Stop movement through paddle
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, 0);
                }
                
                continue; // Skip to next collider after handling paddle
            }
            
            // If the other object is a unit, determine who should move
            if (otherCollider.TryGetComponent<BaseUnitController>(out var otherUnit))
            {
                // Skip if it's the same unit
                if (otherUnit == this) continue;
                
                // Calculate the volume of both colliders
                float ourVolume = ourBounds.size.x * ourBounds.size.y * ourBounds.size.z;
                float theirVolume = theirBounds.size.x * theirBounds.size.y * theirBounds.size.z;
                
                // Determine who should adjust based on size
                if (ourVolume > theirVolume)
                {
                    // We're bigger, don't adjust unless they can't
                    shouldAdjust = false;
                }
                else if (Mathf.Approximately(ourVolume, theirVolume))
                {
                    // Same size, both adjust
                    shouldAdjust = true;
                    shouldOtherAdjust = true;
                }
                else
                {
                    // We're smaller, we adjust
                    shouldAdjust = true;
                }
            }
            
            // Adjust our position if needed
            if (shouldAdjust)
            {
                // Determine direction to move (prioritize X axis)
                Vector3 moveDirection;
                
                // Check if X or Z direction is better
                float xDiff = ourCenter.x - theirCenter.x;
                float zDiff = ourCenter.z - theirCenter.z;
                
                // Prioritize X-axis movement
                if (Mathf.Abs(xDiff) > 0.01f)
                {
                    // Move along X axis
                    moveDirection = new Vector3(Mathf.Sign(xDiff), 0, 0);
                }
                else
                {
                    // Move along Z axis
                    moveDirection = new Vector3(0, 0, Mathf.Sign(zDiff));
                }
                
                // Move a tiny amount
                Vector3 moveVector = moveDirection * 0.01f;
                transform.position += moveVector;
                if (networkPosition != null) networkPosition.Value = transform.position;
                lastValidPosition = transform.position;
            }
            
            // If both should adjust, move the other unit too
            if (shouldOtherAdjust && otherCollider.TryGetComponent<BaseUnitController>(out var otherUnitToMove))
            {
                // Calculate direction for other unit (opposite of our direction)
                Vector3 moveDirection;
                
                // Check if X or Z direction is better
                float xDiff = theirCenter.x - ourCenter.x;
                float zDiff = theirCenter.z - ourCenter.z;
                
                // Prioritize X-axis movement
                if (Mathf.Abs(xDiff) > 0.01f)
                {
                    // Move along X axis
                    moveDirection = new Vector3(Mathf.Sign(xDiff), 0, 0);
                }
                else
                {
                    // Move along Z axis
                    moveDirection = new Vector3(0, 0, Mathf.Sign(zDiff));
                }
                
                // Move a tiny amount
                Vector3 moveVector = moveDirection * 0.01f;
                otherUnitToMove.transform.position += moveVector;
                if (otherUnitToMove.networkPosition != null) otherUnitToMove.networkPosition.Value = otherUnitToMove.transform.position;
                otherUnitToMove.lastValidPosition = otherUnitToMove.transform.position;
            }
        }
    }

    public virtual void SetFlying(bool flying)
    {
        if (!IsServer) return;
        networkIsFlying.Value = flying;
        isFlyingUnit = flying;

        // Adjust collider and rigidbody for flying
        if (TryGetComponent<Collider>(out var collider))
        {
            collider.isTrigger = true;
        }
        if (rb != null)
        {
            rb.useGravity = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();

        // Disable gravity and set collider to trigger
        if (rb != null)
        {
            rb.useGravity = false;
        }
        if (TryGetComponent<CapsuleCollider>(out var collider))
        {
            collider.isTrigger = true;
        }

        if (IsServer)
        {
            currentHP.Value = maxHP;
            FindNewTarget();

            // Start moving in the forward direction
            Vector3 forwardMovementDirection = transform.forward;
            if (!isFlyingUnit)
            {
                forwardMovementDirection.y = 0;
            }
            if (forwardMovementDirection.sqrMagnitude < 0.01f)
            {
                forwardMovementDirection = Vector3.forward;
            }
            rb.linearVelocity = forwardMovementDirection.normalized * moveSpeed;

            if (animator != null)
            {
                animator.SetBool("IsWalking", true);
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer) return;
        if (currentHP.Value <= 0) return;

        // Note: Targeting logic is now in Update, FixedUpdate might not be needed for targeting
        // If physics-based movement is complex, some parts might return here.

        // Update network variables (always needed for clients)
        networkPosition.Value = transform.position;
        networkRotation.Value = transform.rotation;
    }

    protected virtual void FindNewTarget()
    {
        // First, look for other units/placeables
        Collider[] colliders = Physics.OverlapSphere(transform.position, targetSearchRadius);
        float closestDistance = float.MaxValue;
        Transform closestTarget = null;

        foreach (var collider in colliders)
        {
            // Skip if it's our own collider
            if (collider.gameObject == gameObject) continue;

            // Check if it's a placeable unit (not a paddle)
            if (collider.TryGetComponent<IPlaceable>(out var placeable))
            {
                // Skip if it's our own unit (same owner)
                if (placeable is NetworkBehaviour netPlaceable && netPlaceable.OwnerClientId == OwnerClientId)
                    continue;

                // Skip if the unit is dead or dying
                if (placeable is BaseUnitController unit && (unit.currentHP.Value <= 0 || unit.isDying))
                    continue;

                // Check if we can target flying units
                if (placeable is BaseUnitController targetUnit && targetUnit.isFlyingUnit && !canAttackFlyingUnits)
                    continue;

                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = collider.transform;
                }
            }
        }

        // If no units found, look for opponent's paddle
        if (closestTarget == null)
        {
            PaddleController[] paddles = FindObjectsOfType<PaddleController>();
            foreach (var paddle in paddles)
            {
                Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Checking paddle: {paddle.name}, Unit Owner: {OwnerClientId}, Paddle Owner: {paddle.OwnerClientId}");

                // Only target opponent's paddle
                if (paddle.OwnerClientId != OwnerClientId)
                {
                    float distance = Vector3.Distance(transform.position, paddle.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = paddle.transform;
                        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Found valid opponent paddle target: {paddle.name}, Unit Owner: {OwnerClientId}, Paddle Owner: {paddle.OwnerClientId}");
                    }
                }
                else
                {
                    Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Skipping own paddle: {paddle.name}, Unit Owner: {OwnerClientId}, Paddle Owner: {paddle.OwnerClientId}");
                }
            }
        }

        // Update target if we found a new one that's closer or if current target is invalid
        if (closestTarget != null)
        {
            bool shouldUpdateTarget = currentTarget == null ||
                !currentTarget.gameObject.activeInHierarchy ||
                Vector3.Distance(transform.position, closestTarget.position) <
                Vector3.Distance(transform.position, currentTarget.position);

            if (shouldUpdateTarget)
            {
                currentTarget = closestTarget;
                if (currentTarget.TryGetComponent<PaddleController>(out var paddle))
                {
                    Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) New paddle target acquired: {currentTarget.name}, Unit Owner: {OwnerClientId}, Paddle Owner: {paddle.OwnerClientId}");
                }
                else
                {
                    Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) New target acquired: {currentTarget.name}");
                }
            }
        }
        else
        {
            currentTarget = null;
        }
    }

    protected virtual void AttackTarget()
    {
        if (currentTarget == null || !IsServer || currentHP.Value <= 0) return;

        // Check if target is a unit and if it's dying
        if (currentTarget.TryGetComponent<BaseUnitController>(out var targetUnit))
        {
            if (targetUnit.isDying || targetUnit.currentHP.Value <= 0)
            {
                Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Target {currentTarget.name} is already dying or dead, finding new target.");
                // Target is already dying, find a new one
                FindNewTarget();
                return; // Skip this attack cycle
            }
        }

        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Server: Preparing to attack {currentTarget.name}");

        if (isRangedUnit)
        {
            // For ranged units, trigger attack animation.
            // The animation itself should have an event calling FireProjectile().
            if (networkAnimator != null)
            {
                // Ensure your Animator Controller has an "Attack" trigger that leads
                // to the ranged attack animation. This animation needs an event
                // calling the FireProjectile() method.
                networkAnimator.SetTrigger("Attack");
            }
        }
        else // Melee unit
        {
            // For melee units, trigger attack animation.
            // The animation itself should have an event calling DealDamageOnSwipe().
            if (networkAnimator != null)
            {
                networkAnimator.SetTrigger("Attack");
            }
        }
        // Cooldown is typically reset in HandleTargeting after AttackTarget is called.
    }

    // Called by Animation Event for RANGED units at the moment the projectile should be fired
    public virtual void FireProjectile()
    {
        if (!IsServer) return; // Only server should execute this logic

        // Double-check if target is still valid (not dying or dead) before firing
        if (currentTarget == null)
        {
            Debug.LogWarning($"[{GetType().Name}:{NetworkObjectId}] No target to fire at!");
            return;
        }

        // Check if target is a unit and if it's dying
        if (currentTarget.TryGetComponent<BaseUnitController>(out var targetUnit))
        {
            if (targetUnit.isDying || targetUnit.currentHP.Value <= 0)
            {
                Debug.Log($"[{GetType().Name}:{NetworkObjectId}] Target {currentTarget.name} is already dying or dead, canceling projectile.");
                // Target is already dying, don't waste a projectile
                return;
            }
        }

        if (projectilePrefab == null)
        {
            Debug.LogError($"[{GetType().Name}:{NetworkObjectId}] Projectile Prefab is not assigned on {gameObject.name}!");
            return;
        }

        // Determine spawn position and rotation
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (projectileSpawnPoint != null)
        {
            spawnPosition = projectileSpawnPoint.position;
            spawnRotation = projectileSpawnPoint.rotation;
        }
        else
        {
            // Default spawn if no specific point is set
            spawnPosition = transform.position + transform.forward * 1f; // Example: 1 unit in front
            spawnRotation = transform.rotation; // Align with unit's facing direction
            Debug.LogWarning($"[{GetType().Name}:{NetworkObjectId}] Projectile Spawn Point not set for {gameObject.name}, using default spawn position and rotation.");
        }

        GameObject projectileInstance = Instantiate(projectilePrefab, spawnPosition, spawnRotation);
        NetworkObject netObj = projectileInstance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn(true); // Spawn with server ownership initially
            netObj.ChangeOwnership(OwnerClientId); // Change ownership to the client that owns this unit, if intended

            // Attempt to initialize the projectile if it has a controller script
            if (projectileInstance.TryGetComponent<ProjectileController>(out var projectileController))
            {
                // Initialize the projectile.
                // Be cautious: currentTarget might be stale if the animation event is significantly delayed.
                // You might need a more robust way to pass target info if this becomes an issue.
                projectileController.Initialize(currentTarget, attackDamage, OwnerClientId);
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}:{NetworkObjectId}] Projectile {projectileInstance.name} is missing a ProjectileController script for initialization.");
            }
        }
        else
        {
            Debug.LogError($"[{GetType().Name}:{NetworkObjectId}] Projectile Prefab {projectilePrefab.name} is missing a NetworkObject component. Destroying instance.");
            Destroy(projectileInstance);
        }
    }

    // Called by Animation Event from the "Attack" animation clip
    public virtual void DealDamageOnSwipe()
    {
        if (!IsServer || currentTarget == null || currentHP.Value <= 0) return;

        // Check if target is a unit and if it's dying before dealing damage
        if (currentTarget.TryGetComponent<BaseUnitController>(out var targetUnit))
        {
            if (targetUnit.isDying || targetUnit.currentHP.Value <= 0)
            {
                Debug.Log($"[{GetType().Name}:{NetworkObjectId}] Target {currentTarget.name} is already dying or dead, canceling attack.");
                // Target is already dying, don't waste the attack
                return;
            }
        }

        // Try to damage a placeable first
        if (currentTarget.TryGetComponent<IPlaceable>(out var placeable))
        {
            // Skip if it's our own unit/structure
            if (placeable is NetworkBehaviour netPlaceable && netPlaceable.OwnerClientId == OwnerClientId)
                return;

            placeable.TakeDamage(attackDamage);
            Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) SWIPE! Dealt {attackDamage} damage to placeable {currentTarget.name}");
        }
        // If not a placeable, try to damage a paddle
        else if (currentTarget.TryGetComponent<PaddleController>(out var paddle))
        {
            // Only damage opponent's paddle
            if (paddle.OwnerClientId != OwnerClientId)
            {
                paddle.ReduceHP(attackDamage);
                Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) SWIPE! Dealt {attackDamage} damage to opponent's paddle {currentTarget.name}");
            }
        }
    }

    // Helper method to check if damage would be fatal without actually applying it
    public virtual bool WouldDieFromDamage(int amount)
    {
        if (!IsServer || currentHP.Value <= 0 || isDying) return true;
        return (currentHP.Value - amount) <= 0;
    }

    // Get current HP - useful for damage calculations
    public virtual int GetCurrentHP()
    {
        return currentHP.Value;
    }

    // Get max HP - useful for UI and damage calculations
    public virtual int GetMaxHP()
    {
        return maxHP;
    }

    // Implementation of IPlaceable.TakeDamage
    public virtual void TakeDamage(int amount)
    {
        if (!IsServer || currentHP.Value <= 0 || isDying) return;

        int newHP = currentHP.Value - amount;
        currentHP.Value = newHP;
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Took {amount} damage, HP is now {currentHP.Value}");

        if (newHP <= 0)
        {
            // Immediately mark as dying to prevent other units from targeting or attacking
            isDying = true;
            currentHP.Value = 0;

            // Disable collider immediately to prevent further targeting
            if (TryGetComponent<Collider>(out var collider))
            {
                collider.enabled = false;
            }

            // Trigger death animation if available
            if (networkAnimator != null)
            {
                networkAnimator.SetTrigger("Death");
            }

            // Start death sequence
            StartCoroutine(HandleDeath());
        }
    }

    protected virtual System.Collections.IEnumerator HandleDeath()
    {
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Starting death sequence.");
        rb.linearVelocity = Vector3.zero;
        currentTarget = null; // Clear current target when dead

        // Note: Collider is already disabled in TakeDamage when isDying is set to true
        // This ensures units stop targeting this unit immediately when it receives a fatal hit

        yield return new WaitForSeconds(2f);

        if (IsServer)
        {
            NetworkObject.Despawn();
        }
    }
}