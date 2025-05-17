using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;

public class LightningPathBallEffect : IBallEffect
{
    public BallEffectType Type => BallEffectType.Special;
    public string Source { get; private set; }

    private BallController currentBall;
    private GameManager gameManager;

    private float trailVisibleDuration;
    private float lightningDashDuration;
    // Arena boundaries for Z still needed for goal calculation
    private float arenaMinZ, arenaMaxZ;
    // wallLayerMask is no longer needed as we use fixed X boundaries
    // private LayerMask wallLayerMask; 

    private bool sequenceComplete = false;
    private Coroutine effectCoroutine;

    // Board fixed X boundaries
    private const float wallPositiveX = 25f;
    private const float wallNegativeX = -25f;

    public LightningPathBallEffect(string sourceCardName, float trailDuration, float dashDuration,
                                   float minZ, float maxZ) // Removed LayerMask wallsLayer
    {
        Source = sourceCardName;
        trailVisibleDuration = trailDuration;
        lightningDashDuration = dashDuration;
        arenaMinZ = minZ; // Goal lines / back walls
        arenaMaxZ = maxZ;
        // this.wallLayerMask = wallsLayer; // Removed
    }

    // Renamed from StartEffectSequence to match IBallEffect
    public void AttemptToStartSpecialSequence(BallController ball, GameManager gm)
    {
        if (ball == null || gm == null)
        {
            Debug.LogError("[LightningPathBallEffect] BallController or GameManager is null. Cannot start sequence.");
            sequenceComplete = true;
            return;
        }
        this.currentBall = ball;
        this.gameManager = gm;
        effectCoroutine = ball.StartCoroutine(ExecuteLightningSequenceCoroutine());
    }

    private IEnumerator ExecuteLightningSequenceCoroutine()
    {
        if (currentBall == null || gameManager == null)
        {
            Debug.LogError("[LightningPathBallEffect] Coroutine: Ball or GameManager became null.");
            sequenceComplete = true;
            yield break;
        }

        // Get velocity BEFORE stopping the ball
        Vector3 originalBallVelocity = currentBall.GetVelocity();
        float originalBallSpeed = currentBall.GetCurrentSpeed();
        Debug.Log($"[LPE.ExecuteCoroutine] Captured originalBallVelocity BEFORE stopping: {originalBallVelocity}, originalBallSpeed: {originalBallSpeed}");

        // Clear trail renderer before stopping movement for smooth transition
        TrailRenderer trailRenderer = currentBall.GetComponent<TrailRenderer>();
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = false;
        }

        // Now stop the ball
        currentBall.StopMovementAndClearEffects();
        Debug.Log("[LightningPathBallEffect] Ball stopped. Original Vel: " + originalBallVelocity);

        // Small delay to ensure smooth transition
        yield return new WaitForSeconds(0.05f);

        List<Vector3> pathPoints = CalculateLightningPathWithFixedWalls(currentBall.transform.position, originalBallVelocity);

