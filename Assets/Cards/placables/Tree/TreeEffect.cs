using UnityEngine;
using System;
using Unity.Netcode;

[CreateAssetMenu(fileName = "TreeEffect", menuName = "Cards/Effects/Tree")]
public class TreeEffect : CardEffect
{
    public GameObject treePrefab;

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        TreePlacer.Instance.StartPlacing(onPlacementConfirmed);
    }

    public override void ApplyEffect(ulong playerId)
    {
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (GameManager.Instance != null && treePrefab != null)
        {
            GameObject tree = GameObject.Instantiate(treePrefab, position, Quaternion.identity);
            Debug.Log("[Effect] position" + position);
            var netObj = tree.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnWithOwnership(playerId);
                Debug.Log("[Effect] Tree network object spawned.");
            }
            else
            {
                Debug.LogWarning("[Effect] Tree prefab is missing NetworkObject!");
            }
        }
    }

}
