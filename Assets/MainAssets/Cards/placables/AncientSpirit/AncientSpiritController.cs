using UnityEngine;
using Unity.Netcode;

public class AncientSpiritController : BaseUnitController
{
    private void Awake()
    {
        // Set Ancient Spirit's specific stats
        moveSpeed = 2.5f;             // Adjust as needed
        attackRange = 20f;              // Reduced range for better gameplay
        attackCooldown = 2.5f;           // Slower cooldown for AOE?
        attackDamage = 5;             // AOE damage                // Adjust as needed
        targetSearchRadius = 8f;       // How far it looks for targets
        weight = 5f;
        // Specific Ancient Spirit settings
        isRangedUnit = true;
        canAttackFlyingUnits = true;  // Can target flyers
        isFlyingUnit = false;         // Assuming it walks, change if it flies

        // Ensure projectile settings are set in the Inspector
        if (projectilePrefab == null)
        {
            Debug.LogError($"[AncientSpiritController] Projectile Prefab not set for {gameObject.name}!");
        }
        if (projectileSpawnPoint == null)
        {
            Debug.LogWarning($"[AncientSpiritController] Projectile Spawn Point not set for {gameObject.name}, using default position.");
        }
    }

    // Optional: Override methods if needed for special behavior specific to Ancient Spirit
    // For example, custom animation triggers or sounds
    // protected override void AttackTarget()
    // {
    //     base.AttackTarget();
    //     // Add custom sound/effect here
    // }
}