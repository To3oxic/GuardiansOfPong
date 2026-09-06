using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Legacy Legend Projectile - now uses universal ProjectileController
/// This class is kept for backwards compatibility
/// </summary>
public class LegendProjectile : ProjectileController
{
    public void Initialize(Transform target, int damageAmount)
    {
        // Use the universal ProjectileController's target initialization
        InitializeWithTarget(target, damageAmount, ulong.MaxValue);
    }
}
