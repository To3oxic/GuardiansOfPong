using UnityEngine;
using Unity.Netcode;
using System.Collections;

[CreateAssetMenu(fileName = "VanishBallEffect", menuName = "Cards/Effects/VanishBall")]
public class VanishBallEffect : CardEffect
{
    [Header("Vanish Ball Settings")]
    public float vanishDuration = 3f;
    public Material invisibleMaterial; // Assign invisible material in Inspector

    public override bool RequiresPlacement() => false;

    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
        if (!GameManager.Instance.IsServer) return;

        BallController currentBall = BallController.Instance;
        if (currentBall == null)
        {
            Debug.LogWarning("[VanishBallEffect] BallController.Instance not found, cannot apply vanish effect.");
            return;
        }

        if (invisibleMaterial == null)
        {
            Debug.LogWarning("[VanishBallEffect] Invisible material is not assigned!");
            return;
        }

        Debug.Log($"[VanishBallEffect] Vanish Ball activated by player {playerId} at level {cardLevel}");

        // Get duration from card data
        float duration = cardData != null ? cardData.GetDurationAtLevel(cardLevel) : vanishDuration;

        // Get the mesh renderer from the ball
        MeshRenderer meshRenderer = currentBall.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning("[VanishBallEffect] Ball does not have a MeshRenderer component!");
            return;
        }

        // Get the trail renderer
        TrailRenderer trailRenderer = currentBall.GetComponent<TrailRenderer>();

        // Start the vanish coroutine on GameManager
        GameManager.Instance.StartCoroutine(VanishBall(meshRenderer, trailRenderer, duration));
    }
    private IEnumerator VanishBall(MeshRenderer meshRenderer, TrailRenderer trailRenderer, float duration = 3f)
    {
        Debug.Log("[VanishBallEffect] Making ball invisible");

        // Store original material
        Material originalMaterial = meshRenderer.material;

        // Change to invisible material
        meshRenderer.material = invisibleMaterial;

        // Disable trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            Debug.Log("[VanishBallEffect] Trail renderer disabled");
        }

        yield return new WaitForSeconds(duration);

        // Restore original material
        if (meshRenderer != null)
        {
            Debug.Log("[VanishBallEffect] Making ball visible again");
            meshRenderer.material = originalMaterial;
        }

        // Re-enable trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
            Debug.Log("[VanishBallEffect] Trail renderer enabled");
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        BallController currentBall = BallController.Instance;
        if (currentBall == null)
        {
            Debug.LogWarning("[VanishBallEffect] BallController.Instance not found on client, cannot apply visual effect.");
            return;
        }

        // Get the mesh renderer from the ball
        MeshRenderer meshRenderer = currentBall.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning("[VanishBallEffect] Ball does not have a MeshRenderer component!");
            return;
        }

        // Get the trail renderer
        TrailRenderer trailRenderer = currentBall.GetComponent<TrailRenderer>();

        // Start the vanish coroutine on GameManager for clients
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartCoroutine(VanishBall(meshRenderer, trailRenderer));
        }
        Debug.Log("[VanishBallEffect] Vanish effect applied on client by player " + playerId);
    }


}