        // If path calculation fails or results in too few points (e.g., direct dash fallback), ensure we handle it.
        if (pathPoints == null || pathPoints.Count < 2)
        {
            Debug.LogWarning("[LightningPathBallEffect] Main path calculation failed or resulted in a path too short. Attempting direct dash fallback.");
            // No longer need the inner redundant 'if (pathPoints == null || pathPoints.Count < 2)' here.

            float directTargetGoalZ;
            // Prioritize originalBallVelocity.z to determine "forward" if it's not negligible.
            if (Mathf.Abs(originalBallVelocity.z) > 0.001f) // A small threshold for Z-velocity significance
            {
                directTargetGoalZ = (originalBallVelocity.z > 0) ? arenaMaxZ : arenaMinZ;
                Debug.Log($"[LPE Fallback] Using originalBallVelocity.z ({originalBallVelocity.z}) to set directTargetGoalZ to {directTargetGoalZ}");
            }
            else // originalBallVelocity.z is negligible or zero
            {
                // Fallback: determine "forward" based on the ball's current Z position.
                // Assumes positive Z is one player's side, negative Z is the other's, aiming towards opponent's goal line.
                directTargetGoalZ = (currentBall.transform.position.z >= 0f) ? arenaMinZ : arenaMaxZ;
                Debug.Log($"[LPE Fallback] originalBallVelocity.z ({originalBallVelocity.z}) is negligible. Using current ball Z-pos ({currentBall.transform.position.z}) to set directTargetGoalZ to {directTargetGoalZ}");
            }

            float directZDistanceToGoal = Mathf.Abs(directTargetGoalZ - currentBall.transform.position.z);
            float directTravelZ = directZDistanceToGoal * (2f / 3f);

            // Determine the direction of travel towards the goal.
            float travelDirectionSign = Mathf.Sign(directTargetGoalZ - currentBall.transform.position.z);
            if (travelDirectionSign == 0) // If ball is exactly on the determined goal line for Z.
            {
                // Default to move towards the center of the arena or opposite side.
                // If at positive Z goal, move towards negative Z. If at negative Z goal, move towards positive Z.
                travelDirectionSign = (directTargetGoalZ == arenaMaxZ) ? -1f : 1f;
                // If somehow directTargetGoalZ is 0, and ball is at 0, make a choice.
                if (travelDirectionSign == 0) travelDirectionSign = (currentBall.transform.position.z >= 0f) ? -1f : 1f; // Default based on current half
            }

            Vector3 directTargetPoint = new Vector3(
                0f, // X is centered for the direct dash
                currentBall.transform.position.y, // Y remains the same
                currentBall.transform.position.z + travelDirectionSign * directTravelZ
            );

            // Clamp target to be within arena bounds, with a small margin
            directTargetPoint.z = Mathf.Clamp(directTargetPoint.z, arenaMinZ + 0.1f, arenaMaxZ - 0.1f);

            pathPoints = new List<Vector3> { currentBall.transform.position, directTargetPoint };
            Debug.Log($"[LPE Fallback] Calculated direct dash. Start: {pathPoints[0]}, Target: {directTargetPoint}");


            // If still not enough points for a dash even with fallback (e.g. start and target are effectively the same), just resume.
            if (pathPoints.Count < 2 || (pathPoints[1] - pathPoints[0]).sqrMagnitude < 0.1f)
            {
                Debug.LogError("[LightningPathBallEffect] Direct dash target is too close or invalid. Resuming normal movement immediately.");
                Vector3 resumeDirection;
                if (originalBallVelocity.sqrMagnitude > 0.001f)
                {
                    resumeDirection = originalBallVelocity.normalized;
                }
                else
                {
                    // Original velocity was zero. Default to aiming at opponent's goal center.
                    float opponentGoalZResume;
                    // If current Z is positive or zero, opponent goal is minZ. Else, maxZ.
                    // This assumes arenaMinZ is "more negative" than arenaMaxZ if Z=0 is center.
                    if (currentBall.transform.position.z >= 0f) opponentGoalZResume = arenaMinZ;
                    else opponentGoalZResume = arenaMaxZ;

                    Vector3 targetGoalCenter = new Vector3(0, currentBall.transform.position.y, opponentGoalZResume);
                    resumeDirection = (targetGoalCenter - currentBall.transform.position).normalized;

                    if (resumeDirection.sqrMagnitude < 0.001f) // If ball is (somehow) exactly at the targetGoalCenter
                    {
                        // Default to a generic "forward" based on which side of the court it is on.
                        // If on positive Z side (or Z=0), aim towards negative Z (arenaMinZ).
                        // If on negative Z side, aim towards positive Z (arenaMaxZ).
                        resumeDirection = (currentBall.transform.position.z >= 0f) ? Vector3.back : Vector3.forward; // Vector3.back (0,0,-1), Vector3.forward (0,0,1)
                    }
                    Debug.Log($"[LPE Fallback Resume] Original velocity was zero. Defaulting resumeDirection to: {resumeDirection}");
                }
                currentBall.ResumeNormalMovement(originalBallSpeed, resumeDirection);
                sequenceComplete = true;
                yield break;
            }
        }

