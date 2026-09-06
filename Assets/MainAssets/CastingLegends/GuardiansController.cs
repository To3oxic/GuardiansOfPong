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

    [Header("Attack Recenter")]
    [Tooltip("Seconds after an attack is triggered before the guardian eases back to its spawn position/rotation. Covers drift from root-motion attack animations.")]
    public float recenterDelay = 1f;
    [Tooltip("Higher = snappier return to the spawn transform. 0 = instant snap.")]
    public float recenterSpeed = 8f;

    protected Transform currentTarget;
    protected Animator animator;

    // Spawn pose the guardian is pinned to between attacks
    protected Vector3 homePosition;
    protected Quaternion homeRotation;
    protected bool homeCaptured = false;
    private float recenterAtTime = -1f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        animator = GetComponent<Animator>(); // Get the Animator component

        // Ensure firePoint is assigned, default to self if not
        if (firePoint == null)
        {
            firePoint = transform;
        }

        CaptureHome();
    }

    /// <summary>
    /// Records the current transform as the pose to return to after each attack.
    /// Runs on server and every client so animation drift is corrected locally.
    /// </summary>
    protected void CaptureHome()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
        homeCaptured = true;
    }

    /// <summary>
    /// Call right after triggering the attack animation. Schedules a return to the
    /// spawn pose once the animation has had time to play.
    /// </summary>
    protected void ScheduleRecenter()
    {
        recenterAtTime = Time.time + Mathf.Max(0f, recenterDelay);
    }

    protected virtual void LateUpdate()
    {
        if (!homeCaptured || recenterAtTime < 0f || Time.time < recenterAtTime) return;

        if (recenterSpeed <= 0f)
        {
            transform.SetPositionAndRotation(homePosition, homeRotation);
            recenterAtTime = -1f;
            return;
        }

        float t = 1f - Mathf.Exp(-recenterSpeed * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, homePosition, t),
            Quaternion.Slerp(transform.rotation, homeRotation, t));

        if ((transform.position - homePosition).sqrMagnitude < 0.0001f &&
            Quaternion.Angle(transform.rotation, homeRotation) < 0.1f)
        {
            transform.SetPositionAndRotation(homePosition, homeRotation);
            recenterAtTime = -1f;
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
                ScheduleRecenter();
            }
            else
            {
                // Fallback if animator isn't set up: fire directly.
                // Consider removing this if animation is strictly required for firing.
                ActualFireProjectile();
            }
            nextFireTime = Time.time + 1f / fireRate;
        }
    }

    [ClientRpc]
    protected void PlayAttackAnimationClientRpc()
    {
        if (IsServer) return; // Server already triggered its animator

        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
            ScheduleRecenter();
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
