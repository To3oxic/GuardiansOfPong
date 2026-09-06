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
            return;
        }

        if (invisibleMaterial == null)
        {
            return;
        }

        // Get duration from card data
        float duration = cardData != null ? cardData.GetDurationAtLevel(cardLevel) : vanishDuration;

        // Get the mesh renderer from the ball
        MeshRenderer meshRenderer = currentBall.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            return;
        }

        // Get the trail renderer
        TrailRenderer trailRenderer = currentBall.GetComponent<TrailRenderer>();

        // Start the vanish coroutine on GameManager
        GameManager.Instance.StartCoroutine(VanishBall(meshRenderer, trailRenderer, duration));
    }
    private IEnumerator VanishBall(MeshRenderer meshRenderer, TrailRenderer trailRenderer, float duration = 3f)
    {
        // Store original material
        Material originalMaterial = meshRenderer.material;

        // Change to invisible material
        meshRenderer.material = invisibleMaterial;

        // Disable trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }

        yield return new WaitForSeconds(duration);

        // Restore original material
        if (meshRenderer != null)
        {
            meshRenderer.material = originalMaterial;
        }

        // Re-enable trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        BallController currentBall = BallController.Instance;
        if (currentBall == null)
        {
            return;
        }

        // Get the mesh renderer from the ball
        MeshRenderer meshRenderer = currentBall.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            return;
        }

        // Get the trail renderer
        TrailRenderer trailRenderer = currentBall.GetComponent<TrailRenderer>();

        // Start the vanish coroutine on GameManager for clients
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartCoroutine(VanishBall(meshRenderer, trailRenderer));
        }
    }
}
