using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the card drop animation: spawn at y+2, drop to ground, play particle effect, then cleanup.
/// This script is dynamically added to the card visual prefab when spawned.
/// </summary>
public class CardDropAnimator : MonoBehaviour
{
    private CardEffect cardEffect;
    private float dropDuration = 0.5f; // Drop very fast

    public void StartDropAnimation(Vector3 targetPosition, CardEffect effect)
    {
        cardEffect = effect;
        StartCoroutine(DropCardAndPlayEffectCoroutine(targetPosition));
    }

    private IEnumerator DropCardAndPlayEffectCoroutine(Vector3 targetPosition)
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = targetPosition;

        // Drop the card from y+2 to y=0
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / dropDuration;
            transform.position = Vector3.Lerp(startPos, endPos, progress);
            yield return null;
        }

        // Ensure final position is exact
        transform.position = endPos;

        // Play particle effect when card lands
        if (cardEffect != null)
        {
            cardEffect.ApplyVisualEffect(0, endPos, Vector3.zero);
        }

        // Wait for particle to finish before destroying card visual (5 seconds for particle)
        yield return new WaitForSeconds(5f);

        // Destroy the card visual
        Destroy(gameObject);
    }
}
