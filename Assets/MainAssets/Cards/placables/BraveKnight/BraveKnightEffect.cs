using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "BraveKnightEffect", menuName = "Cards/Effects/BraveKnight")]
public class BraveKnightEffect : CardEffect
{
    public GameObject braveKnightPrefab;
    public ParticleSystem cardParticleEffectPrefab;

    public override bool RequiresPlacement() => true;
    public override float GetEffectRadius() => 2f;
    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {

    }

    public override GameObject GetPreviewPrefab()
    {
        if (braveKnightPrefab == null)
        {
            Debug.LogError("[BraveKnightEffect] braveKnightPrefab is not assigned!");
            return null;
        }
        return braveKnightPrefab;
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
            Debug.LogError("[BraveKnightEffect] GameManager.Instance is null.");
            return null;
        }

        if (braveKnightPrefab == null)
        {
            Debug.LogError("[BraveKnightEffect] braveKnightPrefab is not assigned! Cannot apply effect.");
            return null;
        }

        // Use the direction from placement for the initial orientation
        Quaternion initialRotation = Quaternion.LookRotation(direction);

        // Ensure Y coordinate is always 0 for ground placement
        position.y = 0f;

        Debug.Log($"[BraveKnightEffect] Attempting to instantiate BraveKnight at position: {position}, rotation: {initialRotation.eulerAngles}, level: {cardLevel}");
        GameObject knightInstance = GameObject.Instantiate(braveKnightPrefab, position, initialRotation);

        NetworkObject netObj = knightInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[BraveKnightEffect] BraveKnight network object spawned for player {playerId}.");

            // Apply leveled stats to the unit
            BaseUnitController unit = knightInstance.GetComponent<BaseUnitController>();
            if (unit != null && cardData != null)
            {
                unit.SetAttackDamage(cardData.GetDamageAtLevel(cardLevel));
                unit.SetMaxHP(cardData.GetHPAtLevel(cardLevel));
            }

            // Animate the spawned unit
            AnimateSpawnedUnit(knightInstance, position);

            return knightInstance;
        }
        else
        {
            Debug.LogWarning($"[BraveKnightEffect] BraveKnight prefab is missing NetworkObject component!");
            GameObject.Destroy(knightInstance);
            return null;
        }
    }
}
