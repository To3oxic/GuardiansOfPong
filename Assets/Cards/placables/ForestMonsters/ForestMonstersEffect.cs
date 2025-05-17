using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "ForestMonstersEffect", menuName = "Cards/Effects/ForestMonsters")]
public class ForestMonstersEffect : CardEffect
{
    [System.Serializable]
    public class MonsterSpawnInfo
    {
        public GameObject prefab;
        public int count;
        public float spawnRadius = 2f;
        public bool isFlying = false;
    }

    [Header("Monster Spawn Settings")]
    public List<MonsterSpawnInfo> monstersToSpawn = new List<MonsterSpawnInfo>();
    public float spawnDelay = 0.2f;
    public float spreadRadius = 3f;

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        UnitPlacer.Instance.StartPlacing(this, onPlacementConfirmed);
    }

    public override GameObject GetPreviewPrefab()
    {
        // Return the first monster prefab as preview
        if (monstersToSpawn != null && monstersToSpawn.Count > 0 && monstersToSpawn[0].prefab != null)
        {
            return monstersToSpawn[0].prefab;
        }
        Debug.LogError("[ForestMonstersEffect] No monster prefabs assigned!");
        return null;
    }

    public override void ApplyEffect(ulong playerId)
    {
        // This version is not used for placeable effects that require position/direction
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[ForestMonstersEffect] GameManager.Instance is null.");
            return;
        }

        if (monstersToSpawn == null || monstersToSpawn.Count == 0)
        {
            Debug.LogError("[ForestMonstersEffect] No monsters configured to spawn!");
            return;
        }

        // Start spawning monsters
        GameManager.Instance.StartCoroutine(SpawnMonstersCoroutine(playerId, position));
    }

    private System.Collections.IEnumerator SpawnMonstersCoroutine(ulong ownerId, Vector3 centerPosition)
    {
        foreach (var monsterInfo in monstersToSpawn)
        {
            for (int i = 0; i < monsterInfo.count; i++)
            {
                // Calculate random position within spread radius
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spreadRadius;
                Vector3 spawnPosition = centerPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

                // Spawn the monster
                GameObject monster = GameObject.Instantiate(monsterInfo.prefab, spawnPosition, Quaternion.identity);
                NetworkObject networkObject = monster.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.Spawn();
                    networkObject.ChangeOwnership(ownerId);
                    Debug.Log($"[ForestMonstersEffect] Spawned monster {monster.name} with owner ID: {ownerId}, NetworkObject ID: {networkObject.NetworkObjectId}");
                }

                // Set flying state if applicable
                if (monsterInfo.isFlying)
                {
                    if (monster.TryGetComponent<MonsterController>(out var controller))
                    {
                        controller.SetFlying(true);
                    }
                }

                yield return new WaitForSeconds(spawnDelay);
            }
        }
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position = default, Vector3 direction = default)
    {
        // Visual effects can be added here if needed
    }
}