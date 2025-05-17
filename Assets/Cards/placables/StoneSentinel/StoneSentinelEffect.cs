using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "StoneSentinelEffect", menuName = "Cards/Effects/StoneSentinel")]
public class StoneSentinelEffect : CardEffect
{
    public GameObject stoneSentinelPrefab;

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        // Use the generic UnitPlacer
        UnitPlacer.Instance.StartPlacing(this, onPlacementConfirmed);
    }

    public override GameObject GetPreviewPrefab()
    {
        if (stoneSentinelPrefab == null)
        {
            Debug.LogError("[StoneSentinelEffect] stoneSentinelPrefab is not assigned!");
            return null;
        }
        return stoneSentinelPrefab;
    }

    public override void ApplyEffect(ulong playerId)
    {
        // This version is not used for placeable effects that require position/direction
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[StoneSentinelEffect] GameManager.Instance is null.");
            return;
        }

        if (stoneSentinelPrefab == null)
        {
            Debug.LogError("[StoneSentinelEffect] stoneSentinelPrefab is not assigned! Cannot apply effect.");
            return;
        }

        // Use the direction from placement for the initial orientation of the sentinel
        Quaternion initialRotation = Quaternion.LookRotation(direction);
        // If units should always face directly forward along Z or -Z based on side, 
        // UnitPlacer's current direction (Vector3.forward or Vector3.back) will handle this.
        position.y = 0;
        Debug.Log($"[StoneSentinelEffect] Attempting to instantiate StoneSentinel: {stoneSentinelPrefab.name} at position: {position}, rotation: {initialRotation.eulerAngles}");
        GameObject sentinelInstance = GameObject.Instantiate(stoneSentinelPrefab, position, initialRotation);

        NetworkObject netObj = sentinelInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[StoneSentinelEffect] StoneSentinel ({stoneSentinelPrefab.name}) network object spawned for player {playerId}.");

            // Optional: Initialize the controller if needed immediately after spawn (server-side)
            StoneSentinelController controller = sentinelInstance.GetComponent<StoneSentinelController>();
            if (controller != null)
            {
                // Pass the owner's ID, or let the controller determine its target some other way
                // controller.InitializeServerRpc(playerId); // Example if initialization needs client ID
            }
        }
        else
        {
            Debug.LogWarning($"[StoneSentinelEffect] StoneSentinel prefab ({stoneSentinelPrefab.name}) is missing NetworkObject component!");
            GameObject.Destroy(sentinelInstance); // Clean up unspawnable instance
        }
    }
}
