using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    public virtual bool RequiresPlacement() => false;
    public virtual void StartPlacement(System.Action<Vector3, Vector3> onPlacementConfirmed) { }

    /// <summary>
    /// Gets the GameObject to be used for client-side preview during placement.
    /// </summary>
    /// <returns>The prefab to instantiate for preview, or null if no preview is needed.</returns>
    public virtual GameObject GetPreviewPrefab() { return null; }

    /// <summary>
    /// Gets the radius for the placement preview visual (e.g., a circle indicating AoE or unit size).
    /// </summary>
    /// <returns>The radius of the effect. Defaults to 1f.</returns>
    public virtual float GetEffectRadius() => 1f;

    public abstract void ApplyEffect(ulong playerId);
    public virtual void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        Debug.LogWarning("[CardEffect] ApplyEffect with position/direction not implemented for this card.");
    }

    public virtual void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default) { }
}
