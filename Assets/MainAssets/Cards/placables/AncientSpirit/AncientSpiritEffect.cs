using UnityEngine;
using Unity.Netcode;
using System;

[CreateAssetMenu(fileName = "AncientSpiritEffect", menuName = "Cards/Effects/AncientSpirit")]
public class AncientSpiritEffect : CardEffect
{
    public GameObject ancientSpiritPrefab;
    public ParticleSystem cardParticleEffectPrefab;

    public override float GetEffectRadius() => 2f;
    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {

    }

    public override GameObject GetPreviewPrefab()
    {
        return ancientSpiritPrefab;
    }

    // This version of ApplyEffect is required by the abstract base class CardEffect.
    // For AncientSpirit, which requires placement, this method should ideally not be called directly.
    // It's implemented here to satisfy the abstract requirement.
    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
        // This version is not used for placeable effects that require position/direction
    }

    public override GameObject ApplyEffect(ulong playerId, Vector3 position, Vector3 direction, CardData cardData, int cardLevel = 1)
    {
        if (!NetworkManager.Singleton.IsServer) return null;

        if (ancientSpiritPrefab == null)
        {
            Debug.LogError("[AncientSpiritEffect] Ancient Spirit Prefab not assigned!");
            return null;
        }

        // Ensure Y coordinate is always 0 for ground placement
        position.y = 0f;

        Debug.Log($"[AncientSpiritEffect] Attempting to instantiate AncientSpirit at position: {position}, level: {cardLevel}");
        GameObject spirit = Instantiate(ancientSpiritPrefab, position, Quaternion.identity);
        NetworkObject netObj = spirit.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[AncientSpiritEffect] AncientSpirit network object spawned for player {playerId}.");

            // Apply leveled stats to the unit
            BaseUnitController unit = spirit.GetComponent<BaseUnitController>();
            if (unit != null && cardData != null)
            {
                unit.SetAttackDamage(cardData.GetDamageAtLevel(cardLevel));
                unit.SetMaxHP(cardData.GetHPAtLevel(cardLevel));
            }

            return spirit;
        }
        else
        {
            Debug.LogWarning("[AncientSpiritEffect] AncientSpirit prefab is missing NetworkObject component!");
            GameObject.Destroy(spirit);
            return null;
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        // Only run on clients, not on server
        if (NetworkManager.Singleton.IsServer) return;

        // Trigger screen shake effect when Ancient Spirit spawns
        if (ScreenShakeManager.Instance != null)
        {
            ScreenShakeManager.Instance.ShakeScreen(intensity: 0.8f, duration: 0.6f, frequency: 12f);
            Debug.Log("[AncientSpiritEffect] Screen shake triggered for Ancient Spirit spawn");
        }
    }
}