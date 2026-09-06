using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ToxinBallBehavior inherits from BallController to override collision behavior.
/// On collision with a paddle or placeable, it swaps back to the normal ball and applies damage ticks.
/// </summary>
public class ToxinBallBehavior : BallController
{
    [Header("Toxin Settings")]
    public int damagePerTick = 5;
    public float tickInterval = 1f;
    public float totalDuration = 5f;

    private GameObject ballPrefab = null;
    private bool hasCollided = false;
    private ToxinBallEffect toxinEffect = null;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // CRITICAL: Ensure collider is set to trigger for collision detection with custom physics
        Collider ballCollider = GetComponent<Collider>();
        if (ballCollider != null)
        {
            ballCollider.isTrigger = true;
        }
        else
        {
        }
    }

    public void SetBallPrefab(GameObject prefab)
    {
        ballPrefab = prefab;
    }

    public void SetToxinEffect(ToxinBallEffect effect)
    {
        toxinEffect = effect;
    }

    new private void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        if (!IsServer) return;

        Vector3 ballPosition = transform.position;
        Vector3 ballDirection = GetVelocity().normalized;
        float ballSpeed = GetCurrentSpeed();
        if (toxinEffect != null)
        {
            toxinEffect.SpawnNormalBallAfterToxin(ballPosition, ballDirection, ballSpeed, gameObject);
            toxinEffect.ApplyDamageTicks(other.gameObject);
        }
        Destroy(gameObject);
    }
}