        gameManager.ShowLightningPathOnClientsClientRpc(pathPoints.ToArray(), trailVisibleDuration);

        yield return new WaitForSeconds(trailVisibleDuration);

        Debug.Log("[LightningPathBallEffect] Executing lightning dash on server.");
        yield return gameManager.StartCoroutine(ExecutePathMovementCoroutine(pathPoints, lightningDashDuration));

        // Use the original ball velocity's direction after the dash.
        Vector3 directionAfterDash;
        if (originalBallVelocity.sqrMagnitude > 0.001f)
        {
            directionAfterDash = originalBallVelocity.normalized;
        }
        else
        {
            // Original velocity was zero. Default to aiming at opponent's goal center.
            float opponentGoalZ;
            if (currentBall.transform.position.z > 0.1f) opponentGoalZ = arenaMinZ;
            else if (currentBall.transform.position.z < -0.1f) opponentGoalZ = arenaMaxZ;
            else opponentGoalZ = arenaMaxZ; // Default if ball near Z=0

            Vector3 targetCenterOfOpponentGoal = new Vector3(0, currentBall.transform.position.y, opponentGoalZ);
            directionAfterDash = (targetCenterOfOpponentGoal - currentBall.transform.position).normalized;
            if (directionAfterDash == Vector3.zero) directionAfterDash = Vector3.forward; // Absolute fallback
            Debug.Log($"[LPE.ExecuteCoroutine] Original velocity was zero. Defaulting directionAfterDash to aim at opponent goal center: {directionAfterDash}");
        }

