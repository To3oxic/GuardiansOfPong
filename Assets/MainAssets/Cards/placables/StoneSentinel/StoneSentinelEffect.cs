using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "StoneSentinelEffect", menuName = "Cards/Effects/StoneSentinel")]
public class StoneSentinelEffect : CardEffect
{
    public GameObject stoneSentinelPrefab;
    public ParticleSystem cardParticleEffectPrefab;

    public override bool RequiresPlacement() => true;
    public override float GetEffectRadius() => 2f;
    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
    }

    public override GameObject GetPreviewPrefab()
    {
        if (stoneSentinelPrefab == null)
        {
            return null;
        }
        return stoneSentinelPrefab;
    }

    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
        // This version is not used for placeable effects that require position/direction
    }

    public override GameObject ApplyEffect(ulong playerId, Vector3 position, Vector3 direction, CardData cardData, int cardLevel = 1)
    {
        if (!NetworkManager.Singleton.IsServer) return null;

        if (GameManager.Instance == null)
        {
            return null;
        }

        if (stoneSentinelPrefab == null)
        {
            return null;
        }

        // Use the direction from placement for the initial orientation of the sentinel
        Quaternion initialRotation = Quaternion.LookRotation(direction);
        // Direction is provided by the drag & drop system based on paddle position
        position.y = 0;
        GameObject sentinelInstance = GameObject.Instantiate(stoneSentinelPrefab, position, initialRotation);

        NetworkObject netObj = sentinelInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);

            // Apply leveled stats to the unit
            BaseUnitController unit = sentinelInstance.GetComponent<BaseUnitController>();
            if (unit != null && cardData != null)
            {
                unit.SetAttackDamage(cardData.GetDamageAtLevel(cardLevel));
                unit.SetMaxHP(cardData.GetHPAtLevel(cardLevel));
            }

            // Optional: Initialize the controller if needed immediately after spawn (server-side)
            StoneSentinelController controller = sentinelInstance.GetComponent<StoneSentinelController>();
            if (controller != null)
            {
                // Pass the owner's ID, or let the controller determine its target some other way
                // controller.InitializeServerRpc(playerId); // Example if initialization needs client ID
            }

            // Animate the spawned unit
            AnimateSpawnedUnit(sentinelInstance, position);

            return sentinelInstance;
        }
        else
        {
            GameObject.Destroy(sentinelInstance); // Clean up unspawnable instance
            return null;
        }
    }
}
