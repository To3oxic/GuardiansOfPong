using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "AngryBearEffect", menuName = "Cards/Effects/AngryBear")]
public class AngryBearEffect : CardEffect
{
    public GameObject angryBearPrefab;
    public ParticleSystem cardParticleEffectPrefab;

    public override bool RequiresPlacement() => true;
    public override float GetEffectRadius() => 2f;
    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {

    }

    public override GameObject GetPreviewPrefab()
    {
        if (angryBearPrefab == null)
        {
            Debug.LogError("[AngryBearEffect] angryBearPrefab is not assigned!");
            return null;
        }
        return angryBearPrefab;
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
            Debug.LogError("[AngryBearEffect] GameManager.Instance is null.");
            return null;
        }

        if (angryBearPrefab == null)
        {
            Debug.LogError("[AngryBearEffect] angryBearPrefab is not assigned! Cannot apply effect.");
            return null;
        }

        // Use the direction from placement for the initial orientation
        Quaternion initialRotation = Quaternion.LookRotation(direction);

        // Ensure Y coordinate is always 0 for ground placement
        position.y = 0f;

        Debug.Log($"[AngryBearEffect] Attempting to instantiate AngryBear at position: {position}, rotation: {initialRotation.eulerAngles}, level: {cardLevel}");
        GameObject bearInstance = GameObject.Instantiate(angryBearPrefab, position, initialRotation);

        NetworkObject netObj = bearInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[AngryBearEffect] AngryBear network object spawned for player {playerId}.");

            // Apply leveled stats to the unit
            BaseUnitController unit = bearInstance.GetComponent<BaseUnitController>();
            if (unit != null && cardData != null)
            {
                unit.SetAttackDamage(cardData.GetDamageAtLevel(cardLevel));
                unit.SetMaxHP(cardData.GetHPAtLevel(cardLevel));
            }

            // Animate the spawned unit
            AnimateSpawnedUnit(bearInstance, position);

            return bearInstance;
        }
        else
        {
            Debug.LogWarning($"[AngryBearEffect] AngryBear prefab is missing NetworkObject component!");
            GameObject.Destroy(bearInstance);
            return null;
        }
    }
}