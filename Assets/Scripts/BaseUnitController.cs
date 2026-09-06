using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.UI;
using System.Collections;
using Unity.UI.Shaders.Sample;

// Removed the Units namespace as the file is moved to Assets/Cards
public abstract class BaseUnitController : NetworkBehaviour, IPlaceable, IHasHP
{
    [Header("Unit Type Settings")]
    public bool isFlyingUnit = false;
    public bool canAttackFlyingUnits = false;
    public bool isRangedUnit = false;
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float weight = 1f; // For physics interactions

    [Header("HP Bar Settings")]
    public GameObject hpBarCanvasPrefab; // Assign a Canvas prefab with Image component for HP bar
    public Transform hpBarSpawnPoint; // Optional: Assign in inspector to position HP bar above unit

    // Protected fields that will be set by child classes
    [System.NonSerialized] protected float moveSpeed;

    protected float currentSpeed;
    [System.NonSerialized] protected float attackRange;
    [System.NonSerialized] protected float attackCooldown;
    [System.NonSerialized] protected float targetSearchRadius;
    [System.NonSerialized] protected int attackDamage;

    // NetworkVariable for max HP
    protected NetworkVariable<int> maxHP = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );



    // HP bar UI references
    [System.NonSerialized] protected GameObject hpBarCanvasInstance;
    [System.NonSerialized] protected Unity.UI.Shaders.Sample.CustomSlider hpBarSlider; // The HP bar slider

    // Flag to indicate this unit is in the process of dying
    [System.NonSerialized] protected bool isDying = false;

    // Flag to indicate this unit is still in spawn animation phase
    [System.NonSerialized] public bool isSpawningAnimation = true;

    // Particle effect to spawn during animation (set in Inspector on prefab)
    public ParticleSystem spawnParticleEffectPrefab;

    // Canvas overlay prefab for spawn animation (set in Inspector on prefab)
    public GameObject spawnCanvasPrefab;


    // Movement direction (like BallController's launchDirection)
    [System.NonSerialized] protected Vector3 movementDirection = Vector3.forward;

    // Unit collider (trigger-based like ball)
    protected Collider unitCollider;
    protected Animator animator;
    protected NetworkAnimator networkAnimator;
    protected Transform currentTarget;
    protected float cooldownTimer = 0f;

    protected bool Dead = false;
    protected bool isWalking = false;
    // NetworkVariables for synchronization
    protected NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    protected NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    protected NetworkVariable<bool> networkIsFlying = new NetworkVariable<bool>();

    // NetworkVariable for HP
    protected NetworkVariable<int> currentHP = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    protected Vector3 lastValidPosition;
    private const float UNIT_COLLISION_SEPARATION = 0.2f; // Push away from other units to prevent clumping

    protected virtual void Start()
    {

        unitCollider = GetComponent<Collider>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        lastValidPosition = transform.position;

        // HP bar canvas will be created on first damage
        // Don't pre-create it here

        // Setup trigger collider (like BallController)
        if (unitCollider != null)
        {
            unitCollider.isTrigger = true;
        }

        if (IsServer)
        {
            currentHP.Value = maxHP.Value;
        }
    }

    protected virtual void Update()
    {
        // Skip HP bar updates if unit is dead or dying
        if (!isDying && !Dead && hpBarCanvasInstance != null)
        {
            Camera mainCamera = Camera.main;
            float cameraZ = mainCamera != null ? mainCamera.transform.eulerAngles.z : 0;
            hpBarCanvasInstance.transform.localRotation = Quaternion.Euler(90f, 0, cameraZ);
            UpdateHPBar();
        }

        // Client-side: Skip gameplay logic
        if (!IsServer)
        {
            return;
        }

        // Server-side gameplay logic
        // Skip all gameplay logic if currently spawning
        if (isSpawningAnimation)
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
            return;
        }

        // Update cooldown timer
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }
        // Find targets and handle movement/targeting
        FindNewTarget();

        if (currentTarget != null && !Dead)
        {
            // If target is in range, stop moving
            Vector3 directionToTarget = (currentTarget.position - transform.position);
            if (!isFlyingUnit)
            {
                directionToTarget.y = 0;
            }
            float distanceToTarget = directionToTarget.magnitude;

            if (distanceToTarget <= attackRange)
            {
                currentSpeed = 0f;
            }

            HandleTargeting();
        }
        else
        {
            if (Dead)
            {
                moveSpeed = 0f;
            }
            // No target, keep moving forward with position-based movement
            MoveUnitManually();
        }
    }

    /// <summary>
    /// Check if unit is currently in attack animation
    /// </summary>
    protected bool IsInAttackAnimation()
    {
        if (animator == null)
            return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("Attack") || stateInfo.IsName("Attack.Ranged") || stateInfo.IsName("Attack.Melee");
    }

    /// <summary>
    /// Manual position-based movement like BallController
    /// </summary>
    private void MoveUnitManually()
    {
        // Check if in attack animation - if so, stop moving
        if (IsInAttackAnimation())
        {
            currentSpeed = 0f;
        }
        else
        {
            currentSpeed = moveSpeed;
        }

        Vector3 movement = movementDirection * currentSpeed * Time.deltaTime;
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Moving unit by {movementDirection} {moveSpeed} from position {transform.position} hahaha");
        Vector3 newPosition = transform.position + movement;

        // Apply movement
        transform.position = newPosition;
        lastValidPosition = newPosition;

        if (!isWalking)
        {
            if (animator != null && currentSpeed > 0.1f)
            {
                isWalking = true;
                animator.SetBool("IsWalking", true);
            }
            else if (animator != null)
            {
                isWalking = false;
                animator.SetBool("IsWalking", false);
            }
        }
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
            // Stop movement when in attack range
            currentSpeed = 0f;

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
            // Move towards target with position-based movement
            movementDirection = directionToTarget.normalized;
            MoveUnitManually();

            // Rotate to face movement direction
            if (directionToTarget.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(directionToTarget.normalized);
            }
        }
    }

    /// <summary>
    /// Trigger-based collision detection - called when another collider enters this unit's trigger
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Collision with ball - move in ball's direction
        if (other.CompareTag("Ball"))
        {
            BallController ball = other.GetComponent<BallController>();
            if (ball != null)
            {
                // Move in the direction the ball is traveling
                // Try to access ball's launch direction if available
                movementDirection = Vector3.forward; // Default, ball direction handling can be improved
                Debug.Log($"[{GetType().Name}] Hit by ball, moving with ball direction");
            }
        }

        // Collision with another unit - push smaller unit away
        if (other.TryGetComponent<BaseUnitController>(out var otherUnit))
        {
            if (otherUnit == this) return;

            // Compare sizes using local scale magnitude
            float otherweight = otherUnit.weight;

            // Only push this unit if it's smaller than the other unit
            if (weight <= otherweight)
            {
                // Push this unit away from the larger unit
                Vector3 separationDirection = (transform.position - other.transform.position).normalized;
                transform.position += separationDirection * UNIT_COLLISION_SEPARATION;
                lastValidPosition = transform.position;

                Debug.Log($"[{GetType().Name}] Smaller unit pushed away from larger {otherUnit.GetType().Name} (weight: {weight:F2} < {otherweight:F2})");
            }
            else
            {
                Debug.Log($"[{GetType().Name}] Larger or equal unit, not moving (weight: {weight:F2} >= {otherweight:F2})");
            }
        }
    }

    /// <summary>
    /// Optional: Handle paddle collisions
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;

        // Push away from paddle if stuck
        if (other.TryGetComponent<PaddleController>(out _))
        {
            Vector3 paddleCenter = other.transform.position;
            Vector3 unitCenter = transform.position;

            float paddleZ = paddleCenter.z;
            float unitZ = unitCenter.z;

            // Move away from paddle along Z axis
            if (Mathf.Abs(unitZ - paddleZ) < 2f)
            {
                float zDirection = (unitZ > paddleZ) ? 1 : -1;
                transform.position += new Vector3(0, 0, zDirection * UNIT_COLLISION_SEPARATION * 0.5f);
                lastValidPosition = transform.position;
            }
        }
    }

    public virtual void SetFlying(bool flying)
    {
        if (!IsServer) return;
        networkIsFlying.Value = flying;
        isFlyingUnit = flying;

        // Adjust collider for flying (keep as trigger)
        if (TryGetComponent<Collider>(out var collider))
        {
            collider.isTrigger = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        unitCollider = GetComponent<Collider>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();

        // Ensure collider is trigger
        if (unitCollider != null)
        {
            unitCollider.isTrigger = true;
        }
        // Create HP bar canvas on spawn (disabled initially)
        if (hpBarCanvasPrefab != null)
        {
            // Instantiate without parent to avoid inheriting unit's scale
            hpBarCanvasInstance = Instantiate(hpBarCanvasPrefab);
            float unitHeight = GetUnitHeight();
            hpBarCanvasInstance.transform.position = transform.position + Vector3.up * (unitHeight + 0.5f);


            hpBarCanvasInstance.transform.localScale = new Vector3(2f, 2f, 1f); // Set size to 2x

            // Align canvas rotation to camera's Z rotation (like spawn animation)
            Camera mainCamera = Camera.main;
            float cameraZ = mainCamera != null ? mainCamera.transform.eulerAngles.z : 0;
            hpBarCanvasInstance.transform.rotation = Quaternion.Euler(90f, 0, cameraZ);



            // Get the Slider component for the HP bar
            hpBarSlider = hpBarCanvasInstance.GetComponentInChildren<CustomSlider>();
            if (hpBarSlider == null)
            {
                Debug.LogError($"[{GetType().Name}] HP bar canvas prefab doesn't have a Slider component!");
            }
            else
            {
                // Keep slider disabled initially
                hpBarCanvasInstance.SetActive(true);
            }
        }

        // Play visual spawn animation on all clients
        if (isSpawningAnimation)
        {
            StartCoroutine(PlaySpawnAnimation());
        }

        if (IsServer)
        {
            currentHP.Value = maxHP.Value;

            // Initialize movement direction
            Vector3 forwardMovementDirection = transform.forward;
            if (!isFlyingUnit)
            {
                forwardMovementDirection.y = 0;
            }
            if (forwardMovementDirection.sqrMagnitude < 0.01f)
            {
                forwardMovementDirection = Vector3.forward;
            }
            movementDirection = forwardMovementDirection.normalized;
        }
    }

    /// <summary>
    /// Called by card effects to set a custom particle effect prefab for this unit's spawn animation.
    /// </summary>
    public void SetSpawnParticleEffect(ParticleSystem particlePrefab)
    {
        spawnParticleEffectPrefab = particlePrefab;
    }

    /// <summary>
    /// Visual spawn animation - plays on all clients during the 5-second spawn sequence.
    /// Handles drop animation and particle effects.
    /// </summary>
    private System.Collections.IEnumerator PlaySpawnAnimation()
    {

        // Drop the unit from Y+2 to Y=0 in 0.05 seconds
        float dropDuration = 0.002f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        startPos.y = startPos.y + 5f;
        transform.position = startPos; // Start 2 units above
        Vector3 endPos = transform.position;
        endPos.y = 0f; // Land at Y=0
        yield return new WaitForSeconds(0.3f);
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / dropDuration;
            Vector3 newPos = transform.position;
            newPos.y = Mathf.Lerp(startPos.y, endPos.y, progress);
            transform.position = newPos;
            yield return null;
        }

        // Ensure exact landing position
        Vector3 finalPos = transform.position;
        finalPos.y = 0f;
        transform.position = finalPos;
        yield return new WaitForSeconds(0.01f);

        // Spawn particle effect at landing position on all clients
        SpawnSpawnParticleEffect(finalPos);

        // Spawn UI canvas overlay and rotate it during the wait period
        GameObject canvasInstance = null;
        if (spawnCanvasPrefab != null)
        {
            canvasInstance = Instantiate(spawnCanvasPrefab, transform);
            canvasInstance.transform.localPosition = new Vector3(0, 3f, 0); // 3 units above

            // Align canvas to camera's Z rotation for consistent appearance on both clients
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float cameraZ = mainCamera.transform.eulerAngles.z;
                canvasInstance.transform.Rotate(90f, 0, cameraZ);
            }
            else
            {
                // Default rotation if camera not found
                canvasInstance.transform.Rotate(90f, 0, 0);
            }
        }

        // Wait 1.95 seconds, rotating canvas every 0.4875 seconds (1/4 of total time)
        float rotationInterval = 1.95f / 4f; // 0.4875 seconds per rotation
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(rotationInterval);

            if (canvasInstance != null)
            {
                canvasInstance.transform.Rotate(0, 0, -90f); // Rotate 90 degrees around Z axis
            }
        }

        // Destroy canvas after animation completes
        if (canvasInstance != null)
        {
            Destroy(canvasInstance);
        }

        // Animation complete - flag will be reset by Update() on next frame
        Debug.Log($"[{GetType().Name}] Spawn animation complete for unit ID: {NetworkObjectId}");
        isSpawningAnimation = false;
    }

    /// <summary>
    /// Spawns a particle effect at the landing position during unit spawn animation.
    /// Override this in derived classes for custom particle effects.
    /// </summary>
    protected virtual void SpawnSpawnParticleEffect(Vector3 position)
    {
        // Use custom particle effect if one was set by card effect
        ParticleSystem particleEffectPrefab = spawnParticleEffectPrefab;

        // If none set, try to get from derived class
        if (particleEffectPrefab == null)
        {
            particleEffectPrefab = GetSpawnParticleEffectPrefab();
        }

        if (particleEffectPrefab == null)
        {
            Debug.Log($"[{GetType().Name}] No spawn particle effect prefab set.");
            return;
        }

        // Instantiate particle effect at landing position
        ParticleSystem spawnedEffect = Instantiate(particleEffectPrefab, position, Quaternion.identity);

        if (spawnedEffect != null)
        {
            // Scale based on unit type or default
            float effectScale = GetSpawnEffectRadius();
            spawnedEffect.transform.localScale = Vector3.one * effectScale * 3f;

            // Destroy after 5 seconds
            Destroy(spawnedEffect.gameObject, 5f);
        }
    }

    /// <summary>
    /// Override in derived classes to provide a custom particle system prefab for spawn animation.
    /// </summary>
    protected virtual ParticleSystem GetSpawnParticleEffectPrefab() => null;

    /// <summary>
    /// Override in derived classes to customize spawn particle effect radius scaling.
    /// </summary>
    protected virtual float GetSpawnEffectRadius() => 1f;

    /// <summary>
    /// Helper method to update HP bar display. Shows bar on first damage.
    /// </summary>
    private void UpdateHPBar()
    {
        if (hpBarCanvasInstance == null || hpBarSlider == null)
            return;


        float unitHeight = GetUnitHeight();
        hpBarCanvasInstance.transform.position = transform.position + Vector3.up * (unitHeight + 0.5f);


        // Keep HP bar rotation aligned to camera (90, 0, cameraZ)
        Camera mainCamera = Camera.main;
        float cameraZ = mainCamera != null ? mainCamera.transform.eulerAngles.z : 0;
        hpBarCanvasInstance.transform.rotation = Quaternion.Euler(90f, 0, cameraZ);

        // Show HP bar if unit has taken damage (HP < maxHP) and unit is not dead
        bool shouldShowHPBar = currentHP.Value < maxHP.Value && currentHP.Value > 0;

        if (shouldShowHPBar && !hpBarCanvasInstance.activeInHierarchy)
        {
            hpBarCanvasInstance.SetActive(true);
        }
        else if (!shouldShowHPBar && hpBarCanvasInstance.activeInHierarchy)
        {
            hpBarCanvasInstance.SetActive(false);
        }

        // Update slider value if visible
        if (hpBarCanvasInstance.activeInHierarchy)
        {
            hpBarSlider.SetValue(maxHP.Value > 0 ? (float)currentHP.Value / (float)maxHP.Value : 0f);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!IsServer) return;
        if (currentHP.Value <= 0) return;

        // Update network variables for clients to see position
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
            PaddleController[] paddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
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
            netObj.ChangeOwnership(OwnerClientId); // Change ownership to the client that owns this unit

            // Initialize projectile with universal ProjectileController
            if (projectileInstance.TryGetComponent<ProjectileController>(out var projectileController))
            {
                projectileController.InitializeWithTarget(currentTarget, attackDamage, OwnerClientId);
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
        return maxHP.Value;
    }

    // Implementation of IPlaceable.TakeDamage
    public virtual void TakeDamage(int amount)
    {
        if (!IsServer || currentHP.Value <= 0 || isDying) return;

        int newHP = currentHP.Value - amount;
        currentHP.Value = newHP;
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Took {amount} damage, HP is now {currentHP.Value}");

        // Flash red to indicate damage (all clients)
        FlashRedClientRpc();

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
            Dead = true;

            // Start death sequence
            StartCoroutine(HandleDeath());
        }
    }

    protected virtual System.Collections.IEnumerator HandleDeath()
    {
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Starting death sequence.");
        moveSpeed = 0f;
        currentTarget = null; // Clear current target when dead

        // Destroy HP bar if it exists
        if (hpBarCanvasInstance != null)
        {
            Destroy(hpBarCanvasInstance);
            hpBarCanvasInstance = null;
            hpBarSlider = null;
        }

        // Note: Collider is already disabled in TakeDamage when isDying is set to true
        // This ensures units stop targeting this unit immediately when it receives a fatal hit

        yield return new WaitForSeconds(2f);

        if (IsServer)
        {
            NetworkObject.Despawn();
        }
    }

    [ClientRpc]
    private void FlashRedClientRpc()
    {
        StartCoroutine(FlashRedRoutine());
    }

    private System.Collections.IEnumerator FlashRedRoutine()
    {
        // Get all renderers on this unit and its children
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0) yield break;

        // Store original materials and their color property names
        Material[][] originalMaterials = new Material[renderers.Length][];
        Color[][] originalColors = new Color[renderers.Length][];
        string[][] colorPropertyNames = new string[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = new Material[renderers[i].materials.Length];
            originalColors[i] = new Color[renderers[i].materials.Length];
            colorPropertyNames[i] = new string[renderers[i].materials.Length];

            // Copy materials and store original colors
            for (int j = 0; j < renderers[i].materials.Length; j++)
            {
                originalMaterials[i][j] = new Material(renderers[i].materials[j]);

                // Check for common color property names
                if (renderers[i].materials[j].HasProperty("_BaseColor"))
                {
                    originalColors[i][j] = renderers[i].materials[j].GetColor("_BaseColor");
                    colorPropertyNames[i][j] = "_BaseColor";
                }
                else if (renderers[i].materials[j].HasProperty("_Color"))
                {
                    originalColors[i][j] = renderers[i].materials[j].GetColor("_Color");
                    colorPropertyNames[i][j] = "_Color";
                }
                else
                {
                    colorPropertyNames[i][j] = null; // No color property found
                }
            }
        }

        // Flash to red
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int j = 0; j < renderers[i].materials.Length; j++)
            {
                if (colorPropertyNames[i][j] != null)
                {
                    Color redColor = originalColors[i][j];
                    redColor = new Color(1f, 0f, 0f, redColor.a); // Red color maintaining alpha
                    renderers[i].materials[j].SetColor(colorPropertyNames[i][j], redColor);
                }
            }
        }
        Debug.Log($"[{GetType().Name}] Flashing red to indicate damage.");
        // Wait for flash duration
        yield return new WaitForSeconds(0.1f);

        // Restore original colors
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int j = 0; j < renderers[i].materials.Length; j++)
            {
                if (colorPropertyNames[i][j] != null)
                {
                    renderers[i].materials[j].SetColor(colorPropertyNames[i][j], originalColors[i][j]);
                }
            }
        }
    }

    #region IHasHP Implementation

    // IHasHP interface implementations (converts int HP to float for interface compatibility)
    float IHasHP.GetCurrentHP()
    {
        return (float)currentHP.Value;
    }

    float IHasHP.GetMaxHP()
    {
        return (float)maxHP.Value;
    }

    public float GetHPPercentage()
    {
        if (maxHP.Value <= 0) return 0f;
        return (float)currentHP.Value / (float)maxHP.Value;
    }

    public bool IsAlive()
    {
        return currentHP.Value > 0 && !isDying;
    }

    public void setDead()
    {
        Dead = true;
    }
    public bool isdead()
    {
        return Dead;
    }

    /// <summary>
    /// Set the attack damage for this unit (called when applying card levels)
    /// </summary>
    public void SetAttackDamage(int damage)
    {
        attackDamage = damage;
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Attack damage set to {damage}");
    }

    /// <summary>
    /// Set the max HP for this unit (called when applying card levels)
    /// </summary>
    public void SetMaxHP(int hp)
    {
        if (IsServer)
        {
            maxHP.Value = hp;
            currentHP.Value = hp;
        }

        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Max HP set to {hp}");
    }

    /// <summary>
    /// Get the height of the unit by checking its local scale.
    /// Falls back to 2 if local scale is 0.
    /// </summary>
    private float GetUnitHeight()
    {
        float height = transform.localScale.y;
        return height > 0 ? height : 2f; // Default fallback height if scale is 0
    }

    #endregion
}