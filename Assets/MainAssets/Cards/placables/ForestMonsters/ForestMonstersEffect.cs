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
    public ParticleSystem cardParticleEffectPrefab;
    public float spreadRadius = 3f;

    public override bool RequiresPlacement() => true;
    public override float GetEffectRadius() => 2f;
    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
    }

    public override GameObject GetPreviewPrefab()
    {
        // Return the first monster prefab as preview
        if (monstersToSpawn != null && monstersToSpawn.Count > 0 && monstersToSpawn[0].prefab != null)
        {
            return monstersToSpawn[0].prefab;
        }
        return null;
    }

    public override void ApplyEffect(ulong playerId, CardData cardData, int cardLevel = 1)
    {
        // This version is not used for placeable effects that require position/direction
    }

    public override GameObject ApplyEffect(ulong playerId, Vector3 position, Vector3 direction, CardData cardData, int cardLevel = 1)
    {
        if (GameManager.Instance == null)
        {
            return null;
        }

        if (monstersToSpawn == null || monstersToSpawn.Count == 0)
        {
            return null;
        }

        // Start spawning monsters (returns first monster spawned as representative)
        GameManager.Instance.StartCoroutine(SpawnMonstersCoroutine(playerId, position, cardLevel, cardData));

        // Note: This returns null because monsters spawn asynchronously
        // For animation, we'd need to track the first spawned monster, but since multiple units spawn,
        // we'll keep the animation handling simple (just play particle effect)
        return null;
    }

    private System.Collections.IEnumerator SpawnMonstersCoroutine(ulong ownerId, Vector3 centerPosition, int cardLevel = 1, CardData cardData = null)
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

                    // Apply leveled stats to the unit
                    BaseUnitController unit = monster.GetComponent<BaseUnitController>();
                    if (unit != null && cardData != null)
                    {
                        unit.SetAttackDamage(cardData.GetDamageAtLevel(cardLevel));
                        unit.SetMaxHP(cardData.GetHPAtLevel(cardLevel));
                    }
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
        // Visual effects handled by individual unit spawn animations
    }
}