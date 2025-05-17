using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "AngryBearEffect", menuName = "Cards/Effects/AngryBear")]
public class AngryBearEffect : CardEffect
{
    public GameObject angryBearPrefab;

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        // Use the generic UnitPlacer
        UnitPlacer.Instance.StartPlacing(this, onPlacementConfirmed);
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

    public override void ApplyEffect(ulong playerId)
    {
        // This version is not used for placeable effects that require position/direction
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[AngryBearEffect] GameManager.Instance is null.");
            return;
        }

        if (angryBearPrefab == null)
        {
            Debug.LogError("[AngryBearEffect] angryBearPrefab is not assigned! Cannot apply effect.");
            return;
        }

        // Use the direction from placement for the initial orientation
        Quaternion initialRotation = Quaternion.LookRotation(direction);

        Debug.Log($"[AngryBearEffect] Attempting to instantiate AngryBear at position: {position}, rotation: {initialRotation.eulerAngles}");
        GameObject bearInstance = GameObject.Instantiate(angryBearPrefab, position, initialRotation);

        NetworkObject netObj = bearInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[AngryBearEffect] AngryBear network object spawned for player {playerId}.");
        }
        else
        {
            Debug.LogWarning($"[AngryBearEffect] AngryBear prefab is missing NetworkObject component!");
            GameObject.Destroy(bearInstance);
        }
    }
}