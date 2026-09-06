using UnityEngine;
using Unity.Netcode;
using System.Collections;

[CreateAssetMenu(fileName = "LightningBallEffect", menuName = "Cards/Effects/LightningBall")]
public class LightningBallEffect : CardEffect
{
    [Header("Lightning Ball Settings")]
    public GameObject lightningBallPrefab;
    public GameObject ballPrefab;
    public float originalSpeed; // Reference to spawn the normal ball back
    public float lightningSpeed = 50f; // Increased speed for lightning effect
    public float stopDuration = 0.5f; // Duration to stop before despawning

    public override bool RequiresPlacement() => false;

    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
        if (!GameManager.Instance.IsServer) return;

        BallController currentBall = BallController.Instance;
        if (currentBall == null)
        {
            Debug.LogWarning("[LightningBallEffect] BallController.Instance not found, cannot create lightning ball.");
            return;
        }

        if (lightningBallPrefab == null)
        {
            Debug.LogWarning("[LightningBallEffect] Lightning ball prefab is not assigned, cannot create effect.");
            return;
        }

        Debug.Log($"[LightningBallEffect] Lightning Ball activated by player {playerId} at level {cardLevel}");

        // Get current ball state
        Vector3 ballPosition = currentBall.transform.position;
        Quaternion ballRotation = currentBall.transform.rotation;
        Vector3 ballVelocity = currentBall.GetVelocity();
        originalSpeed = currentBall.GetCurrentSpeed();

        // Check if lightning ball prefab has NetworkObject
        NetworkObject lightningBallNetObj = lightningBallPrefab.GetComponent<NetworkObject>();
        if (lightningBallNetObj == null)
        {
            Debug.LogError("[LightningBallEffect] Lightning ball prefab missing NetworkObject component!");
            return;
        }

        // Instantiate the lightning ball prefab
        GameObject lightningBall = Instantiate(lightningBallPrefab, ballPosition, ballRotation);
        Debug.Log($"[LightningBallEffect] Lightning ball instantiated: {lightningBall.name}, position: {lightningBall.transform.position}");

        // Configure lightning behavior settings
        LightningBallBehavior lightningBehavior = lightningBall.GetComponent<LightningBallBehavior>();
        if (lightningBehavior != null)
        {
            // Get damage from card data
            int damage = cardData != null ? cardData.GetDamageAtLevel(cardLevel) : lightningBehavior.damagePerHit;

            lightningBehavior.SetBallPrefab(ballPrefab);
            lightningBehavior.SetLightningEffect(this);
            lightningBehavior.SetCardOwnerPlayerId(playerId);
            lightningBehavior.damagePerHit = damage;
            lightningBehavior.SetSpeed(lightningSpeed);
            lightningBehavior.SetDirection(ballVelocity.normalized);
            Debug.Log($"[LightningBallEffect] Lightning ball configured with speed {lightningSpeed}, damage {damage}");

            // Notify clients about the lightning ball spawned so they can reference it
        }
        else
        {
            Debug.LogWarning("[LightningBallEffect] LightningBallBehavior component not found on lightning ball!");
        }

        // Get the NetworkObject from the instantiated lightning ball and spawn it
        NetworkObject spawnedNetObj = lightningBall.GetComponent<NetworkObject>();
        if (spawnedNetObj != null)
        {
            spawnedNetObj.Spawn();
            Debug.Log($"[LightningBallEffect] Lightning ball network spawned at {ballPosition}");
        }
        else
        {
            Debug.LogError("[LightningBallEffect] Spawned lightning ball missing NetworkObject component!");
            Destroy(lightningBall);
            return;
        }



        // Destroy the original ball - lightning ball will spawn it back when done
        NetworkObject originalBallNetObj = currentBall.GetComponent<NetworkObject>();
        if (originalBallNetObj != null && originalBallNetObj.IsSpawned)
        {
            originalBallNetObj.Despawn();
            Debug.Log("[LightningBallEffect] Original ball despawned from network");
        }
        else
        {
            Destroy(currentBall.gameObject);
            Debug.Log("[LightningBallEffect] Original ball destroyed");
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        Debug.Log($"[LightningBallEffect] Visual effect for Lightning Ball activation by player {playerId}.");
    }

    /// <summary>
    /// Spawns the normal ball after the lightning ball effect completes.
    /// </summary>
    public void SpawnNormalBallAfterLightning(Vector3 ballPosition, Vector3 ballDirection, float ballSpeed)
    {
        // Use coroutine to wait for lightning ball to despawn
        GameManager.Instance.StartCoroutine(WaitAndSpawnNormalBall(ballPosition, ballDirection, ballSpeed));
    }

    private System.Collections.IEnumerator WaitAndSpawnNormalBall(Vector3 ballPosition, Vector3 ballDirection, float ballSpeed)
    {
        // Wait until lightning ball is despawned (Instance is null)
        while (BallController.Instance != null)
        {
            yield return null;
        }

        if (ballPrefab == null)
        {
            Debug.LogWarning("[LightningBallEffect] Ball prefab is null, cannot spawn normal ball!");
            yield break;
        }

        Debug.Log("[LightningBallEffect] Spawning normal ball after lightning effect completes");

        // Instantiate the ball prefab
        GameObject newBall = Instantiate(ballPrefab, ballPosition, Quaternion.identity);

        // Get NetworkObject and spawn it
        NetworkObject ballNetObj = newBall.GetComponent<NetworkObject>();
        if (ballNetObj != null)
        {
            ballNetObj.Spawn();
            Debug.Log($"[LightningBallEffect] Normal ball spawned at {ballPosition}");

            // Configure the spawned ball with current state
            BallController ballController = newBall.GetComponent<BallController>();
            if (ballController != null)
            {
                ballController.SetSpeed(originalSpeed);
                ballController.SetDirection(ballDirection);
                ballController.MoveBallManually();
                Debug.Log($"[LightningBallEffect] Normal ball configured with speed {originalSpeed} and direction {ballDirection}");
            }
        }
        else
        {
            Debug.LogError("[LightningBallEffect] Spawned ball missing NetworkObject component!");
            Destroy(newBall);
        }
    }
}
