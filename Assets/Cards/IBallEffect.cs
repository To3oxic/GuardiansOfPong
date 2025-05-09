using UnityEngine;
using Unity.Netcode;
public interface IBallEffect
{

    BallEffectType Type { get; }
    string Source { get; } // name of card or effect, optional

    void OnUpdate(BallController ball);
    void OnCollision(GameObject hitObject, BallController ball, ref float baseDamage);
    bool ShouldExpire();

    /// Should return true if this effect should cancel/replace another of the same type
    bool ShouldOverride(IBallEffect other);
}