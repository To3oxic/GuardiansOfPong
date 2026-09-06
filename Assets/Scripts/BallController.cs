using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Mathematics;

public class BallController : NetworkBehaviour
{
    [Header("Ball Settings")]
    public float speed = 8f;
    public float baseDamage = 10f;
    public NetworkVariable<float> networkSpeed = new NetworkVariable<float>(8f);
    public float rotationSpeed = 360f;
    private Vector3 launchDirection;

    [Header("Particle Systems")]
    public ParticleSystem dropParticleSystem;

    [Header("Audio Settings")]
    public AudioSource rollingAudioSource;
    public AudioClip rollingBallClip;
    public AudioClip collisionClip;
    public AudioClip goalScoredClip;

    [Header("Audio Configuration")]
    public float minRollingVolume = 0.3f;
    public float maxRollingVolume = 0.8f;
    public float minRollingPitch = 0.8f;
    public float maxRollingPitch = 1.2f;
    public float movementThreshold = 0.1f;

    private AudioSource impactAudioSource;
    public float baseSpeed;

    public static BallController Instance { get; private set; }

    private float bounceCooldown = 0.05f;
    private float lastBounceTime = -1f;
    private float spawnCollisionCooldown = 0.1f;
    private float lastSpawnTime = -1f;

    public TrailRenderer trailRenderer;
    private bool isAnimatingLaunch = false;

    public override void OnNetworkSpawn()
    {
        trailRenderer = GetComponent<TrailRenderer>();

        InitializeAudio();
        baseSpeed = speed;

        if (IsServer)
        {
            networkSpeed.Value = speed;

        }

        Instance = this;
        lastSpawnTime = Time.time;


    }

    public void InitializeAudio()
    {
        Debug.Log("[BallController] Initializing audio system...");

        // Setup rolling audio source if not assigned
        if (rollingAudioSource == null)
        {
            rollingAudioSource = gameObject.AddComponent<AudioSource>();
            Debug.Log("[BallController] Created new AudioSource for rolling sound");
        }

        // Configure rolling audio source
        rollingAudioSource.clip = rollingBallClip;
        rollingAudioSource.loop = true;
        rollingAudioSource.playOnAwake = false;
        rollingAudioSource.volume = minRollingVolume;
        rollingAudioSource.pitch = minRollingPitch;

        // Create separate audio source for impact sounds
        impactAudioSource = gameObject.AddComponent<AudioSource>();
        impactAudioSource.loop = false;
        impactAudioSource.playOnAwake = false;
        impactAudioSource.volume = 0.7f;

        Debug.Log($"[BallController] Audio system initialized - Rolling clip: {(rollingBallClip != null ? "assigned" : "MISSING")}, Collision clip: {(collisionClip != null ? "assigned" : "MISSING")}, Goal clip: {(goalScoredClip != null ? "assigned" : "MISSING")}");
    }

    public void LaunchBall()
    {
        // Setup trigger collider for collision detection
        Collider ballCollider = GetComponent<Collider>();
        if (ballCollider != null)
        {
            ballCollider.isTrigger = true;
        }

        // Choose a random angle between 45° and 80°
        float angleInDegrees = UnityEngine.Random.Range(45f, 80f);
        float angleInRadians = angleInDegrees * Mathf.Deg2Rad;

        // Convert angle to direction vector in XZ plane
        float x = Mathf.Cos(angleInRadians);
        float z = Mathf.Sin(angleInRadians);
        Vector3 direction = new Vector3(x, 0f, z); // Remove Y component for pure 2D movement

        // Randomly flip X and Z to get all 4 quadrants
        if (UnityEngine.Random.value < 0.5f) direction.x *= -1f;
        if (UnityEngine.Random.value < 0.5f) direction.z *= -1f;

        launchDirection = direction.normalized;

        // Calculate start position on circle: x^2 + z^2 = 50
        // Point is in the direction of launchDirection
        float m = launchDirection.z / launchDirection.x;
        x = Mathf.Sqrt(2500f / (1 + m * m));
        z = m * x;
        x = Mathf.Abs(x);
        z = Mathf.Abs(z);
        z = launchDirection.z > 0 ? -z : z;
        x = launchDirection.x > 0 ? -x : x;
        float trajectoryHeight = 20f;
        Vector3 trajectoryStartPos = new Vector3(
            x,
            trajectoryHeight,
            z
        );

        // Reset ball rotation to neutral
        transform.rotation = Quaternion.identity;

        // Tell clients to play the animation
        PlayLaunchAnimationClientRpc(trajectoryStartPos);

        // Server waits 1 second for animation, then starts moving ball
        StartCoroutine(ServerLaunchDelay());

        Debug.Log($"[BallController] Ball launch initiated at angle {angleInDegrees}° -> {launchDirection}");
    }

