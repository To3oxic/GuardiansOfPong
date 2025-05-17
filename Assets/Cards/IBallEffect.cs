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

    /// <summary>
    /// Called by BallController when a Special effect is registered, giving it a chance to start its unique sequence.
    /// Non-special effects can have an empty implementation.
    /// </summary>
    void AttemptToStartSpecialSequence(BallController ball, GameManager gameManager);
}