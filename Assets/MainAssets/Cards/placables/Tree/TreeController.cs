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
        }

        if (IsClient)
        {
        }
    }

    public override void TakeDamage(int amount)
    {
        base.TakeDamage(amount); // Use parent class implementation

        if (currentHP.Value <= 0)
        {
        }
    }
}