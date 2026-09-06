using UnityEngine;
using Unity.Netcode;

public class AngryBearController : BaseUnitController
{
    private void Awake()
    {
        // Set AngryBear's specific stats
        moveSpeed = 4f;
        attackRange = 3f;
        attackCooldown = 1.5f;
        attackDamage = 20;

        targetSearchRadius = 10f;
    }
}