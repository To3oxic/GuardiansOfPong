using UnityEngine;
using Unity.Netcode;

[CreateAssetMenu(fileName = "LightningBallEffect", menuName = "Cards/Effects/LightningBall")]
public class LightningBallEffect : CardEffect // This is the ScriptableObject CardEffect
{
    [Header("Lightning Effect Settings")]
    public float trailVisibleDuration = 0.75f;
    public float lightningDashDuration = 0.25f;
    // public LayerMask wallLayerMask; // No longer needed for fixed wall calculations

    // arenaMin/Max X are no longer needed here, Z boundaries are fetched from GameManager

    public override bool RequiresPlacement() => false;

    public override void ApplyEffect(ulong playerId) // Called on Server
    {
        if (!GameManager.Instance.IsServer) return;

        BallController ball = BallController.Instance;
        GameManager gameManager = GameManager.Instance;

        if (ball == null)
        {
            Debug.LogWarning("[LightningBallEffect SO] BallController.Instance not found, cannot apply effect.");
            return;
        }
        if (gameManager == null)
        {
            Debug.LogWarning("[LightningBallEffect SO] GameManager.Instance not found, cannot apply effect.");
            return;
        }

        // Check for wallLayerMask is no longer needed
        // if (wallLayerMask.value == 0) 
        // {
        //     Debug.LogError("[LightningBallEffect SO] Wall Layer Mask is not set in the Inspector! Cannot calculate path correctly.");
        //     return;
        // }

        Debug.Log($"[LightningBallEffect SO] Applying effect. PlayerID: {playerId}");

        LightningPathBallEffect newLightningEffect = new LightningPathBallEffect(
            sourceCardName: this.name,
            trailDuration: this.trailVisibleDuration,
            dashDuration: this.lightningDashDuration,
            minZ: gameManager.arenaMinZ, // Pass Z boundaries from GameManager
            maxZ: gameManager.arenaMaxZ
        // wallsLayer: this.wallLayerMask // Removed
        );

        ball.RegisterBallEffect(newLightningEffect);
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        Debug.Log($"[LightningBallEffect SO] Visual effect for card activation by player {playerId}.");
    }
}