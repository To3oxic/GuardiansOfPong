using UnityEngine;
using Unity.Netcode;

public class BraveKnightController : BaseUnitController
{
    private void Awake()
    {
        // Set BraveKnight's specific stats
        moveSpeed = 5f;
        attackRange = 3f;
        attackCooldown = 1.2f;
        attackDamage = 25;

        weight = 3f;
        targetSearchRadius = 12f;
    }
}
