using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;
using Unity.UI.Shaders.Sample;

/// <summary>
/// Base class for building/placeable units (Tree, Stone Tower, etc.) that emerge from the ground.
/// Unlike BaseUnitController which has mobile units, buildings:
/// - Don't move after spawn
/// - Emerge from the ground during spawn animation
/// - Can take damage and defend
/// - Can have abilities triggered by other game events
/// </summary>
public abstract class BaseBuildingController : NetworkBehaviour, IPlaceable, IHasHP
{
    [Header("Building Settings")]
    public bool isDefensive = true;
    public bool canAttackFlyingUnits = false;
    public bool isRangedUnit = false;
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("HP Bar Settings")]
    public GameObject hpBarCanvasPrefab; // Assign a Canvas prefab with Image component for HP bar

    // Protected fields that will be set by child classes
    [System.NonSerialized] protected float attackRange;
    [System.NonSerialized] protected float attackCooldown;
    [System.NonSerialized] protected float targetSearchRadius;
    [System.NonSerialized] protected int attackDamage;
    [System.NonSerialized] protected int maxHP;

    // HP bar UI references
    [System.NonSerialized] protected GameObject hpBarCanvasInstance;
    [System.NonSerialized] protected Unity.UI.Shaders.Sample.CustomSlider hpBarSlider; // The HP bar slider

    // Flag to indicate this building is in the process of dying
    [System.NonSerialized] protected bool isDying = false;

    // Flag to indicate this building is still in spawn animation phase (emerging from ground)
    [System.NonSerialized] public bool isSpawningAnimation = true;

    // Particle effect to spawn during animation (set in Inspector on prefab)
    public ParticleSystem spawnParticleEffectPrefab;

    // Building collider (trigger-based for detection)
    protected Collider buildingCollider;
    protected Animator animator;
    protected NetworkAnimator networkAnimator;
    protected Transform currentTarget;
    protected float cooldownTimer = 0f;

    protected bool Dead = false;

    // NetworkVariables for synchronization
    protected NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    protected NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();

    // NetworkVariable for HP
    protected NetworkVariable<int> currentHP = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    protected Vector3 spawnPosition; // Initial spawn position
    private const float GROUND_LEVEL = 0f;

    protected virtual void Start()
    {
        buildingCollider = GetComponent<Collider>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        spawnPosition = transform.position;

        // Setup trigger collider
        if (buildingCollider != null)
        {
            buildingCollider.isTrigger = true;
        }

        if (IsServer)
        {
            currentHP.Value = maxHP;
        }
    }

    protected virtual void Update()
    {
        // Update HP bar display on all clients
        UpdateHPBar();

        if (!IsServer)
        {
            return;
        }

        // Skip all gameplay logic if currently spawning
        if (isSpawningAnimation)
        {
            return;
        }

        // Update cooldown timer
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // Find targets and handle attacking
        if (!Dead)
        {
            FindNewTarget();
            if (currentTarget != null)
            {
                HandleTargeting();
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        buildingCollider = GetComponent<Collider>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        spawnPosition = transform.position;

        // Ensure collider is trigger
        if (buildingCollider != null)
        {
            buildingCollider.isTrigger = true;
        }

        // Create HP bar canvas on spawn (disabled initially)
        if (hpBarCanvasPrefab != null)
        {
            hpBarCanvasInstance = Instantiate(hpBarCanvasPrefab, transform);
            hpBarCanvasInstance.transform.localPosition = new Vector3(0, 2f, 0); // Above the building

            // Align canvas rotation to camera's Z rotation (like spawn animation)
            Camera mainCamera = Camera.main;
            float cameraZ = mainCamera != null ? mainCamera.transform.eulerAngles.z : 0;
            hpBarCanvasInstance.transform.localRotation = Quaternion.Euler(90f, 0, cameraZ);

            // Set RectTransform size for UI bar
            RectTransform hpBarRect = hpBarCanvasInstance.GetComponent<RectTransform>();
            if (hpBarRect != null)
            {
                hpBarRect.sizeDelta = new Vector2(100, 75);
            }

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

        // Play visual spawn animation on all clients (emerge from ground)
        if (isSpawningAnimation)
        {
            StartCoroutine(PlaySpawnAnimation());
        }

        if (IsServer)
        {
            currentHP.Value = maxHP;
        }
    }

    /// <summary>
    /// Visual spawn animation - building emerges from the ground.
    /// Plays on all clients during the spawn sequence.
    /// </summary>
    private System.Collections.IEnumerator PlaySpawnAnimation()
    {
        // Building emerges from ground: Y = -height to Y = 0 in 0.5 seconds
        float emergeDuration = 0.5f;
        float elapsed = 0f;

        Vector3 startPos = spawnPosition;
        startPos.y = -2f; // Start underground

        Vector3 endPos = spawnPosition;
        endPos.y = GROUND_LEVEL; // End at ground level

        // Emerge from ground
        while (elapsed < emergeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / emergeDuration;
            Vector3 newPos = transform.position;
            newPos.y = Mathf.Lerp(startPos.y, endPos.y, progress);
            transform.position = newPos;

            // Network position sync
            if (IsServer)
            {
                networkPosition.Value = newPos;
            }

            yield return null;
        }

        // Ensure exact final position
        Vector3 finalPos = transform.position;
        finalPos.y = GROUND_LEVEL;
        transform.position = finalPos;

        if (IsServer)
        {
            networkPosition.Value = finalPos;
        }

        // Spawn particle effect at final position
        SpawnSpawnParticleEffect(finalPos);

        // Wait before becoming fully active (1 second total including emerge)
        yield return new WaitForSeconds(0.5f);

        // Animation complete
        Debug.Log($"[{GetType().Name}] Spawn animation complete for building ID: {NetworkObjectId}");
        isSpawningAnimation = false;
    }

    /// <summary>
    /// Spawns a particle effect at the specified position during building spawn animation.
    /// Runs on all clients via NetworkObject.
    /// </summary>
    private void SpawnSpawnParticleEffect(Vector3 position)
    {
        if (spawnParticleEffectPrefab == null)
        {
            return;
        }

        // Instantiate particle effect at position
        ParticleSystem particleInstance = Instantiate(spawnParticleEffectPrefab, position, Quaternion.identity);

        // Destroy after particle system duration
        if (particleInstance != null)
        {
            float duration = particleInstance.main.duration;
            Destroy(particleInstance.gameObject, duration + 1f);
        }
    }

    /// <summary>
    /// Find new attack targets in range (enemies/units).
    /// Override this to customize target-finding behavior.
    /// </summary>
    protected virtual void FindNewTarget()
    {
        if (Dead)
        {
            currentTarget = null;
            return;
        }

        // Search for units or placeables in range
        Collider[] colliders = Physics.OverlapSphere(transform.position, targetSearchRadius);

        Transform bestTarget = null;
        float closestDistance = targetSearchRadius;

        foreach (Collider collider in colliders)
        {
            if (collider.gameObject == gameObject) continue; // Skip self

            // Check if it's a unit or placeable we can attack
            BaseUnitController unit = collider.GetComponent<BaseUnitController>();
            IPlaceable placeable = collider.GetComponent<IPlaceable>();

            if (unit != null && unit.GetCurrentHP() > 0)
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = collider.transform;
                }
            }
            else if (placeable != null && !(this as IPlaceable).Equals(placeable) && collider.gameObject != gameObject)
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = collider.transform;
                }
            }
        }

        currentTarget = bestTarget;
    }

    /// <summary>
    /// Handle attacking current target if in range.
    /// </summary>
    protected virtual void HandleTargeting()
    {
        if (currentTarget == null || cooldownTimer > 0)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        if (distanceToTarget <= attackRange)
        {
            // Attack the target
            if (currentTarget.TryGetComponent<NetworkObject>(out var targetNetObj))
            {
                AttackServerRpc(targetNetObj.NetworkObjectId);
            }
            cooldownTimer = attackCooldown;
        }
    }

    [Rpc(SendTo.Server)]
    protected virtual void AttackServerRpc(ulong targetNetworkObjectId)
    {
        if (cooldownTimer > 0) return;

        // Find the target object by NetworkObjectId
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            return;
        }

        GameObject targetObject = targetNetObj.gameObject;

        if (isRangedUnit && projectilePrefab != null && projectileSpawnPoint != null)
        {
            // Spawn projectile with universal ProjectileController
            Vector3 shootDirection = (targetObject.transform.position - projectileSpawnPoint.position).normalized;
            GameObject projectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.LookRotation(shootDirection));

            // Spawn on network
            NetworkObject projNetObj = projectile.GetComponent<NetworkObject>();
            if (projNetObj != null)
            {
                projNetObj.SpawnWithOwnership(OwnerClientId);

                // Initialize projectile controller
                if (projectile.TryGetComponent<ProjectileController>(out var projController))
                {
                    projController.InitializeWithTarget(targetObject.transform, attackDamage, OwnerClientId);
                }
            }
        }
        else
        {
            // Melee attack
            if (targetObject.TryGetComponent<BaseUnitController>(out var unitController))
            {
                unitController.TakeDamage(attackDamage);
            }
            else if (targetObject.TryGetComponent<BaseBuildingController>(out var buildingController))
            {
                buildingController.TakeDamage(attackDamage);
            }
            else if (targetObject.TryGetComponent<IPlaceable>(out var placeable))
            {
                placeable.TakeDamage(attackDamage);
            }
        }

        cooldownTimer = attackCooldown;
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
            // Immediately mark as dying
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

    /// <summary>
    /// Helper method to update HP bar display. Shows bar on first damage.
    /// </summary>
    private void UpdateHPBar()
    {
        if (hpBarCanvasInstance == null || hpBarSlider == null)
            return;

        // Keep HP bar rotation aligned to camera (90, 0, cameraZ)
        Camera mainCamera = Camera.main;
        float cameraZ = mainCamera != null ? mainCamera.transform.eulerAngles.z : 0;
        hpBarCanvasInstance.transform.localRotation = Quaternion.Euler(90f, 0, cameraZ);

        // Show HP bar if building has taken damage (HP < maxHP)
        bool shouldShowHPBar = currentHP.Value < maxHP;

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
            hpBarSlider.SetValue(maxHP > 0 ? (float)currentHP.Value / (float)maxHP : 0f);
        }
    }

    protected virtual System.Collections.IEnumerator HandleDeath()
    {
        Debug.Log($"[{GetType().Name}] (ID: {NetworkObjectId}) Starting death sequence.");
        currentTarget = null;

        yield return new WaitForSeconds(2f);

        if (IsServer)
        {
            // Despawn the building
            GetComponent<NetworkObject>().Despawn();
        }
    }

    /// <summary>
    /// Override this method to implement building abilities.
    /// Called manually by your game logic when appropriate.
    /// </summary>
    public virtual void ApplyAbility()
    {
        if (isDying || isSpawningAnimation)
        {
            return;
        }

        Debug.Log($"[{GetType().Name}] Ability called (override ApplyAbility to implement)");
        // Child classes should override this to implement specific building abilities
    }

    // Implementation of IPlaceable
    public Transform GetTransform() => transform;
    public Vector3 GetPosition() => transform.position;
    public float GetCurrentHP() => currentHP.Value;
    public float GetMaxHP() => maxHP;
    public bool IsDead() => Dead;

    // Implementation of IHasHP
    public float GetHPPercentage() => maxHP > 0 ? currentHP.Value / (float)maxHP : 0f;
    public bool IsAlive() => currentHP.Value > 0 && !Dead;
}
