using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Universal projectile controller that moves projectiles in a given direction.
/// Can be used for any type of projectile (arrows, bullets, spells, etc.)
/// Attach this script to any projectile prefab.
/// </summary>
public class ProjectileController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 15f;

    [Header("Damage")]
    public int damage = 10;
    public ulong firerClientId; // Who fired this projectile

    private Vector3 moveDirection = Vector3.forward;
    private Transform targetTransform; // Optional: move towards target instead of fixed direction
    protected bool hasHit = false;

    /// <summary>
    /// Initialize projectile to move towards a target (homing projectile)
    /// </summary>
    public void InitializeWithTarget(Transform target, int damageAmount, ulong firerId)
    {
        targetTransform = target;
        damage = damageAmount;
        firerClientId = firerId;
        moveDirection = Vector3.forward;
    }

    /// <summary>
    /// Initialize projectile to move in a fixed direction (non-homing projectile)
    /// </summary>
    public void InitializeWithDirection(Vector3 direction, int damageAmount, ulong firerId)
    {
        moveDirection = direction.normalized;
        targetTransform = null;
        damage = damageAmount;
        firerClientId = firerId;
    }

    protected void Update()
    {
        if (!IsServer)
            return;

        // If target is dead or inactive, destroy projectile
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            NetworkObject.Despawn();
            return;
        }

        // Update direction based on target
        moveDirection = (targetTransform.position - transform.position).normalized;

        // Move projectile
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // Check for target collision
        float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
        if (distanceToTarget < 0.5f)
        {
            OnHitTarget(targetTransform);
        }
    }

    /// <summary>
    /// Called when projectile hits a target
    /// </summary>
    protected virtual void OnHitTarget(Transform target)
    {
        hasHit = true;

        // Try to damage the target if it's a unit or building
        if (target.TryGetComponent<BaseUnitController>(out var unit))
        {
            unit.TakeDamage(damage);
        }
        else if (target.TryGetComponent<BaseBuildingController>(out var building))
        {
            building.TakeDamage(damage);
        }

        // Destroy projectile
        if (IsServer)
        {
            NetworkObject.Despawn();
        }
    }
}
