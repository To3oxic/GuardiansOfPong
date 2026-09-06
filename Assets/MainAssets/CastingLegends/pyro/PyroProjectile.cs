using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PyroProjectile : ProjectileController
{
    [Header("AOE Settings")]
    public float aoeRange = 15f; // Range of the area of effect explosion
    public int aoeDamage = 20; // Damage dealt to units in AOE (can differ from direct hit damage)

    protected override void OnHitTarget(Transform target)
    {
        if (hasHit) return;

        // Call base implementation (deals direct damage)
        base.OnHitTarget(target);

        // Detect all enemy units within AOE range and deal AOE damage
        DetectAndDamageEnemiesInAOE();
    }

    private void DetectAndDamageEnemiesInAOE()
    {
        // Get all units in range
        Collider[] colliders = Physics.OverlapSphere(transform.position, aoeRange);

        HashSet<BaseUnitController> damagedUnits = new HashSet<BaseUnitController>();

        foreach (var collider in colliders)
        {
            // Skip if it's our own collider
            if (collider.gameObject == gameObject) continue;

            // Check if it's a unit
            if (collider.TryGetComponent<BaseUnitController>(out var unit))
            {
                // Skip if already dead or dying
                if (!unit.IsAlive())
                    continue;

                // Skip if it's our own unit (same owner)
                if (unit.OwnerClientId == firerClientId)
                    continue;

                // Skip if we already damaged this unit this frame
                if (damagedUnits.Contains(unit))
                    continue;

                // Deal AOE damage
                unit.TakeDamage(aoeDamage);
                damagedUnits.Add(unit);
            }
        }

        if (damagedUnits.Count > 0)
        {
        }
    }

    // Optional: Visualize AOE range in editor
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aoeRange);
    }
}

