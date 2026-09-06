using UnityEngine;

public class PyroGuardianController : GuardiansController
{
    public override void OnNetworkSpawn()
    {
        // Set Pyro-specific defaults BEFORE calling base

        base.OnNetworkSpawn();
        // Any other Pyro-specific initialization
    }
}
