using UnityEngine;
using Unity.Netcode;

public class PaddleController : NetworkBehaviour
{
    public float speed = 25f;

    public int maxHP = 2000;
    public int maxMana = 10;
    [HideInInspector] public float paddleLength = 1.0f;

    public NetworkVariable<float> currentHP = new NetworkVariable<float>(2000);
    public NetworkVariable<float> currentMana = new NetworkVariable<float>(10);

    private float manaRegenTimer = 0f;
    private float boundary = 27f;
    private Vector3 lastSentPosition;

    public string assignedLegendName; // Used to apply correct buffs per player

    void Start()
    {
        if (IsOwner)
        {
            ApplyLegendStoneBuff();
            ApplyCastingLegendBuff();
        }

        if (IsServer)
        {
            currentHP.Value = maxHP;
            currentMana.Value = 4;
        }
    }

    void ApplyLegendStoneBuff()
    {
        var stone = DeckManager.Instance?.GetSelectedStone();
        if (stone != null)
        {
            speed += stone.paddleSpeedBonus;
            Debug.Log($"[PaddleController] Applied stone buff. New speed: {speed}");
        }
    }

    void ApplyCastingLegendBuff()
    {
        var legend = DeckManager.Instance?.GetSelectedLegend();
        if (legend != null)
        {
            Debug.Log($"[PaddleController] Applying casting legend buffs. Damage Boost: {legend.damageBoostPercent}, Mana Cost Reduction: {legend.manaCostReduction}");
            // Future hooks could apply these to a player stats system
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        float input = Input.GetAxis("Horizontal");
        if (transform.position.z > 0) input = -input;
        float move = input * speed * Time.deltaTime;
        Vector3 newPosition = transform.position + new Vector3(move, 0, 0);

        float clampedX = Mathf.Clamp(newPosition.x, -boundary + paddleLength / 2f, boundary - paddleLength / 2f);
        newPosition.x = clampedX;

        transform.position = newPosition;

        // 🔁 Send only occasionally (e.g. every few frames or when changed significantly)
        if (Vector3.Distance(lastSentPosition, newPosition) > 0.1f)
        {
            lastSentPosition = newPosition;
            UpdatePaddlePositionServerRpc(newPosition);
        }

        if (IsServer)
        {
            manaRegenTimer += Time.deltaTime;
            if (manaRegenTimer >= 1f)
            {
                manaRegenTimer = 0f;
                if (currentMana.Value < maxMana)
                {
                    currentMana.Value += 1;
                    Debug.Log($"[PaddleController] {gameObject.name} mana regenerated to {currentMana.Value}");
                }
            }
        }
    }

    [ServerRpc]
    public void ModifyHPServerRpc(int amount)
    {
        if (!IsServer) return;
        currentHP.Value = Mathf.Max(0, currentHP.Value + amount);
    }

    [ServerRpc]
    void UpdatePaddlePositionServerRpc(Vector3 newPosition)
    {
        transform.position = newPosition;
        UpdatePaddlePositionClientRpc(newPosition);
    }

    [ServerRpc]
    public void ModifyManaServerRpc(int amount)
    {
        if (!IsServer) return;
        currentMana.Value = Mathf.Max(0, currentMana.Value + amount);
    }

    [ClientRpc]
    void UpdatePaddlePositionClientRpc(Vector3 newPosition)
    {
        if (!IsOwner)
        {
            transform.position = newPosition;
        }
    }

    public void ReduceHP(float damage)
    {
        if (!IsServer) return;
        currentHP.Value -= damage;
        Debug.Log($"[PaddleController] {gameObject.name} HP reduced by {damage}. New HP: {currentHP.Value}");
        if (currentHP.Value <= 0)
        {
            Debug.Log($"[PaddleController] {gameObject.name} HP reached 0. Triggering end game.");

            ulong loserId = GetComponent<NetworkObject>().OwnerClientId;
            GameManager.Instance.TriggerPaddleDefeat(loserId);

            GetComponent<NetworkObject>().Despawn(); // Optional if you want to remove the paddle visually
        }

    }
}