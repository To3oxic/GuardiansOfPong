using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class XodGuardianController : GuardiansController
{
    [Header("Xod Spawning Settings")]
    public GameObject unitToSpawnPrefab;
    public float spawnInterval = 10f;
    private float nextSpawnTime = 0f;
    private PaddleController ownerPaddle;

    public override void OnNetworkSpawn()
    {
        // Don't call base.OnNetworkSpawn() as we override the entire behavior
        animator = GetComponent<Animator>();

        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Find the owner's paddle
        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
        foreach (PaddleController paddle in allPaddles)
        {
            if (paddle.OwnerClientId == OwnerClientId)
            {
                ownerPaddle = paddle;
                break;
            }
        }

        if (ownerPaddle == null)
        {
            Debug.LogWarning("[XodGuardianController] Could not find owner paddle!");
        }

        nextSpawnTime = Time.time + spawnInterval;
    }

    protected override void Update()
    {
        if (!IsServer) return;

        // Xod spawns units instead of attacking
        if (Time.time >= nextSpawnTime)
        {
            SpawnUnit();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void SpawnUnit()
    {
        if (unitToSpawnPrefab == null)
        {
            Debug.LogWarning("[XodGuardianController] unitToSpawnPrefab is not assigned!");
            return;
        }

        if (ownerPaddle == null)
        {
            Debug.LogWarning("[XodGuardianController] Owner paddle is null, cannot spawn unit!");
            return;
        }

        // Calculate spawn position in front of the paddle
        Vector3 spawnPosition = ownerPaddle.transform.position;

        if (ownerPaddle.transform.position.z > 0)
        {
            spawnPosition.z = ownerPaddle.transform.position.z - 2f;
        }
        else
        {
            spawnPosition.z = ownerPaddle.transform.position.z + 2f;
        }

        spawnPosition.y = 0f;

        Debug.Log($"[XodGuardianController] Spawning unit at position: {spawnPosition}");

        // Instantiate the unit
        GameObject unitInstance = Instantiate(unitToSpawnPrefab, spawnPosition, Quaternion.identity);

        // Network spawn with ownership
        NetworkObject unitNetObj = unitInstance.GetComponent<NetworkObject>();
        if (unitNetObj != null)
        {
            unitNetObj.SpawnWithOwnership(OwnerClientId);
            Debug.Log($"[XodGuardianController] Unit spawned and networked for player {OwnerClientId}");
        }
        else
        {
            Debug.LogWarning("[XodGuardianController] Spawned unit is missing NetworkObject component!");
            Destroy(unitInstance);
        }
    }
}
