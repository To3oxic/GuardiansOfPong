using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "PhantomSplitEffect", menuName = "Cards/Effects/Phantom Split")]
public class PhantomSplitEffect : CardEffect
{
    [Header("Phantom Split Settings")]
    public GameObject cloneBallPrefab;
    public float cloneLifetime = 10f;
    public float splitAngle = 45f;
    public float splitSpeed = 8f;
    public float vanishDuration = 0.3f;
    public Material transparentMaterial;

    public override void ApplyEffect(ulong playerId)
    {
        if (BallController.Instance != null)
        {
            BallController.Instance.StartCoroutine(PerformPhantomSplit());
            Debug.Log("[PhantomSplitEffect] Activated by player " + playerId);
        }
        else
        {
            Debug.LogWarning("[PhantomSplitEffect] BallController instance not found!");
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        BallController.Instance?.StartCoroutine(PerformPhantomSplit());
    }


    private IEnumerator PerformPhantomSplit()
    {
        BallController ball = BallController.Instance;
        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        Renderer renderer = ball.GetComponentInChildren<Renderer>();

        
        if (renderer == null)
        {
            Debug.LogWarning("[PhantomSplitEffect] Ball has no Renderer!");
            yield break;
        }

        Material originalMaterial = renderer.material;
        renderer.material = transparentMaterial;
        yield return new WaitForSeconds(vanishDuration);
        renderer.material = originalMaterial;
    }


    private void SpawnClone(Vector3 position, Vector3 direction, Material originalMaterial)
    {
        GameObject clone = Instantiate(cloneBallPrefab, position, Quaternion.identity);
        Renderer renderer = clone.GetComponent<Renderer>();
        renderer.material= originalMaterial;
        Rigidbody rb = clone.GetComponent<Rigidbody>();
        rb.linearVelocity = direction;

        BallClone tagger = clone.GetComponent<BallClone>();
        if (tagger != null)
        {
            tagger.lifetime = cloneLifetime;
        }
    }
}
