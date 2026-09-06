using UnityEngine;
using Unity.Netcode;

public class StoneSentinelController : BaseUnitController
{
    private void Awake()
    {
        // Set StoneSentinel's specific stats
        weight = 5f;
        moveSpeed = 3f;
        attackRange = 4f;
        attackCooldown = 2f;
        attackDamage = 15;
        targetSearchRadius = 10f;
    }


}
