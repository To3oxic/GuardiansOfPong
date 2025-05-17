using UnityEngine;
using Unity.Netcode;

public class LegendStoneController : NetworkBehaviour
{
    [Header("References")]
    // Set in inspector or via Initialize
    private PaddleController paddleController;

    private ulong associatedPlayerId;

    [Header("Visual Effects")]
    // Assign in the Inspector on the LegendStoneVisualPrefab, 
    // ideally a child ParticleSystem designed as a beam/stream.
    public ParticleSystem energyBeamEffect;
    private Transform paddleTargetReference; // Set in Initialize

    public void Initialize(PaddleController ownerPaddle, ulong ownerPlayerId)
    {
        paddleController = ownerPaddle;
        associatedPlayerId = ownerPlayerId;
        if (ownerPaddle != null) // Null check for safety
        {
            paddleTargetReference = ownerPaddle.transform;
        }

        // If energyBeamEffect is a child on this prefab, it should be auto-assigned if public 
        // or you can GetComponentInChildren<ParticleSystem>() here if needed.
        // For simplicity, assuming it will be assigned in the prefab inspector.

        if (energyBeamEffect != null)
        {
            if (!energyBeamEffect.isPlaying)
            {
                energyBeamEffect.Play(); // Start playing the effect immediately
            }
        }
        else
        {
            Debug.LogWarning($"[LegendStoneController:{NetworkObjectId}] EnergyBeamEffect is not assigned for player {associatedPlayerId}.");
        }

        Debug.Log($"[LegendStoneController:{NetworkObjectId}] Initialized for player {associatedPlayerId}. Stone Position: {transform.position}. Paddle: {(ownerPaddle != null ? ownerPaddle.name : "null")}");
    }

    void Update()
    {
        // This runs on all clients, making the local visual effect aim correctly.
        if (energyBeamEffect != null && paddleTargetReference != null)
        {
            // Make the particle system (or its direct transform) look at the paddle center.
            // You might need to adjust which transform is rotated depending on your particle system's setup.
            Vector3 directionToPaddle = paddleTargetReference.position - energyBeamEffect.transform.position;
            if (directionToPaddle != Vector3.zero)
            {
                // Ensure the particle system emits along its local forward (often Z axis)
                // If your particle system emits differently, you might need an offset rotation or a dedicated emitter child object.
                energyBeamEffect.transform.rotation = Quaternion.LookRotation(directionToPaddle);
            }
            // Ensure it's playing if it was stopped for some reason (unless you have specific logic to stop/start it)
            if (!energyBeamEffect.isPlaying && IsSpawned) // Only try to play if spawned and active
            {
                energyBeamEffect.Play();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ball"))
        {
            Debug.Log($"[LegendStoneController] Stone for player {associatedPlayerId} (OwnerClientId: {OwnerClientId}) hit by ball on server.");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerLegendStoneDefeat(associatedPlayerId);
                Debug.Log($"[LegendStoneController] Called GameManager.Instance.TriggerLegendStoneDefeat for player {associatedPlayerId}");
            }
            else
            {
                Debug.LogError("[LegendStoneController] GameManager.Instance is null. Cannot process player loss.");
            }

            // Consider despawning the stone or other effects.
            // For now, just the loss condition is triggered.
            // if (NetworkObject != null && NetworkObject.IsSpawned)
            // {
            //     NetworkObject.Despawn();
            // }
        }
    }

    // Placeholder for HP-based state change
    public void HandlePaddleHPLow()
    {
        if (!IsServer) return;
        Debug.Log($"[LegendStoneController] Player {associatedPlayerId}'s paddle HP is low. Moving stone and granting passive.");
        // TODO: Implement moving the stone "outside the board"
        // TODO: Implement granting the new passive ability to the paddle
    }

    // TODO: Add visual effect logic (e.g., particles pointing to paddle)
}