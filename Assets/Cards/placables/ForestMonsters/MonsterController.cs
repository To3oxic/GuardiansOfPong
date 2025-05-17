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
        attackRange = 2f;
        attackCooldown = 1.8f;
        attackDamage = 18;
        maxHP = 90;
        targetSearchRadius = 8f;
        Debug.Log($"[MonsterController] Initialized with OwnerClientId: {OwnerClientId}");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Wait for one frame to ensure ownership is properly set
        StartCoroutine(InitializeAfterOwnership());
    }

    private System.Collections.IEnumerator InitializeAfterOwnership()
    {
        // Wait for one frame to ensure ownership is properly set
        yield return null;
        isInitialized = true;
        Debug.Log($"[MonsterController] (ID: {NetworkObjectId}) Ownership confirmed: {OwnerClientId}");
    }

    protected override void Update()
    {
        if (!isInitialized) return;
        base.Update();
    }

    protected override void FindNewTarget()
    {
        if (!isInitialized) return;
        Debug.Log($"[MonsterController] (ID: {NetworkObjectId}) Finding new target. Current Owner: {OwnerClientId}");
        base.FindNewTarget();
    }

    protected override void HandleTargeting()
    {
        if (!isInitialized) return;
        if (currentTarget != null)
        {
            Debug.Log($"[MonsterController] (ID: {NetworkObjectId}) Handling target: {currentTarget.name}, Unit Owner: {OwnerClientId}, Target Owner: {currentTarget.GetComponent<NetworkBehaviour>()?.OwnerClientId}");
        }
        base.HandleTargeting();
    }
}