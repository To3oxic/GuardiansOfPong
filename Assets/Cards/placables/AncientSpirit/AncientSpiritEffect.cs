using UnityEngine;
using Unity.Netcode;
using System;

[CreateAssetMenu(fileName = "AncientSpiritEffect", menuName = "Cards/Effects/AncientSpirit")]
public class AncientSpiritEffect : CardEffect
{
    public GameObject ancientSpiritPrefab;

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        // Use the generic UnitPlacer for placement
        UnitPlacer.Instance.StartPlacing(this, onPlacementConfirmed);
    }

    public override GameObject GetPreviewPrefab()
    {
        return ancientSpiritPrefab;
    }

    // This version of ApplyEffect is required by the abstract base class CardEffect.
    // For AncientSpirit, which requires placement, this method should ideally not be called directly.
    // It's implemented here to satisfy the abstract requirement.
    public override void ApplyEffect(ulong playerId)
    {
        // If this card MUST be placed, log an error or decide on a default placement behavior.
        Debug.LogError("[AncientSpiritEffect] ApplyEffect(ulong playerId) was called directly, but Ancient Spirit requires placement. This is likely an error in the calling code.");
        // Optionally, you could call the placement version with a default/invalid position to signify an issue,
        // or simply do nothing further to prevent unintended behavior.
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (ancientSpiritPrefab == null)
        {
            Debug.LogError("[AncientSpiritEffect] Ancient Spirit Prefab not assigned!");
            return;
        }

        GameObject spirit = Instantiate(ancientSpiritPrefab, position, Quaternion.identity);
        NetworkObject netObj = spirit.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[AncientSpiritEffect] AncientSpirit network object spawned for player {playerId}.");
        }
        else
        {
            Debug.LogWarning("[AncientSpiritEffect] AncientSpirit prefab is missing NetworkObject component!");
            GameObject.Destroy(spirit);
        }
    }
}