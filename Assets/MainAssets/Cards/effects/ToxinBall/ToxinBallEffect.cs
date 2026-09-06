using UnityEngine;
using Unity.Netcode;

[CreateAssetMenu(fileName = "ToxinBallEffect", menuName = "Cards/Effects/ToxinBall")]
public class ToxinBallEffect : CardEffect
{
    [Header("Toxin Ball Settings")]
    public GameObject toxinBallPrefab;
    public GameObject ballPrefab; // Reference to spawn the normal ball back
    public int damagePerTick = 5;
    public float tickInterval = 1f;
    public float totalDuration = 5f;

    public override bool RequiresPlacement() => false;

    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
        if (!GameManager.Instance.IsServer) return;

        BallController currentBall = BallController.Instance;
        if (currentBall == null)
        {
            Debug.LogWarning("[ToxinBallEffect] BallController.Instance not found, cannot create toxin ball.");
            return;
        }

        if (toxinBallPrefab == null)
        {
            Debug.LogWarning("[ToxinBallEffect] Toxin ball prefab is not assigned, cannot create effect.");
            return;
        }

        Debug.Log($"[ToxinBallEffect] Toxin Ball activated by player {playerId} at level {cardLevel}");

        // Get current ball state
        Vector3 ballPosition = currentBall.transform.position;
        Quaternion ballRotation = currentBall.transform.rotation;
        Vector3 ballVelocity = currentBall.GetVelocity();
        float ballSpeed = currentBall.GetCurrentSpeed();

        // Check if toxin ball prefab has NetworkObject
        NetworkObject toxinBallNetObj = toxinBallPrefab.GetComponent<NetworkObject>();
        if (toxinBallNetObj == null)
        {
            Debug.LogError("[ToxinBallEffect] Toxin ball prefab missing NetworkObject component!");
            return;
        }

        // Instantiate the toxin ball prefab
        GameObject toxinBall = Instantiate(toxinBallPrefab, ballPosition, ballRotation);
        Debug.Log($"[ToxinBallEffect] Toxin ball instantiated: {toxinBall.name}, active: {toxinBall.activeSelf}, position: {toxinBall.transform.position}, scale: {toxinBall.transform.localScale}");

        // Get the NetworkObject from the instantiated toxin ball and spawn it
        NetworkObject spawnedNetObj = toxinBall.GetComponent<NetworkObject>();
        if (spawnedNetObj != null)
        {
            spawnedNetObj.Spawn();
            Debug.Log($"[ToxinBallEffect] Toxin ball network spawned at {ballPosition}");
        }
        else
        {
            Debug.LogError("[ToxinBallEffect] Spawned toxin ball missing NetworkObject component!");
            Destroy(toxinBall);
            return;
        }

        // Configure toxin behavior settings
        ToxinBallBehavior toxinBehavior = toxinBall.GetComponent<ToxinBallBehavior>();
        if (toxinBehavior != null)
        {
            // Get damage and duration from card data
            int damage = cardData != null ? cardData.GetDamageAtLevel(cardLevel) : damagePerTick;
            float duration = cardData != null ? cardData.GetDurationAtLevel(cardLevel) : totalDuration;

            damagePerTick = damage;
            toxinBehavior.tickInterval = tickInterval;
            totalDuration = duration;
            toxinBehavior.SetBallPrefab(ballPrefab);
            toxinBehavior.SetToxinEffect(this);
            toxinBehavior.SetSpeed(ballSpeed);
            toxinBehavior.SetDirection(ballVelocity.normalized);
            toxinBehavior.SyncBallStateOnCollisionClientRpc(ballVelocity.normalized, ballSpeed);
            Debug.Log($"[ToxinBallEffect] Toxin ball configured with speed {ballSpeed}, direction {ballVelocity.normalized}, damage {damage}, and duration {duration}");
        }
        else
        {
            Debug.LogWarning("[ToxinBallEffect] ToxinBallBehavior component not found on toxin ball!");
        }