    /// <summary>
    /// Server waits for animation to complete before starting movement
    /// </summary>
    private IEnumerator ServerLaunchDelay()
    {
        isAnimatingLaunch = true;
        yield return new WaitForSeconds(0.75f);
        isAnimatingLaunch = false;

        // Sync direction and speed to clients for normal gameplay
        SyncBallStateOnCollisionClientRpc(launchDirection, speed);

        Debug.Log($"[BallController] Server starting normal movement with direction {launchDirection}");
    }

    /// <summary>
    /// RPC to tell clients to play the launch animation
    /// </summary>
    [ClientRpc]
    private void PlayLaunchAnimationClientRpc(Vector3 trajectoryStartPos)
    {
        if (IsServer) return; // Only clients play the animation

        StartCoroutine(ClientPlayLaunchAnimation(trajectoryStartPos));
    }

    /// <summary>
    /// Clients play the launch animation locally
    /// </summary>
    private IEnumerator ClientPlayLaunchAnimation(Vector3 trajectoryStartPos)
    {
        // Set starting position for animation
        transform.position = trajectoryStartPos;
        transform.localScale = new Vector3(7f, 7f, 7f);

        Debug.Log($"[BallController] Client playing launch animation from {trajectoryStartPos}");

        // Play the parabolic trajectory animation
        yield return StartCoroutine(MoveWithParabolicTrajectory(0.75f));

        Debug.Log($"[BallController] Client launch animation complete");
    }

