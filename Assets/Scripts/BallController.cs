using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class BallController : NetworkBehaviour
{
    public float speed = 22f;
    private Rigidbody rb;
    private Vector3 launchDirection;

    public static BallController Instance { get; private set; }

    private float bounceCooldown = 0.05f;
    private float lastBounceTime = -1f;

    private bool isStoneBallActive = false;
    private int stoneHitsRemaining = 0;
    private int bonusDamage = 0;
    private Material originalMaterial;
    private Renderer ballRenderer;

    private float trailRendererTime = 1;

    private TrailRenderer trailRenderer;

    private float baseDamage = 10;
    private List<IBallEffect> activeBallEffects = new();

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        ballRenderer = GetComponent<Renderer>();
        originalMaterial = ballRenderer.material;
        trailRenderer = GetComponent<TrailRenderer>();
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (IsServer)
        {
            LaunchBall();
        }
    }

    void LaunchBall()
    {
        // Choose a random angle between 45° and 80°
        float angleInDegrees = Random.Range(45f, 80f);
        float angleInRadians = angleInDegrees * Mathf.Deg2Rad;

        // Convert angle to direction vector in XZ plane
        float x = Mathf.Cos(angleInRadians);
        float z = Mathf.Sin(angleInRadians);
        Vector3 direction = new Vector3(x, 1f, z);

        // Randomly flip X and Z to get all 4 quadrants
        if (Random.value < 0.5f) direction.x *= -1f;
        if (Random.value < 0.5f) direction.z *= -1f;

        launchDirection = direction.normalized;
        rb.linearVelocity = launchDirection * speed;

        Debug.Log($"[BallController] Ball launched at angle {angleInDegrees}° -> {launchDirection}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer)
            return;

        if (Time.time - lastBounceTime < bounceCooldown)
            return;

        lastBounceTime = Time.time;
        if (speed < 30) speed += 5f;
        else if (speed < 50) speed += 2f;
        else if (speed < 60) speed += 1f;

        if (collision.gameObject.CompareTag("Wall"))
        {
            launchDirection.x = -launchDirection.x;
            rb.linearVelocity = launchDirection.normalized * speed;
            Debug.Log("Ball hit wall. New velocity: " + rb.linearVelocity);
        }
        else if (collision.gameObject.TryGetComponent<IPlaceable>(out var placeable))
        {
            int calculatedDamage = CalculateDamage() * 3;
            placeable.TakeDamage(calculatedDamage);
            ContactPoint contact = collision.contacts[0];
            Vector3 normal = contact.normal.normalized;

            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
            {
                // Hit from the side → reverse X
                launchDirection.x = -launchDirection.x;
            }
            else
            {
                // Hit from top/bottom → reverse Z
                launchDirection.z = -launchDirection.z;
            }

            rb.linearVelocity = launchDirection.normalized * speed;

            Debug.Log($"[Ball] Hit placeable '{collision.gameObject.name}' — New direction: {launchDirection}");
        }

        else if (collision.gameObject.CompareTag("client1Paddle") || collision.gameObject.CompareTag("client2Paddle"))
        {
            Vector3 paddleCenter = collision.transform.position;
            Vector3 contactPoint = collision.contacts[0].point;

            float offset = contactPoint.x - paddleCenter.x;
            float paddleWidth = collision.collider.bounds.size.x;

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
            int calculatedDamage = CalculateDamage();

            rb.linearVelocity = launchDirection * speed;

            Debug.Log($"[BallController] Ball bounced at angle {bounceAngle}° -> {launchDirection}");

            PaddleController paddle = collision.gameObject.GetComponent<PaddleController>();
            if (paddle != null)
                paddle.ReduceHP(calculatedDamage);
        }
    }
    public int CalculateDamage()
    {
        float damage = baseDamage + (2 * speed / 10);

        // Apply modifiers from active ball effects
        for (int i = activeBallEffects.Count - 1; i >= 0; i--)
        {
            activeBallEffects[i].OnCollision(null, this, ref damage);
            if (activeBallEffects[i].ShouldExpire())
            {
                activeBallEffects.RemoveAt(i);
            }
        }

        return Mathf.RoundToInt(damage);
    }

    public void ResetToCenter()
    {
        float resetY = 1f;

        trailRenderer.Clear();
        trailRenderer.emitting = false;
        transform.position = new Vector3(0f, resetY, 0f);

        // Launch angle logic
        float angleInDegrees = Random.Range(45f, 80f);
        float angleInRadians = angleInDegrees * Mathf.Deg2Rad;

        float x = Mathf.Cos(angleInRadians);
        float z = Mathf.Sin(angleInRadians);
        Vector3 direction = new Vector3(x, 0f, z);

        if (Random.value < 0.5f) direction.x *= -1f;
        if (Random.value < 0.5f) direction.z *= -1f;

        launchDirection = direction.normalized;
        speed = 15f;
        rb.linearVelocity = launchDirection * speed;

        trailRenderer.emitting = true; // ✅ Re-enable trail cleanly

        Debug.Log("[BallController] Ball reset and relaunched from center.");
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        // If rb is kinematic, it implies a special effect is controlling movement, so skip normal triggers.
        if (rb.isKinematic)
            return;

        // Removed LegendStone check here, as LegendStoneController.OnCollisionEnter handles this.
        // if (other.CompareTag("LegendStone"))
        // {
        //     LegendStoneController stoneController = other.GetComponent<LegendStoneController>();
        //     if (stoneController != null)
        //     {
        //         // The LegendStoneController itself will call GameManager.Instance.TriggerLegendStoneDefeat
        //         Debug.Log("Ball hit a legend stone - collision detected by BallController (this log can be removed if redundant)."); 
        //     }
        // }
        // else 
        if (other.CompareTag("client2Goal"))
        {
            Debug.Log("Goal! Player scored a point.");
            GameManager.Instance.OpponentLosesPoint();
        }
        else if (other.CompareTag("client1Goal"))
        {
            Debug.Log("Goal! Opponent scored a point.");
            GameManager.Instance.PlayerLosesPoint();
        }
    }

    void Update()
    {
        if (IsServer)
        {
            // If Rigidbody is kinematic, an effect is controlling it, so don't run standard effect updates or general position sync.
            if (!rb.isKinematic)
            {
                for (int i = activeBallEffects.Count - 1; i >= 0; i--)
                {
                    activeBallEffects[i].OnUpdate(this);

                    if (activeBallEffects[i].ShouldExpire())
                    {
                        activeBallEffects.RemoveAt(i);
                    }
                }
                UpdateBallPositionClientRpc(transform.position); // Regular position update for non-kinematic state
            }
        }
    }

    public void RegisterBallEffect(IBallEffect newEffect)
    {
        if (!IsServer) return;

        if (newEffect.Type == BallEffectType.Special)
        {
            Debug.Log($"[BallController] Registering SPECIAL effect: {newEffect.Source}. Clearing other effects and preparing to start its sequence.");

            activeBallEffects.Add(newEffect); // Add only the new special effect

            // Attempt to start its sequence using the new interface method.
            if (GameManager.Instance != null)
            {
                Debug.Log($"[BallController] Attempting to start special sequence for: {newEffect.Source}");
                newEffect.AttemptToStartSpecialSequence(this, GameManager.Instance);
            }
            else
            {
                Debug.LogError($"[BallController] GameManager.Instance is null. Cannot start Special effect sequence for {newEffect.Source}. Effect will be registered but not run.");
            }
        }
        else // For non-special effects, use the existing override logic
        {
            for (int i = activeBallEffects.Count - 1; i >= 0; i--)
            {
                var existing = activeBallEffects[i];
                // Example: More complex override logic could be based on specific sources or priorities
                if (existing.Type == newEffect.Type && existing.Source == newEffect.Source) // Simple same type/source override
                {
                    if (newEffect.ShouldOverride(existing))
                    {
                        Debug.Log($"[BallController] Overriding existing effect {existing.Source} with {newEffect.Source} of the same type.");
                        activeBallEffects.RemoveAt(i);
                        break; // Found and handled, now add the new one outside loop
                    }
                    else
                    {
                        Debug.Log($"[BallController] New effect {newEffect.Source} did not override existing {existing.Source} of the same type. Not adding.");
                        return; // Do not add newEffect
                    }
                }
            }
            Debug.Log($"[BallController] Adding non-special effect: {newEffect.Source} to list of {activeBallEffects.Count} effects.");
            activeBallEffects.Add(newEffect);
        }
    }


    [ClientRpc]
    void UpdateBallPositionClientRpc(Vector3 newPosition)
    {
        if (!IsServer)
        {
            // NEW: Check distance before teleporting
            float distance = Vector3.Distance(transform.position, newPosition);
            if (distance > 5f) // adjust threshold as needed
            {
                trailRenderer.Clear();
                trailRenderer.emitting = false;
                StartCoroutine(EnableTrailNextFrame());
            }

            transform.position = newPosition;
        }
    }
    private IEnumerator EnableTrailNextFrame()
    {
        yield return null;
        trailRenderer.emitting = true;
    }


    public void IncreaseSpeed(float amount)
    {
        speed += amount;
        rb.linearVelocity = rb.linearVelocity.normalized * speed;
        Debug.Log($"Ball speed increased to {speed}");
    }

    public void DecreaseSpeed(float amount)
    {
        speed = Mathf.Max(5f, speed - amount);
        rb.linearVelocity = rb.linearVelocity.normalized * speed;
        Debug.Log($"Ball speed decreased to {speed}");
    }

    [ClientRpc]
    public void SyncBallPositionClientRpc(Vector3 position, Vector3 velocity, ClientRpcParams clientRpcParams = default)
    {
        if (!IsServer)
        {
            transform.position = position;
            rb.linearVelocity = velocity;
        }
    }

    public void ActivateStoneBall(Material stoneMaterial, float speedReduction, int damageBoost)
    {
        if (isStoneBallActive) return;

        isStoneBallActive = true;
        stoneHitsRemaining = 1;
        bonusDamage = damageBoost;

        originalMaterial = GetComponent<Renderer>().material;
        GetComponent<Renderer>().material = stoneMaterial;

        speed = Mathf.Max(5f, speed - speedReduction);
        rb.linearVelocity = rb.linearVelocity.normalized * speed;

        Debug.Log("[BallController] Stone Ball activated.");
    }

    public void SetTemporaryMaterial(Material newMaterial)
    {
        SetMaterialClientRpc(true, newMaterial.name);
    }

    [ClientRpc]
    private void SetMaterialClientRpc(bool isStone, string materialName)
    {
        if (ballRenderer == null) return;

        if (isStone)
        {
            Material newMat = Resources.Load<Material>(materialName);
            if (newMat != null)
            {
                ballRenderer.material = newMat;
            }
        }
        else
        {
            ballRenderer.material = originalMaterial;
        }
    }

    [ClientRpc]
    private void ResetMaterialClientRpc()
    {
        if (ballRenderer != null)
        {
            ballRenderer.material = originalMaterial;
        }
    }

    public void StopMovementAndClearEffects()
    {
        if (!IsServer) return;

        // Zero out velocities FIRST, while Rigidbody might still be non-kinematic
        if (rb != null) // Ensure rb is not null
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Then make kinematic and set other properties
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        activeBallEffects.Clear();
        Debug.Log("[BallController] Movement stopped, effects cleared, RB kinematic.");

        // Notify clients to stop movement
        StopMovementOnClientsClientRpc();
    }

    [ClientRpc]
    private void StopMovementOnClientsClientRpc()
    {
        if (!IsServer) // Only run on clients
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            Debug.Log("[BallController] Client-side movement stopped for special effect.");
        }
    }

    public void ResumeNormalMovement(float newSpeed, Vector3 newDirection)
    {
        if (!IsServer) return;
        Debug.Log($"[BallController.ResumeNormalMovement] CALLED. Received newSpeed: {newSpeed}, newDirection: {newDirection}");

        rb.isKinematic = false;
        rb.useGravity = true; // Re-enable gravity (assuming it's normally used)

        this.launchDirection = newDirection.normalized; // Update internal launchDirection FIELD
        this.speed = newSpeed; // Update internal speed FIELD

        rb.linearVelocity = this.launchDirection * this.speed;
        Debug.Log($"[BallController.ResumeNormalMovement] APPLIED. rb.linearVelocity: {rb.linearVelocity}, this.launchDirection: {this.launchDirection}, this.speed: {this.speed}");

        // Ensure clients also resume normal movement if they were doing special prediction/kinematic handling
        // For now, SyncBallPositionClientRpc should update them with the new physics-driven velocity.
        SyncBallPositionClientRpc(rb.position, rb.linearVelocity);
    }

    // Added missing getter methods needed by effects
    public Vector3 GetVelocity()
    {
        if (rb == null) return Vector3.zero; // Safeguard if rb is not yet initialized
        return rb.linearVelocity;
    }

    public float GetCurrentSpeed()
    {
        return this.speed; // 'speed' is a public field in BallController
    }
}