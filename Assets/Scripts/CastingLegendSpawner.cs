using UnityEngine;
using Unity.Netcode;
public class CastingLegendSpawner : NetworkBehaviour
{
    public CastingLegendData selectedLegend;
    public Transform bottomRightAnchor; // Assign this anchor in the scene (positioned bottom-right)
    private GameObject spawnedLegend;
    public override void OnNetworkSpawn()
    {
        if (IsOwner && selectedLegend != null && bottomRightAnchor != null)
        {
            SpawnLegendAvatarServerRpc();
        }
    }
    [ServerRpc]
    private void SpawnLegendAvatarServerRpc(ServerRpcParams rpcParams = default)
    {
        GameObject legend = Instantiate(selectedLegend.legendAvatarPrefab, bottomRightAnchor.position, Quaternion.identity);
        NetworkObject netObj = legend.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(OwnerClientId);
        }
        spawnedLegend = legend;
    }
    public GameObject GetSpawnedLegend()
    {
        return spawnedLegend;
    }
}
