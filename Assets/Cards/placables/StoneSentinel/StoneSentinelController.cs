using UnityEngine;
using Unity.Netcode;

public class StoneSentinelController : BaseUnitController
{
    private void Awake()
    {
        // Set StoneSentinel's specific stats
        moveSpeed = 3f;
        attackRange = 4f;
        attackCooldown = 2f;
        attackDamage = 15;
        maxHP = 150;
        targetSearchRadius = 10f;
    }

    protected override void FindNewTarget()
    {
        if (!IsServer) return;

        // First, look for structures (non-unit placeables)
        Collider[] colliders = Physics.OverlapSphere(transform.position, targetSearchRadius);
        float closestDistance = float.MaxValue;
        Transform closestTarget = null;

        foreach (var collider in colliders)
        {
            // Skip if it's our own collider or if the object is inactive
            if (collider.gameObject == gameObject || !collider.gameObject.activeInHierarchy)
                continue;

            // Check if it's a placeable
            if (collider.TryGetComponent<IPlaceable>(out var placeable))
            {
                // Skip if it's our own unit/structure
                if (placeable is NetworkBehaviour netPlaceable && netPlaceable.OwnerClientId == OwnerClientId)
                    continue;

                // Skip if it's a unit (we only want structures)
                if (placeable is BaseUnitController)
                    continue;

                // Skip if the placeable is dying or dead
                if (placeable is BaseUnitController unit)
                    continue;

                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = collider.transform;
                }
            }
        }

        // If no structures found, look for opponent's paddle
        if (closestTarget == null)
        {
            PaddleController[] paddles = FindObjectsOfType<PaddleController>();
            foreach (var paddle in paddles)
            {
                // Skip if paddle is inactive
                if (!paddle.gameObject.activeInHierarchy) continue;

                // Only target opponent's paddle
                if (paddle.OwnerClientId != OwnerClientId)
                {
                    float distance = Vector3.Distance(transform.position, paddle.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = paddle.transform;
                    }
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
                Debug.Log($"[StoneSentinel] (ID: {NetworkObjectId}) New target acquired: {currentTarget.name}");
            }
        }
        else
        {
            currentTarget = null;
        }
    }
}
