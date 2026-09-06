using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class GuardiansController : NetworkBehaviour
{
    [Header("Guardian Settings")]
    public float attackRange = 10f;
    public float fireRate = 1f; // Attacks per second
    protected float nextFireTime = 0f;

    [Header("Projectile")]
    public GameObject projectilePrefab; // Assign your GuardiansProjectile prefab here
    public Transform firePoint; // Point from where projectiles are fired
    public int projectileDamage = 10;
    // public bool isAoe = false; // Future consideration

    [Header("Animation")]
    public string attackTriggerName = "Attack"; // Name of the trigger parameter in your Animator

    protected Transform currentTarget;
    protected Animator animator;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        animator = GetComponent<Animator>(); // Get the Animator component

        // Ensure firePoint is assigned, default to self if not
        if (firePoint == null)
        {
            firePoint = transform;
        }
        if (animator == null)
        {
            Debug.LogWarning("GuardiansController is missing an Animator component. Animations will not play, and animation-driven firing will not work.", this);
        }
    }

    protected virtual void Update()
    {
        if (!IsServer) return;

        FindTarget();
        HandleAttackAttempt(); // Renamed for clarity, triggers animation
    }

    protected virtual void FindTarget()
    {
        // Simple targeting: find the closest BaseUnitController within range
        // This assumes enemy units have the "BaseUnitController" component and are on a different team/layer if applicable

        currentTarget = null; // Reset target each frame or less frequently if performance is an issue
                              // float closestDistance = attackRange; // Not strictly needed with Linq OrderBy

        BaseUnitController[] allUnits = FindObjectsByType<BaseUnitController>(FindObjectsSortMode.None);
        List<BaseUnitController> potentialTargets = new List<BaseUnitController>();

        foreach (var unit in allUnits)
        {
            // Basic check: is the unit an enemy?
            if (unit.OwnerClientId == OwnerClientId) continue; // Don't target own units

            float distanceToUnit = Vector3.Distance(transform.position, unit.transform.position);
            if (distanceToUnit <= attackRange)
            {
                potentialTargets.Add(unit);
            }
        }

        if (potentialTargets.Any())
        {
            // Target the closest one
            currentTarget = potentialTargets.OrderBy(u => Vector3.Distance(transform.position, u.transform.position))
                                          .FirstOrDefault()?.transform;
        }
    }

    protected virtual void HandleAttackAttempt()
    {
        if (currentTarget != null && !currentTarget.GetComponent<BaseUnitController>().isdead() && Time.time >= nextFireTime)
        {
            // Mark target as dead immediately so other units won't attack it during projectile flight
            if (currentTarget.GetComponent<BaseUnitController>().GetCurrentHP() - projectileDamage <= 0)
            {
                currentTarget.GetComponent<BaseUnitController>().setDead();
            }

            // Just attack the target normally, let the projectile damage system handle it
            if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
            {
                animator.SetTrigger(attackTriggerName); // Server plays its animation
                PlayAttackAnimationClientRpc();     // Tell clients to play animation
            }
            else
            {
                // Fallback if animator isn't set up: fire directly.
                // Consider removing this if animation is strictly required for firing.
                Debug.LogWarning("GuardiansController: Animator or attackTriggerName not set. Firing directly. Ensure Animator is configured for animation-driven firing.", this);
                ActualFireProjectile();
            }
            nextFireTime = Time.time + 1f / fireRate;
        }
    }

    [ClientRpc]
    protected void PlayAttackAnimationClientRpc()
    {
        if (IsServer) return; // Server already triggered its animator

        Debug.Log($"[GuardiansController] PlayAttackAnimationClientRpc received on client {NetworkManager.Singleton.LocalClientId} for {gameObject.name}. Animator is null: {animator == null}");

        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
        }
    }

    // PUBLIC method to be called by an Animation Event
    public void AnimationEvent_FireProjectile()
    {
        if (!IsServer) return; // Critical: Only the server should execute this projectile firing logic

        ActualFireProjectile();
    }

    protected virtual void ActualFireProjectile()
    {
        if (currentTarget == null) // Target might have died or moved out of range
        {
            // Debug.Log("Guardian: Target lost before projectile fire event.", this);
            return;
        }

        if (projectilePrefab != null && firePoint != null)
        {
            // Instantiate the projectile on the server
            Vector3 shootDirection = (currentTarget.position - firePoint.position).normalized;
            GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(shootDirection));
            NetworkObject projectileNO = projectileGO.GetComponent<NetworkObject>();

            if (projectileNO != null)
            {
                projectileNO.Spawn(true); // Spawn with ownership to the server
                projectileNO.ChangeOwnership(OwnerClientId);
            }

            // Use universal ProjectileController
            if (projectileGO.TryGetComponent<ProjectileController>(out var projController))
            {
                projController.InitializeWithTarget(currentTarget, projectileDamage, OwnerClientId);
            }

            else
            {
                Debug.LogError("Projectile prefab does not have ProjectileController or GuardiansProjectile component!", this);
                if (projectileNO != null && projectileNO.IsSpawned) projectileNO.Despawn(true); // Clean up if misconfigured
                else Destroy(projectileGO);
            }
        }
    }

    // Optional: Visualize attack range in editor
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
