using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class LegendStoneController : NetworkBehaviour
{
    [Header("References")]
    // Set in inspector or via Initialize
    private PaddleController paddleController;

    [Header("Paddle Configuration")]
    public GameObject associatedPaddlePrefab; // The paddle prefab this stone is connected to

    private ulong associatedPlayerId;
    private bool isMovingToSafePoint = false; // Flag to prevent interference during movement

    // Alternative: NetworkVariable for position sync (comment out if using NetworkTransform approach)
    // private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Visual Effects")]
    // Legacy support - keeping for backwards compatibility
    public ParticleSystem energyBeamEffect;

    [Header("Advanced Spirit Beam")]
    public SpiritBeamController spiritBeam;
    public BeamSettings customBeamSettings;

    private Transform paddleTargetReference; // Set in Initialize

    void Start()
    {
        // Check NetworkObject and NetworkTransform configuration
        NetworkObject networkObject = GetComponent<NetworkObject>();
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        Collider legendStoneCollider = GetComponent<Collider>();
        if (legendStoneCollider != null)
        {
            legendStoneCollider.isTrigger = true;
        }
    }

    private System.Collections.IEnumerator DebugBeamAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        if (spiritBeam != null)
        {
            if (spiritBeam.beamParticleSystem != null)
            {
            }
            else
            {
            }
        }
        else
        {
        }
    }

    private Color GetSimpleBeamColor()
    {
        // Simple color assignment based on position or name
        int hash = gameObject.name.GetHashCode();
        switch (Mathf.Abs(hash) % 4)
        {
            case 0: return Color.cyan;
            case 1: return Color.magenta;
            case 2: return Color.yellow;
            case 3: return Color.green;
            default: return Color.white;
        }
    }

    public void Initialize(PaddleController ownerPaddle, ulong ownerPlayerId)
    {
        paddleController = ownerPaddle;
        associatedPlayerId = ownerPlayerId;

        if (ownerPaddle != null)
        {
            paddleTargetReference = ownerPaddle.transform;
        }
        else
        {
        }
    }

    public ulong GetAssociatedPlayerId()
    {
        return associatedPlayerId;
    }

    // Helper method to safely manage NetworkTransform during local animations
    private void SetNetworkTransformEnabled(bool enabled)
    {
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
        {
            if (networkTransform.enabled != enabled)
            {
                networkTransform.enabled = enabled;
            }
        }
        else
        {
        }
    }

    // CLIENT-SIDE: Visual movement animation (temporarily disables NetworkTransform)
    public void MoveToSafePointVisually(Vector3 safePointPosition)
    {
        // Prevent interference during movement
        isMovingToSafePoint = true;

        // Start the visual movement animation
        StartCoroutine(MoveToSafePointSequenceVisual(safePointPosition));
    }

    private System.Collections.IEnumerator MoveToSafePointSequence(Vector3 safePointPosition)
    {
        // Stop particle effects
        if (energyBeamEffect != null)
        {
            energyBeamEffect.Stop();
        }

        if (spiritBeam != null && spiritBeam.beamParticleSystem != null)
        {
            spiritBeam.beamParticleSystem.Stop();
        }

        // Move underground
        Vector3 originalPosition = transform.position;
        Vector3 undergroundPosition = new Vector3(originalPosition.x, originalPosition.y - 5f, originalPosition.z);

        float animationDuration = 1.0f;
        float elapsedTime = 0f;

        // Animate going underground
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            Vector3 newPosition = Vector3.Lerp(originalPosition, undergroundPosition, progress);
            transform.position = newPosition;

            // Log every 10th frame to see if position is actually changing
            if (Time.frameCount % 10 == 0)
            {
            }

            yield return null;
        }

        // Note: In client-side visual approach, no need to sync position via RPC
        // Wait underground
        yield return new WaitForSeconds(0.5f);

        // Emerge at safe point
        elapsedTime = 0f;
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            Vector3 newPosition = Vector3.Lerp(undergroundPosition, safePointPosition, progress);
            transform.position = newPosition;

            // Log every 10th frame to see if position is actually changing
            if (Time.frameCount % 10 == 0)
            {
            }

            yield return null;
        }

        // Note: In client-side visual approach, no need for final position sync via RPC

        // Restart particle effects
        if (energyBeamEffect != null)
        {
            energyBeamEffect.Play();
        }

        if (spiritBeam != null && spiritBeam.beamParticleSystem != null)
        {
            spiritBeam.beamParticleSystem.Play();
        }

        isMovingToSafePoint = false; // Clear flag - movement complete
    }

    // CLIENT-SIDE: Visual movement sequence (purely visual, no networking)
    private System.Collections.IEnumerator MoveToSafePointSequenceVisual(Vector3 safePointPosition)
    {
        // STEP 1: Temporarily disable NetworkTransform to allow local animation
        SetNetworkTransformEnabled(false);

        // Stop particle effects during movement
        if (energyBeamEffect != null && energyBeamEffect.isPlaying)
        {
            energyBeamEffect.Stop();
        }

        if (spiritBeam != null && spiritBeam.beamParticleSystem != null)
        {
            spiritBeam.beamParticleSystem.Stop();
        }

        // Move underground
        Vector3 originalPosition = transform.position;
        Vector3 undergroundPosition = new Vector3(originalPosition.x, originalPosition.y - 5f, originalPosition.z);

        float animationDuration = 1.0f;
        float elapsedTime = 0f;

        // Animate going underground
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            Vector3 newPosition = Vector3.Lerp(originalPosition, undergroundPosition, progress);
            transform.position = newPosition;
            yield return null;
        }

        // Wait underground
        yield return new WaitForSeconds(0.5f);

        // Emerge at safe point
        elapsedTime = 0f;
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            Vector3 newPosition = Vector3.Lerp(undergroundPosition, safePointPosition, progress);
            transform.position = newPosition;
            yield return null;
        }

        // Restart particle effects
        if (energyBeamEffect != null)
        {
            energyBeamEffect.Play();
        }

        if (spiritBeam != null && spiritBeam.beamParticleSystem != null)
        {
            spiritBeam.beamParticleSystem.Play();
        }

        // STEP 3: Re-enable NetworkTransform at the final position
        SetNetworkTransformEnabled(true);

        // Clear the movement flag
        isMovingToSafePoint = false;
    }

    void Update()
    {
        // Simple legacy particle system support
        if (energyBeamEffect != null && associatedPaddlePrefab != null)
        {
            // Make the particle system look at the paddle prefab position
            Vector3 directionToPaddle = associatedPaddlePrefab.transform.position - energyBeamEffect.transform.position;
            if (directionToPaddle != Vector3.zero)
            {
                energyBeamEffect.transform.rotation = Quaternion.LookRotation(directionToPaddle);
            }
            // Ensure it's playing (unless we're moving to safe point)
            if (!energyBeamEffect.isPlaying && !isMovingToSafePoint)
            {
                energyBeamEffect.Play();
            }
        }

        // The SpiritBeamController handles its own updates automatically
    }

    // Placeholder for HP-based state change
    public void HandlePaddleHPLow()
    {
        if (!IsServer) return;

        // Dramatic visual change when paddle HP is low
        if (spiritBeam != null)
        {
            spiritBeam.SetBeamColor(Color.red);
            spiritBeam.SetBeamIntensity(1.0f); // Maximum intensity for dramatic effect
        }

        // TODO: Implement moving the stone "outside the board"
        // TODO: Implement granting the new passive ability to the paddle
    }

    public void DisconnectFromPaddle()
    {
        if (spiritBeam != null)
        {
            spiritBeam.EnableBeam(false);
        }

        if (energyBeamEffect != null && energyBeamEffect.isPlaying)
        {
            energyBeamEffect.Stop();
        }

        paddleTargetReference = null;
    }

    public void ReconnectToPaddle(Transform newPaddleTarget)
    {
        paddleTargetReference = newPaddleTarget;

        if (spiritBeam != null && newPaddleTarget != null)
        {
            // Destroy the temporary target if it exists
            Transform tempTarget = spiritBeam.transform.Find("TempPaddleTarget");
            if (tempTarget != null)
            {
                if (Application.isPlaying)
                    Destroy(tempTarget.gameObject);
                else
                    DestroyImmediate(tempTarget.gameObject);
            }

            spiritBeam.Initialize(transform, newPaddleTarget, customBeamSettings);
            spiritBeam.EnableBeam(true);
        }
    }

    private System.Collections.IEnumerator DebugParticleSystems()
    {
        yield return new WaitForSeconds(2f); // Wait 2 seconds for initialization

        if (spiritBeam != null)
        {
            if (spiritBeam.beamParticleSystem != null)
            {
            }
        }

        if (energyBeamEffect != null)
        {
        }
    }

    public void UpdateBeamTarget()
    {
        // This method can be called to refresh the beam target
        // Useful when paddle positions change or when switching from prefab position to actual paddle
        if (spiritBeam != null && paddleTargetReference != null)
        {
            spiritBeam.PaddleTransform = paddleTargetReference;
        }
    }

    // TODO: Add visual effect logic (e.g., particles pointing to paddle)
}