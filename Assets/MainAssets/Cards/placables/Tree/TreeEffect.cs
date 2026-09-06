using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Netcode;

[CreateAssetMenu(fileName = "TreeEffect", menuName = "Cards/Effects/Tree")]
public class TreeEffect : CardEffect
{
    public List<GameObject> treePrefabs = new List<GameObject>();
    public ParticleSystem cardParticleEffectPrefab;

    public override bool RequiresPlacement() => true;
    public override float GetEffectRadius() => 1f;
    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
    }

    public override GameObject GetPreviewPrefab()
    {
        if (treePrefabs == null || treePrefabs.Count == 0)
        {
            return null;
        }
        int randomIndex = UnityEngine.Random.Range(0, treePrefabs.Count);
        return treePrefabs[randomIndex];
    }

    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
    }

    public override GameObject ApplyEffect(ulong playerId, Vector3 position, Vector3 direction, CardData cardData, int cardLevel = 1)
    {
        if (!NetworkManager.Singleton.IsServer) return null;

        if (GameManager.Instance == null)
        {
            return null;
        }

        if (treePrefabs == null || treePrefabs.Count == 0)
        {
            return null;
        }

        int randomIndex = UnityEngine.Random.Range(0, treePrefabs.Count);
        GameObject selectedTreePrefab = treePrefabs[randomIndex];

        if (selectedTreePrefab == null)
        {
            return null;
        }

        // Ensure Y coordinate is always 0 for ground placement
        position.y = 0f;

        GameObject tree = GameObject.Instantiate(selectedTreePrefab, position, Quaternion.identity);

        var netObj = tree.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(playerId);

            // Apply leveled stats to the unit
            BaseUnitController unit = tree.GetComponent<BaseUnitController>();
            if (unit != null && cardData != null)
            {
                unit.SetAttackDamage(cardData.GetDamageAtLevel(cardLevel));
                unit.SetMaxHP(cardData.GetHPAtLevel(cardLevel));
            }

            // Animate the spawned unit
            AnimateSpawnedUnit(tree, position);

            return tree;
        }
        else
        {
            GameObject.Destroy(tree);
            return null;
        }
    }
}
