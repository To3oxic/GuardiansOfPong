using UnityEngine;
using System;
using Unity.Netcode;
[CreateAssetMenu(fileName = "StoneSentinelEffect", menuName = "Cards/Effects/StoneSentinel")]
public class StoneSentinelEffect : CardEffect
{
    public GameObject sentinelPrefab;
    public GameObject sentinelPreviewPrefab;

    public override bool RequiresPlacement() => true;

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {

    }

    public override void ApplyEffect(ulong playerId)
    {

    }
    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        GameObject sentinel = Instantiate(sentinelPrefab, position, Quaternion.LookRotation(direction));
        sentinel.GetComponent<NetworkObject>().SpawnWithOwnership(playerId);
    }
}
