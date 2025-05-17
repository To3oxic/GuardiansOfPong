using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "TreeEffect", menuName = "Cards/Effects/Tree")]
public class TreeEffect : CardEffect
{
    public List<GameObject> treePrefabs = new List<GameObject>();

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        UnitPlacer.Instance.StartPlacing(this, onPlacementConfirmed);
    }

    public override GameObject GetPreviewPrefab()
    {
        if (treePrefabs == null || treePrefabs.Count == 0)
        {
            Debug.LogError("[TreeEffect] No treePrefabs assigned or list is empty!");
            return null;
        }
        int randomIndex = UnityEngine.Random.Range(0, treePrefabs.Count);
        return treePrefabs[randomIndex];
    }

    public override void ApplyEffect(ulong playerId)
    {
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[TreeEffect] GameManager.Instance is null.");
            return;
        }

        if (treePrefabs == null || treePrefabs.Count == 0)
        {
            Debug.LogError("[TreeEffect] No treePrefabs assigned or list is empty! Cannot apply effect.");
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, treePrefabs.Count);
        GameObject selectedTreePrefab = treePrefabs[randomIndex];

        if (selectedTreePrefab == null)
        {
            Debug.LogError($"[TreeEffect] Selected tree prefab at index {randomIndex} is null!");
            return;
        }

        Debug.Log($"[TreeEffect] Attempting to instantiate tree: {selectedTreePrefab.name} at position: {position}");
        GameObject tree = GameObject.Instantiate(selectedTreePrefab, position, Quaternion.identity);

        var netObj = tree.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);
            Debug.Log($"[TreeEffect] Tree ({selectedTreePrefab.name}) network object spawned for player {playerId}.");
        }
        else
        {
            Debug.LogWarning($"[TreeEffect] Tree prefab ({selectedTreePrefab.name}) is missing NetworkObject!");
            GameObject.Destroy(tree);
        }
    }
}
