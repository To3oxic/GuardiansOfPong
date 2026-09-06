using UnityEngine;

public class BoulderGuardianController : GuardiansController
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Any other Boulder-specific initialization
    }
}
