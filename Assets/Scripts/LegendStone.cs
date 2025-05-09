using Unity.Netcode;
using UnityEngine;

public class LegendStone : NetworkBehaviour
{
    // Set this flag in the Inspector to indicate which legend stone it is.
    public bool isTopStone;
    public ulong ownerClientId;

    private void OnTriggerEnter(Collider other)
    {

    }
}
