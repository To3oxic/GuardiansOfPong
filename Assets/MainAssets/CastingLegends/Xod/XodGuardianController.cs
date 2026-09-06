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
        CaptureHome(); // pin spawn pose in case a summon animation is wired later

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
            return;
        }

        if (ownerPaddle == null)
        {
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

        // Instantiate the unit
        GameObject unitInstance = Instantiate(unitToSpawnPrefab, spawnPosition, Quaternion.identity);

        // Network spawn with ownership
        NetworkObject unitNetObj = unitInstance.GetComponent<NetworkObject>();
        if (unitNetObj != null)
        {
            unitNetObj.SpawnWithOwnership(OwnerClientId);
        }
        else
        {
            Destroy(unitInstance);
        }
    }
}
