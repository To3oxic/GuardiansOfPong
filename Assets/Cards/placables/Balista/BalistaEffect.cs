using UnityEngine;
using System;
using Unity.Netcode;

[CreateAssetMenu(fileName = "BalistaEffect", menuName = "Cards/Effects/Balista")]
public class BalistaEffect : CardEffect
{
    public GameObject balistaPrefab;

    public override bool RequiresPlacement() => true;

    public override void ApplyEffect(ulong playerId)
    {
    }

    public override void ApplyEffect(ulong playerId, Vector3 position, Vector3 direction)
    {
        if (GameManager.Instance != null && balistaPrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(direction);
            Debug.Log($"[BalistaEffect-Server] Instantiating Balista. Prefab: {balistaPrefab.name}, firepoint: {balistaPrefab.GetComponent<BalistaController>().firePoint.position}, Position: {position}, Rotation: {rotation.eulerAngles}, Direction: {direction}");
            GameObject balista = GameObject.Instantiate(balistaPrefab, position, rotation);
            var controller = balista.GetComponent<BalistaController>();
            if (controller != null)
            {
                controller.SetShootDirection(direction);
            }

            NetworkObject netObj = balista.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnWithOwnership(playerId);
            }

            Debug.Log("[Effect] Balista placed.");
        }
    }

    public override void StartPlacement(Action<Vector3, Vector3> onPlacementConfirmed)
    {
        UnitPlacer.Instance.StartPlacing(this, onPlacementConfirmed);
    }

    /// <summary>
    /// Provides the prefab to be used for the client-side placement preview.
    /// </summary>
    /// <returns>The Balista prefab.</returns>
    public override GameObject GetPreviewPrefab()
    {
        return balistaPrefab;
    }

    public override void ApplyVisualEffect(ulong playerId, Vector3 position, Vector3 direction)
    {

    }
}
