using UnityEngine;

[CreateAssetMenu(fileName = "StoneBallEffect", menuName = "Cards/Effects/Stone Ball")]
public class StoneBallEffect : CardEffect
{
    [Header("Stone Ball Settings")]
    public Material stoneMaterial;
    public float speedReduction = 5f;
    public int damageBoost = 10;

    public override void ApplyEffect(ulong playerId)
    {
        if (BallController.Instance != null)
        {
            BallController.Instance.RegisterBallEffect(new StoneBallIBallEffect(15));

            Debug.Log("[StoneBallEffect] Activated by player " + playerId);
        }
        else
        {
            Debug.LogWarning("[StoneBallEffect] BallController instance not found!");
        }
    }
}
