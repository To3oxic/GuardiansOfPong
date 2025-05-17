using UnityEngine;
using Unity.Netcode;

public class TreeController : NetworkBehaviour, IPlaceable
{
    // This Tree is a simple obstacle. It doesn't take damage or have complex logic for now.
    // It implements IPlaceable so the BallController can identify it for bounce physics.

    [Header("Stats")]
    public int maxHP = 100; // Default max HP, can be set in Inspector

    // NetworkVariable to sync current health across the network
    // ReadPermission.Everyone allows clients to read HP (e.g., for a health bar)
    private NetworkVariable<int> currentHP = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // Good practice to call base

        if (IsServer)
        {
            currentHP.Value = maxHP;
            Debug.Log($"[TreeController] Tree spawned on server with {currentHP.Value} HP.");
        }

        if (IsClient)
        {
            // Optional: Subscribe to HP changes if you want to update a health bar on clients
            // currentHP.OnValueChanged += OnCurrentHPChanged;
            // UpdateHealthBar(currentHP.Value, maxHP); // Initial health bar update
            Debug.Log($"[TreeController] Tree spawned on client. Initial HP from server: {currentHP.Value}");
        }
    }

    // Optional: Example for client-side health bar updates
    // private void OnCurrentHPChanged(int previousValue, int newValue)
    // {
    //     Debug.Log($"[TreeController] Client: HP changed from {previousValue} to {newValue}");
    //     UpdateHealthBar(newValue, maxHP);
    // }
    // private void UpdateHealthBar(int current, int max) { /* Your health bar UI logic here */ }

    public void TakeDamage(int amount)
    {
        if (!IsServer) return; // Damage logic should only run on the server

        if (currentHP.Value <= 0) return; // Already destroyed, do nothing

        currentHP.Value -= amount;
        Debug.Log($"[TreeController] Tree (ID: {NetworkObjectId}) took {amount} damage, HP is now {currentHP.Value}");

        if (currentHP.Value <= 0)
        {
            currentHP.Value = 0; // Clamp HP to 0
            Debug.Log($"[TreeController] Tree (ID: {NetworkObjectId}) destroyed!");

            // Optional: Trigger a destruction visual/audio effect via ClientRpc before despawning
            // PlayDestructionEffectClientRpc(); 

            // Despawn the tree NetworkObject
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Despawn(true); // true to destroy the GameObject across the network
            }
        }
    }

    // Optional: ClientRpc for destruction effects
    // [ClientRpc]
    // private void PlayDestructionEffectClientRpc()
    // {
    //     Debug.Log("[TreeController] Client: Playing tree destruction effect.");
    //     // Your particle effects, sound effects, etc., for when the tree is destroyed
    // }

    // Optional: Unsubscribe from events if you added subscriptions
    // public override void OnNetworkDespawn()
    // {
    //     if (IsClient)
    //     {
    //         currentHP.OnValueChanged -= OnCurrentHPChanged;
    //     }
    //     base.OnNetworkDespawn();
    // }

    // No Update() needed if the tree is static and has no active behavior.
}