using UnityEngine;

public class BoulderGuardianController : GuardiansController
{
    public override void OnNetworkSpawn()
    {
        // Set Boulder-specific defaults BEFORE calling base
        attackRange = 50f;
        projectileDamage = 50;
        base.OnNetworkSpawn();
        // Any other Boulder-specific initialization
    }
}