        // Destroy the original ball - toxin ball will spawn it back when it collides
        NetworkObject originalBallNetObj = currentBall.GetComponent<NetworkObject>();
        if (originalBallNetObj != null && originalBallNetObj.IsSpawned)
        {
            originalBallNetObj.Despawn();
            Debug.Log("[ToxinBallEffect] Original ball despawned from network");
        }
        else
        {
            Destroy(currentBall.gameObject);
            Debug.Log("[ToxinBallEffect] Original ball destroyed");
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        Debug.Log($"[ToxinBallEffect] Visual effect for Toxin Ball activation by player {playerId}.");
    }

    /// <summary>
    /// Applies damage ticks to a target over time. Call this when the toxin ball hits something.
    /// </summary>
    public void ApplyDamageTicks(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[ToxinBallEffect] Target is null, cannot apply damage ticks!");
            return;
        }

        if (!GameManager.Instance.IsServer) return;

        // Start the damage tick coroutine
        GameManager.Instance.StartCoroutine(DamageTick(target));
        Debug.Log($"[ToxinBallEffect] Started damage ticks on {target.name}");
    }

    private System.Collections.IEnumerator DamageTick(GameObject target)
    {
        float elapsedTime = 0f;

        while (elapsedTime < totalDuration && target != null)
        {
            // Apply damage to paddle
            PaddleController paddle = target.GetComponent<PaddleController>();
            if (paddle != null)
            {
                paddle.ReduceHP(damagePerTick);
                Debug.Log($"[ToxinBallEffect] Applied {damagePerTick} toxin damage to paddle {target.name}");
            }

            // Apply damage to placeable
            IPlaceable placeable = target.GetComponent<IPlaceable>();
            if (placeable != null)
            {
                placeable.TakeDamage(damagePerTick);
                Debug.Log($"[ToxinBallEffect] Applied {damagePerTick} toxin damage to placeable {target.name}");
            }

            elapsedTime += tickInterval;
            yield return new WaitForSeconds(tickInterval);
        }

        Debug.Log($"[ToxinBallEffect] Finished applying toxin damage to {target.name}");
    }

    /// <summary>
    /// Spawns the normal ball after the toxin ball collision.
    /// Ensures toxin ball is despawned first to clear the Singleton.
    /// </summary>
    public void SpawnNormalBallAfterToxin(Vector3 ballPosition, Vector3 ballDirection, float ballSpeed, GameObject toxinBall)
    {
        // Use coroutine to wait for toxin ball to despawn
        GameManager.Instance.StartCoroutine(WaitAndSpawnNormalBall(ballPosition, ballDirection, ballSpeed, toxinBall));
    }

    private System.Collections.IEnumerator WaitAndSpawnNormalBall(Vector3 ballPosition, Vector3 ballDirection, float ballSpeed, GameObject toxinBall)
    {
        // Wait until toxin ball is despawned (Instance is null)
        while (BallController.Instance != null)
        {
            yield return null;
        }

        if (ballPrefab == null)
        {
            Debug.LogWarning("[ToxinBallEffect] Ball prefab is null, cannot spawn normal ball!");
            yield break;
        }

        Debug.Log("[ToxinBallEffect] Spawning normal ball after toxin collision");

        // Instantiate the ball prefab
        GameObject newBall = Instantiate(ballPrefab, ballPosition, Quaternion.identity);

        // Get NetworkObject and spawn it
        NetworkObject ballNetObj = newBall.GetComponent<NetworkObject>();
        if (ballNetObj != null)
        {
            ballNetObj.Spawn();
            Debug.Log($"[ToxinBallEffect] Normal ball spawned at {ballPosition}");

            // Configure the spawned ball with current state
            BallController ballController = newBall.GetComponent<BallController>();
            if (ballController != null)
            {
                ballController.SetSpeed(ballSpeed);
                ballController.SetDirection(ballDirection);
                ballController.SyncBallStateOnCollisionClientRpc(ballDirection, ballSpeed);
                Debug.Log($"[ToxinBallEffect] Normal ball configured with speed {ballSpeed} and direction {ballDirection}");
            }
        }
        else
        {
            Debug.LogError("[ToxinBallEffect] Spawned ball missing NetworkObject component!");
            Destroy(newBall);
        }
    }
}