        Debug.Log($"[LPE.ExecuteCoroutine] BEFORE ResumeNormalMovement - Speed: {originalBallSpeed}, Direction: {directionAfterDash}");
        currentBall.ResumeNormalMovement(originalBallSpeed, directionAfterDash);
        sequenceComplete = true;
        Debug.Log("[LightningPathBallEffect] Sequence complete.");
    }

    private IEnumerator ExecutePathMovementCoroutine(List<Vector3> pathPoints, float totalDuration)
    {
        if (currentBall == null || !currentBall.IsServer || pathPoints == null || pathPoints.Count < 2)
        {
            Debug.LogError("[LightningPathEffect.ExecutePathMovement] Invalid parameters, not server, or ball is null.");
            yield break;
        }

        Rigidbody ballRb = currentBall.GetComponent<Rigidbody>();
        // Ensure Rigidbody is kinematic and gravity is off for the dash.
        // currentBall.StopMovementAndClearEffects() should have already made it kinematic.
        if (!ballRb.isKinematic) ballRb.isKinematic = true;
        if (ballRb.useGravity) ballRb.useGravity = false;
        ballRb.linearVelocity = Vector3.zero; // Ensure no carry-over velocity

        float totalPathLength = 0;
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            totalPathLength += Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
        }

        if (totalPathLength < 0.01f)
        {
            Debug.LogWarning("[LightningPathEffect.ExecutePathMovement] Total path length is too short. Path execution aborted.");
            // ResumeNormalMovement will be called by ExecuteLightningSequenceCoroutine after this.
            yield break;
        }

        Vector3 currentActualPosition = ballRb.position; // Start from current RB position

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector3 segmentStart = (i == 0) ? currentActualPosition : pathPoints[i];
            Vector3 segmentEnd = pathPoints[i + 1];
            float segmentDistance = Vector3.Distance(segmentStart, segmentEnd);

            if (segmentDistance < 0.01f) continue;

            float timeForSegment = (segmentDistance / totalPathLength) * totalDuration;
            if (timeForSegment <= 0.001f)
            {
                ballRb.position = segmentEnd;
                currentBall.SyncBallPositionClientRpc(ballRb.position, Vector3.zero);
                currentActualPosition = segmentEnd;
                continue;
            }

            Vector3 segmentDirection = (segmentEnd - segmentStart).normalized;
            float segmentSpeed = segmentDistance / timeForSegment;

            float elapsedTime = 0;
            while (elapsedTime < timeForSegment)
            {
                elapsedTime += Time.deltaTime;
                float fraction = Mathf.Clamp01(elapsedTime / timeForSegment);
                Vector3 newPosition = Vector3.Lerp(segmentStart, segmentEnd, fraction);
                ballRb.position = newPosition;
                currentBall.SyncBallPositionClientRpc(newPosition, segmentDirection * segmentSpeed);
                yield return null;
            }
            ballRb.position = segmentEnd;
            currentBall.SyncBallPositionClientRpc(segmentEnd, segmentDirection * segmentSpeed);
            currentActualPosition = segmentEnd;
        }
        Debug.Log("[LightningPathEffect.ExecutePathMovement] Path execution complete on server.");
    }

    // Renamed from CalculateLightningPathUsingRaycasts
    private List<Vector3> CalculateLightningPathWithFixedWalls(Vector3 startPos, Vector3 ballCurrentVelocityAtEffectStart)
    {
        List<Vector3> points = new List<Vector3>();
        points.Add(startPos);

        // PART 1: Determine target goal based on ball's velocity direction
        float targetGoalZ;
        if (ballCurrentVelocityAtEffectStart.z > 0)
        {
            targetGoalZ = arenaMaxZ; // Ball moving towards positive Z
        }
        else
        {
            targetGoalZ = arenaMinZ; // Ball moving towards negative Z
        }

        // Calculate 2/3 of remaining distance to goal
        float remainingDistanceToGoal = Mathf.Abs(targetGoalZ - startPos.z);
        float travelDistance = remainingDistanceToGoal * (2f / 3f);

        // Calculate target point
        Vector3 targetPoint = new Vector3(
            startPos.x, // Center X
            startPos.y, // Same height
            startPos.z + Mathf.Sign(targetGoalZ - startPos.z) * travelDistance
        );

        Debug.Log($"[LightningPath] Start: {startPos}, Target Goal: {targetGoalZ}, Travel Distance: {travelDistance}, Target Point: {targetPoint}, ballCurrentVelocityAtEffectStart: {ballCurrentVelocityAtEffectStart}");

        // PART 2: Calculate bounce points
        float segmentDistance = travelDistance / 3f; // Divide total travel into 3 segments
        Vector3 currentPos = startPos;
        bool startFromLeftWall = startPos.x < 0; // Determine initial wall based on ball position

        for (int i = 0; i < 3; i++)
        {
            // Calculate Z position for this bounce
            float bounceZ = startPos.z + Mathf.Sign(targetGoalZ - startPos.z) * (segmentDistance * (i + 1));

            // Alternate between walls
            float bounceX = startFromLeftWall ?
                (i % 2 == 0 ? wallNegativeX : wallPositiveX) :
                (i % 2 == 0 ? wallPositiveX : wallNegativeX);

            Vector3 bouncePoint = new Vector3(bounceX, startPos.y, bounceZ);
            points.Add(bouncePoint);
            Debug.Log($"[LightningPath] Bounce {i + 1}: {bouncePoint}");
        }

        // Add final target point
        points.Add(targetPoint);
        Debug.Log($"[LightningPath] Final target point: {targetPoint}");

        return points;
    }

    // OnUpdate, OnCollision, ShouldExpire, ShouldOverride remain the same
    public void OnUpdate(BallController ball) { }
    public void OnCollision(GameObject hitObject, BallController ball, ref float baseDamage) { }
    public bool ShouldExpire() { return sequenceComplete; }
    public bool ShouldOverride(IBallEffect other) { return true; }
    // Default empty implementation for non-LightningPathBallEffect special effects if this class were inherited.
    // However, since this class IS the specific effect, this method IS its starting point.
}