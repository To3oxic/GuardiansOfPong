using UnityEngine;
using System.Collections;

public abstract class CardEffect : ScriptableObject
{
    public virtual bool RequiresPlacement() => false;
    public virtual void StartPlacement(System.Action<Vector3, Vector3> onPlacementConfirmed)
    {
    }

    /// <summary>
    /// Called when placement is confirmed by the drag & drop system
    /// </summary>
    public virtual void ConfirmPlacement(Vector3 position, Vector3 direction)
    {
        // Override this in derived classes if needed
    }

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

    public abstract void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1);

    public virtual GameObject ApplyEffect(ulong playerId, Vector3 position, Vector3 direction, CardData cardData, int cardLevel = 1)
    {
        return null;
    }

    /// <summary>
    /// Spawns a unit at the given position. The unit will automatically play its spawn animation
    /// on all clients via OnNetworkSpawn(). This is called by ApplyEffect in derived classes.
    /// </summary>
    protected void AnimateSpawnedUnit(GameObject spawnedUnit, Vector3 landingPosition)
    {
        if (spawnedUnit == null) return;

        // Unit will automatically play spawn animation in OnNetworkSpawn()
        // Particle effect is set directly on the prefab in the Inspector
    }

    public virtual void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        // Legacy method - kept for compatibility with existing code
    }
}
