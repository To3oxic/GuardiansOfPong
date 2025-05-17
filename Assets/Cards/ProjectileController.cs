using UnityEngine;
using Unity.Netcode;

// Moved to Assets/Cards folder
public class ProjectileController : NetworkBehaviour
{
    public float speed = 10f;
    public float maxLifetime = 5f;
    public bool isAOE = false;
    public float aoeRadius = 10f;
    public LayerMask hitMask;

    private Transform target;
    private int damage;
    private ulong ownerClientId;
    private bool initialized = false;
    private float lifetime = 0f;

    public void Initialize(Transform target, int damage, ulong ownerClientId, bool isAOE = false, float aoeRadius = 0f)
    {
        this.target = target;
        this.damage = damage;
        this.ownerClientId = ownerClientId;
        
        // Only override these values if explicitly passed
        if (isAOE) this.isAOE = true;
        if (aoeRadius > 0f) this.aoeRadius = aoeRadius;
        
        initialized = true;
        
        // Log initialization for debugging
        if (this.isAOE)
        {
            Debug.Log($"[ProjectileController] Initialized AOE projectile with radius {this.aoeRadius} and damage {damage}");
        }
        else
        {
            Debug.Log($"[ProjectileController] Initialized direct projectile with damage {damage}");
        }
    }

    private void Update()
    {
        if (!IsServer || !initialized) return;

        lifetime += Time.deltaTime;
        if (lifetime > maxLifetime)
        {
            NetworkObject.Despawn();
            return;
        }

        if (target == null)
        {
            // If target is destroyed, continue in last direction
            transform.position += transform.forward * speed * Time.deltaTime;
            return;
        }

        // Move towards the target
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;
        
        // Rotate to face the direction of travel
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Check for impact (as a fallback if trigger doesn't work)
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget < 0.5f)
        {
            OnImpact();
        }
    }

    private void OnImpact(Collider hitCollider = null)
    {
        if (isAOE)
        {
            // Damage all valid targets in radius - ignore layer mask to affect everything
            Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius);
            int targetsHit = 0;
            
            foreach (var hit in hits)
            {
                bool damageDealt = false;
                
                // Try IPlaceable interface first (units, structures)
                if (hit.TryGetComponent<IPlaceable>(out var placeable))
                {
                    // Only damage enemies
                    if (placeable is NetworkBehaviour netPlaceable && netPlaceable.OwnerClientId != ownerClientId)
                    {
                        // Check if this would be a fatal hit for BaseUnitController
                        bool wouldBeLethal = false;
                        if (placeable is BaseUnitController unitController)
                        {
                            wouldBeLethal = unitController.WouldDieFromDamage(damage);
                            if (wouldBeLethal)
                            {
                                Debug.Log($"[ProjectileController] Fatal hit on {hit.name} - unit will die from {damage} damage");
                            }
                        }
                        
                        // Apply the damage
                        placeable.TakeDamage(damage);
                        
                        damageDealt = true;
                        targetsHit++;
                    }
                }
                // Try PaddleController for paddles
                else if (hit.TryGetComponent<PaddleController>(out var paddle))
                {
                    if (paddle.OwnerClientId != ownerClientId)
                    {
                        paddle.ReduceHP(damage);
                        damageDealt = true;
                        targetsHit++;
                    }
                }
                // We've already handled the main damage types (IPlaceable and PaddleController)
                // If we need to handle other damageable objects, we would add them here
                
                if (damageDealt)
                {
                    Debug.Log($"[ProjectileController] AOE hit on {hit.name} for {damage} damage");
                }
            }
            
            Debug.Log($"[ProjectileController] AOE explosion hit {targetsHit} targets with {damage} damage in radius {aoeRadius}");
            
            // Could add visual effect for AOE explosion here
            // GameObject explosionVFX = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            // Destroy(explosionVFX, 2f); // Destroy after effect duration
        }
        else if (hitCollider != null)
        {
            // Hit something with trigger
            if (hitCollider.TryGetComponent<IPlaceable>(out var placeable))
            {
                if (placeable is NetworkBehaviour netPlaceable && netPlaceable.OwnerClientId != ownerClientId)
                {
                    // Check if this would be a fatal hit for BaseUnitController
                    bool wouldBeLethal = false;
                    int currentHP = 0;
                    
                    if (placeable is BaseUnitController unitController)
                    {
                        wouldBeLethal = unitController.WouldDieFromDamage(damage);
                        currentHP = unitController.GetCurrentHP();
                        
                        if (wouldBeLethal)
                        {
                            Debug.Log($"[ProjectileController] Fatal direct hit on {hitCollider.name} - unit will die from {damage} damage");
                        }
                        else
                        {
                            Debug.Log($"[ProjectileController] Direct hit on {hitCollider.name} for {damage} damage, HP: {currentHP}/{unitController.GetMaxHP()}");
                        }
                    }
                    
                    // Apply the damage
                    placeable.TakeDamage(damage);
                }
            }
        }
        else if (target != null)
        {
            // Single target (fallback method)
            if (target.TryGetComponent<IPlaceable>(out var placeable))
            {
                if (placeable is NetworkBehaviour netPlaceable && netPlaceable.OwnerClientId != ownerClientId)
                {
                    // Check if this would be a fatal hit for BaseUnitController
                    bool wouldBeLethal = false;
                    int currentHP = 0;
                    
                    if (placeable is BaseUnitController unitController)
                    {
                        wouldBeLethal = unitController.WouldDieFromDamage(damage);
                        currentHP = unitController.GetCurrentHP();
                        
                        if (wouldBeLethal)
                        {
                            Debug.Log($"[ProjectileController] Fatal target hit on {target.name} - unit will die from {damage} damage");
                        }
                        else
                        {
                            Debug.Log($"[ProjectileController] Target hit on {target.name} for {damage} damage, HP: {currentHP}/{unitController.GetMaxHP()}");
                        }
                    }
                    
                    // Apply the damage
                    placeable.TakeDamage(damage);
                }
            }
        }
        
        // Visual effect could be added here
        
        NetworkObject.Despawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !initialized) return;
        
        // Skip collision with owner
        if (other.TryGetComponent<NetworkBehaviour>(out var netBehaviour) && 
            netBehaviour.OwnerClientId == ownerClientId)
            return;
            
        // Skip collision with triggers
        if (other.isTrigger)
            return;
            
        OnImpact(other);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (isAOE)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
    }
}