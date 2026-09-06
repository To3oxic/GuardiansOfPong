using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// LightningBallBehavior inherits from BallController to execute the lightning zigzag pattern.
/// The ball bounces between walls in a calculated pattern, killing anything it hits.
/// </summary>
public class LightningBallBehavior : BallController
{
    [Header("Lightning Settings")]
    public float stopDuration = 0.5f;
    private GameObject ballPrefab = null;
    private LightningBallEffect lightningEffect = null;
    public int damagePerHit = 10;
    private bool hasStartedSequence = false;
    private PaddleController targetPaddle = null;
    private ulong cardOwnerPlayerId = 0;
    private bool isExecutingLightningSequence = false;
    private Vector3 direction;

    public void SetBallPrefab(GameObject prefab)
    {
        ballPrefab = prefab;
    }

    public void SetLightningEffect(LightningBallEffect effect)
    {
        lightningEffect = effect;
    }

    public void SetCardOwnerPlayerId(ulong playerId)
    {
        cardOwnerPlayerId = playerId;
    }

    public override void OnNetworkSpawn()
    {
        trailRenderer = GetComponent<TrailRenderer>();

        InitializeAudio();
        baseSpeed = 300;

        if (IsServer)
        {
            networkSpeed.Value = 300;
        }

        if (IsServer)
        {
            // Start the lightning sequence after a brief delay
            StartCoroutine(ExecuteLightningSequence());
        }
        if (IsClient)
        {
        }
    }

    public void Update()
    {
        // Lightning ball handles its own movement during sequence
        // No network syncing needed during sequence
    }

    private IEnumerator ExecuteLightningSequence()
    {
        if (hasStartedSequence) yield break;
        hasStartedSequence = true;
        isExecutingLightningSequence = true;

        // Wait a frame to ensure everything is initialized
        yield return null;

        // Find the opponent's paddle (the one we're moving towards)
        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);

        // Find the paddle that belongs to the opponent (not the card owner)
        targetPaddle = null;
        foreach (PaddleController paddle in allPaddles)
        {
            if (paddle.OwnerClientId != cardOwnerPlayerId)
            {
                targetPaddle = paddle;
                break;
            }
        }

        if (targetPaddle == null)
        {
            yield break;
        }

        // Calculate total distance from ball to paddle
        float distanceToPaddle = Mathf.Abs(targetPaddle.transform.position.z - transform.position.z);
        float segmentDistance = distanceToPaddle * 2f / 9f;

        yield return new WaitForSeconds(stopDuration);
        // Calculate direction to paddle (positive or negative Z)
        float directionTowardsPaddle = targetPaddle.transform.position.z > transform.position.z ? 1f : -1f;

        // First bounce: move segmentDistance towards the paddle on one wall
        float firstBounceZ = transform.position.z + (segmentDistance * directionTowardsPaddle);
        direction = new Vector3(GetWallX(true), transform.position.y, firstBounceZ);

        yield return StartCoroutine(MoveBallToPosition(new Vector3(GetWallX(true), transform.position.y, firstBounceZ)));

        // Second bounce: move segmentDistance towards the paddle on opposite wall
        float secondBounceZ = firstBounceZ + (segmentDistance * directionTowardsPaddle);
        direction = new Vector3(GetWallX(false), transform.position.y, secondBounceZ);
        yield return StartCoroutine(MoveBallToPosition(new Vector3(GetWallX(false), transform.position.y, secondBounceZ)));

        // Third move: reach currentZ + (remainingDistance * 2/3) towards the paddle
        float thirdTargetZ = secondBounceZ + (segmentDistance * directionTowardsPaddle);
        direction = new Vector3(GetWallX(true), transform.position.y, thirdTargetZ);
        yield return StartCoroutine(MoveBallToPosition(new Vector3(GetWallX(true), transform.position.y, thirdTargetZ)));

        // Stop for half a second
        yield return new WaitForSeconds(stopDuration);

        // Resume movement towards opponent paddle with random angle and original speed
        Vector3 directionToOpponent = (targetPaddle.transform.position - transform.position).normalized;

        // Add random angle variation (±30 degrees)
        float randomAngle = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
        Quaternion rotationOffset = Quaternion.AngleAxis(randomAngle, Vector3.up);
        Vector3 finalDirection = rotationOffset * directionToOpponent;

        SetDirection(finalDirection);

        // Despawn lightning ball and respawn normal ball
        isExecutingLightningSequence = false;
        DespawnAndRespawnNormal();
    }
    [ClientRpc]
    private void ExecuteLightningSequenceCallerClientRPC(Vector3 targetPosition, float time)
    {
        StartCoroutine(MoveClientBallToPosition(targetPosition, time));
    }
    private IEnumerator MoveClientBallToPosition(Vector3 _targetPosition, float _timeToMove)
    {
        Vector3 startPosition = transform.position;

        float timeToMove = _timeToMove;

        float elapsedTime = 0f;
        while (elapsedTime < timeToMove)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / timeToMove);
            transform.position = Vector3.Lerp(startPosition, _targetPosition, t);
            yield return null;
        }

        transform.position = _targetPosition;
    }

    private IEnumerator MoveBallToPosition(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float distance = Vector3.Distance(startPosition, targetPosition);
        float timeToMove = distance / baseSpeed;
        ExecuteLightningSequenceCallerClientRPC(targetPosition, timeToMove);

        float elapsedTime = 0f;
        while (elapsedTime < timeToMove)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / timeToMove);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
    }

    private float GetWallX(bool leftWall)
    {
        if (GameManager.Instance != null)
        {
            return leftWall ? GameManager.Instance.LeftWallX : GameManager.Instance.RightWallX;
        }
        return leftWall ? -25f : 25f;
    }

    private void DespawnAndRespawnNormal()
    {
        Vector3 ballPosition = transform.position;
        Vector3 ballDirection = GetVelocity().normalized;
        float ballSpeed = GetCurrentSpeed();

        // Despawn lightning ball
        if (IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
        else
        {
            Destroy(gameObject);
        }

        // Spawn normal ball through effect
        if (lightningEffect != null)
        {
            lightningEffect.SpawnNormalBallAfterLightning(ballPosition, ballDirection, ballSpeed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Kill anything the lightning ball hits
        PaddleController paddle = other.GetComponent<PaddleController>();
        if (paddle != null)
        {
            paddle.ReduceHP(damagePerHit); // Instant kill
            return;
        }

        IPlaceable placeable = other.GetComponent<IPlaceable>();
        if (placeable != null)
        {
            placeable.TakeDamage(damagePerHit); // Instant kill
            return;
        }
    }
}
