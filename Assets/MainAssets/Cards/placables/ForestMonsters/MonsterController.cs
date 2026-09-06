using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class MonsterController : BaseUnitController
{
    private bool isInitialized = false;

    private void Awake()
    {
        // Set Monster's specific stats
        moveSpeed = 3.5f;
        attackRange = 3f;
        attackCooldown = 1.8f;
        attackDamage = 18;

        targetSearchRadius = 8f;
        weight = 1f;
    }
}