using UnityEngine;

public class StoneBallIBallEffect : IBallEffect
{
    private int bonusDamage;
    private bool used = false;

    public StoneBallIBallEffect(int amount)
    {
        bonusDamage = amount;
    }
    public BallEffectType Type => BallEffectType.DamageModifier;
    public string Source => "StoneBall";

    public void OnUpdate(BallController ball) { }

    public void OnCollision(GameObject hit, BallController ball, ref float baseDamage)
    {
        baseDamage += bonusDamage;
        used = true;
    }

    public bool ShouldExpire() => used;

    public bool ShouldOverride(IBallEffect other)
    {
        // Replace any existing damage modifier
        return other.Type == BallEffectType.DamageModifier;
    }

    // Implementation for IBallEffect interface, even if not used by this specific effect type.
    public void AttemptToStartSpecialSequence(BallController ball, GameManager gameManager)
    {
        // This effect is not a "Special" sequence-starting effect, so this method is intentionally empty.
    }
}