    public void MoveBallManually()
    {
        // Manual movement with position-based collision detection
        Vector3 movement = launchDirection * speed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;

        // Get wall boundaries from GameManager
        float leftWallX = GameManager.Instance != null ? GameManager.Instance.LeftWallX : -25f;
        float rightWallX = GameManager.Instance != null ? GameManager.Instance.RightWallX : 25f;

        // Check for wall collisions (X boundaries with ball radius consideration)
        float ballRadius = 1.5f;
        if (newPosition.x > (rightWallX - ballRadius) || newPosition.x < (leftWallX + ballRadius))
        {
            // Check bounce cooldown to prevent multiple rapid bounces
            if (Time.time - lastBounceTime < bounceCooldown)
            {
                // Still apply position clamping but don't bounce
                newPosition.x = Mathf.Clamp(newPosition.x, leftWallX + ballRadius, rightWallX - ballRadius);
                transform.position = newPosition;
                return;
            }

            // Wall collision detected - bouncing

            // Bounce off wall (negate X direction)
            launchDirection.x = -launchDirection.x;
            launchDirection = launchDirection.normalized;

            // Increase speed and sync
            if (speed < 30) speed += 5f;
            else if (speed < 50) speed += 2f;
            else if (speed < 60) speed += 1f;
            networkSpeed.Value = speed;

            // Clamp position to stay within bounds
            newPosition.x = Mathf.Clamp(newPosition.x, leftWallX + ballRadius, rightWallX - ballRadius);

            PlayCollisionSoundClientRpc();
            lastBounceTime = Time.time;

            // Sync direction and speed to clients after wall collision
            SyncBallStateOnCollisionClientRpc(launchDirection, speed);
        }

        // Check for goal collisions using GameManager goal positions
        float player1GoalZ = GameManager.Instance != null ? GameManager.Instance.Player1GoalZ : -40f;
        float player2GoalZ = GameManager.Instance != null ? GameManager.Instance.Player2GoalZ : 40f;

        if (newPosition.z > player2GoalZ || newPosition.z < player1GoalZ)
        {
            if (newPosition.z > player2GoalZ)
            {
                PlayGoalSoundClientRpc();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OpponentLosesPoint();
                    // Destroy current ball before calling scoring
                    DestroyCurrentBall();

                }
            }
            else // newPosition.z < player1GoalZ
            {
                PlayGoalSoundClientRpc();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.PlayerLosesPoint();
                    // Destroy current ball before calling scoring
                    DestroyCurrentBall();

                }
            }
            return; // Don't update position, let GameManager.ResetBall() handle it
        }

        // Update position if no collisions
        transform.position = newPosition;

        // Update ball rotation to simulate rolling
        UpdateBallRotation();
    }

    private void UpdateBallRotation()
    {
        // Calculate rotation based on movement direction and speed
        // Roll the ball around the axis perpendicular to movement direction
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, launchDirection); // Swapped order to fix direction

        // Make rotation directly proportional to speed - slow when speed is low, fast when speed is high
        float baseRotationSpeed = 30f; // Base degrees per second at speed = 1 (reduced from 50f)
        float rotationAmount = speed * baseRotationSpeed * Time.deltaTime * 2;

        // Only rotate if we're actually moving
        if (launchDirection.magnitude > 0.1f && speed > 0.1f)
        {
            transform.Rotate(rotationAxis, rotationAmount, Space.World);

            // Debug rotation for testing
            if (Time.frameCount % 120 == 0) // Every 2 seconds
            {
                DebugHelper.Log($"[BallController] Speed: {speed:F1}, Rotation Rate: {rotationAmount:F2}°/frame (slower rotation)");
            }
        }
    }



    // NEW TRIGGER-BASED COLLISION DETECTION FOR CUSTOM PHYSICS
    public void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        // Disable collisions for 0.1s after spawn
        if (Time.time - lastSpawnTime < spawnCollisionCooldown)
            return;

        if (Time.time - lastBounceTime < bounceCooldown)
            return;

        lastBounceTime = Time.time;

        // Increase speed with each bounce
        if (speed < 30) speed += 5f;
        else if (speed < 50) speed += 2f;
        else if (speed < 60) speed += 1f;

        // Sync speed to clients
        networkSpeed.Value = speed;

        Debug.Log($"[BallController] Trigger collision with {other.name}, tag: '{other.tag}'");

        // Handle paddle collision (trigger-based)
        if (other.CompareTag("Paddle"))
        {
            Vector3 paddleCenter = other.transform.position;
            Vector3 ballPos = transform.position;

            float offset = ballPos.x - paddleCenter.x;
            float paddleWidth = other.bounds.size.x;

            // Normalize the offset (-1 to 1)
            float normalizedOffset = Mathf.Clamp(offset / (paddleWidth / 2f), -1f, 1f);

            // Calculate bounce angle range (in degrees)
            float bounceAngle = normalizedOffset * 60f; // between -60 and +60 degrees

            // Determine incoming direction
            bool isComingFromTop = launchDirection.z < 0;

            float angleInRad = bounceAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(angleInRad);
            float z = Mathf.Cos(angleInRad);

            // Always reflect vertically
            z = isComingFromTop ? 1f : -1f;

            launchDirection = new Vector3(x, 0f, z).normalized;

            PlayCollisionSoundClientRpc();
            Debug.Log($"[BallController] Paddle bounce at angle {bounceAngle}° -> {launchDirection}");

            // Apply damage to paddle
            PaddleController paddle = other.GetComponent<PaddleController>();
            if (paddle != null)
            {
                int calculatedDamage = CalculateDamage();
                paddle.ReduceHP(calculatedDamage);
            }

            // Sync direction and speed to clients after paddle collision
            SyncBallStateOnCollisionClientRpc(launchDirection, speed);
        }
        // Handle units with BaseUnitController (soldiers, monsters, etc.)
        else if (other.TryGetComponent<BaseUnitController>(out var unit))
        {
            int calculatedDamage = CalculateDamage();
            int unitHP = unit.GetCurrentHP();

            // Check if damage would kill the unit
            if (unitHP <= calculatedDamage)
            {
                // Would kill - penetrate through (damage but keep moving)
                unit.TakeDamage(calculatedDamage);

                // Slow down the ball (penetration resistance)
                speed *= 0.75f; // Reduce speed by 25%
                networkSpeed.Value = speed;

                // Keep same direction (no bounce)
                PlayCollisionSoundClientRpc();
                Debug.Log($"[BallController] Ball penetrated through {unit.name} (had {unitHP} HP, took {calculatedDamage} damage), slowed to {speed:F1}");

                // Sync state to clients
                SyncBallStateOnCollisionClientRpc(launchDirection, speed);
            }
            else
            {
                // Would not kill - bounce off the unit
                unit.TakeDamage(calculatedDamage);

                Vector3 ballPos = transform.position;
                Vector3 unitPos = other.transform.position;

                // Determine which face of the cube was hit
                if (Mathf.Abs(ballPos.x - unitPos.x) > Mathf.Abs(ballPos.z - unitPos.z))
                {
                    launchDirection.x = -launchDirection.x; // Hit from side
                }
                else
                {
                    launchDirection.z = -launchDirection.z; // Hit from top/bottom
                }

                launchDirection = launchDirection.normalized;
                PlayCollisionSoundClientRpc();
                Debug.Log($"[BallController] Ball bounced off {unit.name} (had {unitHP} HP, took {calculatedDamage} damage) - New direction: {launchDirection}");

                // Sync state to clients after bounce
                SyncBallStateOnCollisionClientRpc(launchDirection, speed);
            }
        }
        // Handle legend stone collision (instant defeat)
        else if (other.TryGetComponent<LegendStoneController>(out var legendStone))
        {
            HandleLegendStoneCollision(legendStone);
        }
        // Handle placeable objects (buildings, structures, etc.)
        else if (other.TryGetComponent<IPlaceable>(out var placeable))
        {
            int calculatedDamage = CalculateDamage() * 3;
            placeable.TakeDamage(calculatedDamage);

            // Simple bounce off placeables
            Vector3 ballPos = transform.position;
            Vector3 placeablePos = other.transform.position;

            if (Mathf.Abs(ballPos.x - placeablePos.x) > Mathf.Abs(ballPos.z - placeablePos.z))
            {
                launchDirection.x = -launchDirection.x; // Hit from side
            }
            else
            {
                launchDirection.z = -launchDirection.z; // Hit from top/bottom
            }

            launchDirection = launchDirection.normalized;
            PlayCollisionSoundClientRpc();

            Debug.Log($"[BallController] Placeable bounce - New direction: {launchDirection}");

            // Sync direction and speed to clients after placeable collision
            SyncBallStateOnCollisionClientRpc(launchDirection, speed);
        }
        else
        {
            Debug.Log($"[BallController] Trigger collision with '{other.name}' tag '{other.tag}' - no special handling needed (walls and goals handled by position detection)");
        }
    }

    public int CalculateDamage()
    {
        return Mathf.RoundToInt(baseDamage * (speed / 10));
    }

    private void HandleLegendStoneCollision(LegendStoneController legendStone)
    {
        if (!IsServer) return;

        ulong associatedPlayerId = legendStone.GetAssociatedPlayerId();

        Debug.Log($"[BallController] Hit legend stone for player {associatedPlayerId} (OwnerClientId: {legendStone.OwnerClientId}) on server.");

        if (GameManager.Instance != null)
        {
            Debug.Log($"[BallController] *** ABOUT TO CALL TriggerLegendStoneDefeat for player {associatedPlayerId} ***");
            GameManager.Instance.TriggerLegendStoneDefeat(associatedPlayerId);
            Debug.Log($"[BallController] *** FINISHED CALLING TriggerLegendStoneDefeat for player {associatedPlayerId} ***");
        }
        else
        {
            Debug.LogError("[BallController] GameManager.Instance is null. Cannot process legend stone defeat.");
        }
    }

    public void ResetToCenter()
    {
        float resetY = 3f;

        trailRenderer.Clear();
        trailRenderer.emitting = false;
        transform.position = new Vector3(0f, resetY, 0f);

        // Launch angle logic
        float angleInDegrees = UnityEngine.Random.Range(45f, 80f);
        float angleInRadians = angleInDegrees * Mathf.Deg2Rad;

        float x = Mathf.Cos(angleInRadians);
        float z = Mathf.Sin(angleInRadians);
        Vector3 direction = new Vector3(x, 0f, z);

        if (UnityEngine.Random.value < 0.5f) direction.x *= -1f;
        if (UnityEngine.Random.value < 0.5f) direction.z *= -1f;

        launchDirection = direction.normalized;
        speed = 15f;

        transform.rotation = Quaternion.identity;

        networkSpeed.Value = speed;

        Collider ballCollider = GetComponent<Collider>();
        if (ballCollider != null)
        {
            ballCollider.isTrigger = true;
        }

        trailRenderer.emitting = true;
    }




    void Update()
    {
        if (isAnimatingLaunch)
            return;
        if (IsServer)
        {
            MoveBallManually();
        }
        else if (IsClient)
        {
            // Clients simulate ball movement locally
            SimulateBallMovementLocally();
            UpdateBallRotation();
            UpdateRollingAudio();
        }

        // Host: do both server and client updates

    }

    private void SimulateBallMovementLocally()
    {
        // Clients move the ball locally based on last synced direction and speed
        Vector3 movement = launchDirection * speed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;

        // Get wall boundaries from GameManager
        float leftWallX = GameManager.Instance != null ? GameManager.Instance.LeftWallX : -25f;
        float rightWallX = GameManager.Instance != null ? GameManager.Instance.RightWallX : 25f;

        // Clamp to boundaries (clients don't bounce, server handles that)
        float ballRadius = 1.5f;
        newPosition.x = Mathf.Clamp(newPosition.x, leftWallX + ballRadius, rightWallX - ballRadius);

        transform.position = newPosition;
    }

    private void UpdateRollingAudio()
    {
        if (rollingAudioSource == null)
        {
            return;
        }

        // Get current velocity magnitude (simulated)
        float currentVelocity = networkSpeed.Value; // Use networked speed for clients

        // Debug velocity and threshold check
        if (Time.frameCount % 60 == 0) // Debug every second
        {
            Debug.Log($"[BallController] Rolling audio check: Velocity={currentVelocity:F2}, Threshold={movementThreshold}, Moving={currentVelocity > movementThreshold}, AudioClip={(rollingBallClip != null ? "assigned" : "missing")}");
        }

        // Check if ball is moving above threshold
        if (currentVelocity > movementThreshold)
        {
            // Ball is moving - play rolling sound if not already playing
            if (!rollingAudioSource.isPlaying)
            {
                if (rollingBallClip != null)
                {
                    rollingAudioSource.Play();
                    Debug.Log($"[BallController] Started rolling sound - Velocity: {currentVelocity:F2}");
                }
                else
                {
                    Debug.LogWarning("[BallController] Cannot play rolling sound - rollingBallClip is not assigned!");
                }
            }

            // Calculate speed ratio (current speed vs base speed)
            float speedRatio = Mathf.Clamp01(currentVelocity / baseSpeed);

            // Adjust volume based on speed (faster = louder)
            rollingAudioSource.volume = Mathf.Lerp(minRollingVolume, maxRollingVolume, speedRatio);

            // Adjust pitch based on speed (faster = higher pitch, clamped to max 1.2x)
            float targetPitch = Mathf.Lerp(minRollingPitch, maxRollingPitch, speedRatio);
            rollingAudioSource.pitch = Mathf.Clamp(targetPitch, minRollingPitch, maxRollingPitch);
        }
        else
        {
            // Ball is not moving - stop rolling sound
            if (rollingAudioSource.isPlaying)
            {
                rollingAudioSource.Stop();
                Debug.Log($"[BallController] Stopped rolling sound - Velocity too low: {currentVelocity:F2}");
            }
        }
    }

    [ClientRpc]
    private void PlayCollisionSoundClientRpc()
    {
        // Only play audio on clients, not on server
        if (!IsClient) return;

        if (impactAudioSource != null && collisionClip != null)
        {
            impactAudioSource.clip = collisionClip;
            impactAudioSource.Play();
            Debug.Log("[BallController] Playing collision sound on client");
        }
    }

    [ClientRpc]
    private void PlayGoalSoundClientRpc()
    {
        // Only play audio on clients, not on server
        if (!IsClient) return;

        if (impactAudioSource != null && goalScoredClip != null)
        {
            // Stop rolling sound when goal is scored
            if (rollingAudioSource != null && rollingAudioSource.isPlaying)
            {
                rollingAudioSource.Stop();
            }

            impactAudioSource.clip = goalScoredClip;
            impactAudioSource.Play();
            Debug.Log("[BallController] Playing goal sound on client");
        }
    }

    public void StopAllAudio()
    {
        // Only stop audio on clients, not on server
        if (!IsClient) return;

        if (rollingAudioSource != null && rollingAudioSource.isPlaying)
        {
            rollingAudioSource.Stop();
        }
        if (impactAudioSource != null && impactAudioSource.isPlaying)
        {
            impactAudioSource.Stop();
        }
    }

    /// <summary>
    /// Sync ball state (speed and direction) to all clients after collision
    /// Called when ball hits paddle, walls, or other objects
    /// </summary>
    [ClientRpc]
    public void SyncBallStateOnCollisionClientRpc(Vector3 newDirection, float newSpeed)
    {
        if (IsServer) return; // Server doesn't need to update from its own RPC

        // Update client's ball state to match server after collision
        launchDirection = newDirection.normalized;
        speed = newSpeed;

        DebugHelper.Log($"[BallController] Synced ball state after collision - Direction: {launchDirection}, Speed: {speed:F1}");
    }

    private IEnumerator EnableTrailNextFrame()
    {
        yield return null;
        trailRenderer.emitting = true;
    }


    public void IncreaseSpeed(float amount)
    {
        if (!IsServer) return;

        speed += amount;
        networkSpeed.Value = speed;
        Debug.Log($"Ball speed increased to {speed}");
    }

    public void DecreaseSpeed(float amount)
    {
        if (!IsServer) return;

        speed = Mathf.Max(5f, speed - amount);
        networkSpeed.Value = speed;
        Debug.Log($"Ball speed decreased to {speed}");
    }

    public Vector3 GetVelocity()
    {
        return launchDirection * speed;
    }

    public float GetCurrentSpeed()
    {
        return this.speed;
    }

    public void SetDirection(Vector3 newDirection)
    {
        if (!IsServer) return;
        launchDirection = newDirection.normalized;
        Debug.Log($"[BallController] Direction set to {launchDirection}");
    }

    public void SetSpeed(float newSpeed)
    {
        if (!IsServer) return;
        speed = Mathf.Max(0f, newSpeed);
        networkSpeed.Value = speed;
        Debug.Log($"[BallController] Speed set to {speed}");
    }

    public void SetLaunchDirection(Vector3 position, Vector3 targetPosition)
    {
        if (!IsServer) return;
        Vector3 direction = (targetPosition - position).normalized;
        launchDirection = direction;
        Debug.Log($"[BallController] Launch direction set towards {targetPosition}");
    }

    public void DestroyCurrentBall()
    {
        NetworkObject ballNetObj = GetComponent<NetworkObject>();
        if (ballNetObj != null && ballNetObj.IsSpawned)
        {
            ballNetObj.Despawn();
            Debug.Log("[BallController] Ball despawned from network");
        }
        else
        {
            Destroy(gameObject);
            Debug.Log("[BallController] Ball destroyed");
        }
    }

    /// <summary>
    /// Ball shot from outside map, falls down exponentially as it approaches center
    /// Simulates being shot from far away with gravity pulling it down
    /// </summary>
    private IEnumerator ShotFromOutsideMap(Vector3 startPosition, float duration = 4f)
    {
        // Start position: far outside the map (high up)

        transform.position = startPosition;
        float elapsedTime = 0f;

        Debug.Log($"[BallController] Shot from outside map at {startPosition}");

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            // Manual horizontal movement from start to center
            float posX = startPosition.x - (startPosition.x * t);  // 60 -> 0
            float posZ = startPosition.z - (startPosition.z * t);  // 60 -> 0

            // Vertical fall: exponential drop as it approaches center
            // y = e^(-x) where x is normalized progress, creates steep drop at end
            float fallFactor = Mathf.Exp(-t * 3f);  // Negative exponent for decay
            float posY = startPosition.y * fallFactor;

            Vector3 position = new Vector3(posX, posY, posZ);
            transform.position = position;

            yield return null;
        }

        // Ensure final position is at center ground level
        transform.position = new Vector3(0f, 3f, 0f);

        // Activate particle system
        if (dropParticleSystem != null)
        {
            dropParticleSystem.Play();
            Debug.Log("[BallController] Drop particle system activated");
        }

        Debug.Log("[BallController] Shot complete, ball landed at center");
    }

    /// <summary>
    /// Moves ball manually using parabolic trajectory: y = abs(x^2) + 1.5
    /// Endpoint is always (0, 0, 0)
    /// </summary>
    private IEnumerator MoveWithParabolicTrajectory(float duration = 1f)
    {
        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        Debug.Log($"[BallController] Starting parabolic trajectory from {startPosition} to (0, 0, 0)");

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            // Manual movement from start position to end position (0, 0, 0)
            float posX = startPosition.x - (startPosition.x * t);
            float posZ = startPosition.z - (startPosition.z * t);

            // Apply parabolic curve: y = abs(x^2) + 1.5
            float x = posX;
            float parabolaHeight = Mathf.Sqrt(x) * 5 + 1.5f;  // Scale down x^2 for reasonable heights

            Vector3 position = new Vector3(posX, parabolaHeight, posZ);
            Debug.Log($"[BallController] Parabolic position at t={t:F2}: {position}, Height={parabolaHeight:F2}");
            transform.position = position;
            float scale = 7f - (4f * t);  // 7 -> 3
            transform.localScale = new Vector3(scale, scale, scale);

            yield return null;
        }


        // Ensure final position is exactly (0, 0, 0)
        transform.position = new Vector3(0f, 1.5f, 0f);

        // Activate particle system
        if (dropParticleSystem != null)
        {
            dropParticleSystem.Play();
            Debug.Log("[BallController] Drop particle system activated");
        }

        Debug.Log("[BallController] Parabolic trajectory complete, ball at (0, 0, 0)");
    }

    /// <summary>
    /// Test caller for parabolic trajectory
    /// </summary>
    private IEnumerator TestParabolicTrajectory()
    {
        yield return new WaitForSeconds(2f);

        // First trajectory
        transform.position = new Vector3(40f, 15f, 40f);
        Debug.Log("[BallController] Test reset to (40, 15, 40)");
        yield return StartCoroutine(MoveWithParabolicTrajectory(1f));

        // Wait and repeat
        yield return new WaitForSeconds(1f);

        // Second trajectory
        transform.position = new Vector3(40f, 15f, 40f);
        Debug.Log("[BallController] Test reset to (40, 15, 40) - second run");
        yield return StartCoroutine(MoveWithParabolicTrajectory(3f));
    }
}