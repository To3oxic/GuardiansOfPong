using UnityEngine;
using Unity.Netcode;

public class TreeController : BaseBuildingController
{
    // This Tree is a simple obstacle/building. 
    // It inherits HP system from BaseBuildingController
    // maxHP is inherited from BaseBuildingController - set it in the Inspector

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // Good practice to call base

        if (IsServer)
        {
            Debug.Log($"[TreeController] Tree spawned on server with {currentHP.Value} HP.");
        }

        if (IsClient)
        {
            Debug.Log($"[TreeController] Tree spawned on client. Initial HP from server: {currentHP.Value}");
        }
    }

    public override void TakeDamage(int amount)
    {
        base.TakeDamage(amount); // Use parent class implementation
        Debug.Log($"[TreeController] Tree (ID: {NetworkObjectId}) took {amount} damage, HP is now {currentHP.Value}");

        if (currentHP.Value <= 0)
        {
            Debug.Log($"[TreeController] Tree (ID: {NetworkObjectId}) destroyed!");
        }
    }
}