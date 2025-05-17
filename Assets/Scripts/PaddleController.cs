using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // Required for FixedString

public class PaddleController : NetworkBehaviour
{
    [Header("Paddle Settings")]
    public float speed = 25f;
    public int maxHP = 2000;
    public int maxMana = 10;
    [HideInInspector] public float paddleLength = 1.0f;

    [Header("Legend Stone Setup - Dynamically Determined")]
    // public GameObject legendStonePrefab; // Obsolete: Now determined by LegendStoneData via SelectedLegendStoneName
    public float legendStoneOffsetZ = 2.0f; // Distance behind the paddle

    private LegendStoneController _legendStoneInstance;
    public LegendStoneController LegendStone => _legendStoneInstance;

    // NetworkVariable to store the name of the LegendStone chosen by the player via their deck.
    // This is set by the GameManager on the server before this paddle is fully spawned and active.
    public NetworkVariable<FixedString64Bytes> SelectedLegendStoneName =
        new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Temporary variable for diagnostic purposes - set by GameManager before spawn
    internal string _tempInitialStoneNameForServer;

    public NetworkVariable<float> currentHP = new NetworkVariable<float>(2000);
    public NetworkVariable<float> currentMana = new NetworkVariable<float>(10);

    private float manaRegenTimer = 0f;
    private float boundary = 27f;
    private Vector3 lastSentPosition;
    private bool _isHPLowStateTriggered = false;

    public string assignedLegendName; // Used for CastingLegend buffs, separate from LegendStone

    // This method is no longer the primary way to set the stone name before spawn.
    // GameManager now sets _tempInitialStoneNameForServer directly.
    // public void SetSelectedLegendStoneName(string stoneName) { ... } // Can be removed

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            if (!string.IsNullOrEmpty(_tempInitialStoneNameForServer))
            {
                SelectedLegendStoneName.Value = _tempInitialStoneNameForServer;
                Debug.Log($"[PaddleController:{NetworkObjectId}] OnNetworkSpawn (Server) - Successfully set SelectedLegendStoneName from _tempInitialStoneNameForServer: '{_tempInitialStoneNameForServer}'. NV is now: '{SelectedLegendStoneName.Value}'");
            }
            else
            {
                Debug.LogWarning($"[PaddleController:{NetworkObjectId}] OnNetworkSpawn (Server) - _tempInitialStoneNameForServer was empty or null when attempting to set NetworkVariable.");
            }

            Debug.Log($"[PaddleController:{NetworkObjectId}] OnNetworkSpawn (Server). Attempting to read SelectedLegendStoneName. Current Raw Value: '{SelectedLegendStoneName.Value}'");
            currentHP.Value = maxHP;
            currentMana.Value = 4;

            if (string.IsNullOrEmpty(SelectedLegendStoneName.Value.ToString()))
            {
                Debug.LogError($"[PaddleController:{NetworkObjectId}] SelectedLegendStoneName is still not set on spawn after trying temp var! Cannot spawn Legend Stone.");
                return;
            }
            else
            {
                LegendStoneData stoneData = DeckRegistry.Instance.GetLegendStoneByName(SelectedLegendStoneName.Value.ToString());
                if (stoneData != null && stoneData.legendStoneVisualPrefab != null)
                {
                    float zPos = transform.position.z - (transform.position.z > 0 ? -legendStoneOffsetZ : legendStoneOffsetZ);
                    Vector3 stonePosition = new Vector3(transform.position.x, transform.position.y, zPos);

                    GameObject stoneGO = Instantiate(stoneData.legendStoneVisualPrefab, stonePosition, Quaternion.identity);
                    NetworkObject stoneNetObj = stoneGO.GetComponent<NetworkObject>();
                    if (stoneNetObj == null)
                    {
                        Debug.LogError($"[PaddleController:{NetworkObjectId}] LegendStoneVisualPrefab '{stoneData.legendStoneVisualPrefab.name}' for '{stoneData.stoneName}' is missing a NetworkObject component! Destroying stone.");
                        Destroy(stoneGO);
                    }
                    else
                    {
                        stoneNetObj.SpawnWithOwnership(OwnerClientId);
                        _legendStoneInstance = stoneGO.GetComponent<LegendStoneController>();
                        if (_legendStoneInstance != null)
                        {
                            _legendStoneInstance.Initialize(this, OwnerClientId);
                            Debug.Log($"[PaddleController:{NetworkObjectId}] Spawned Legend Stone '{stoneData.stoneName}' for player {OwnerClientId} at {stonePosition}.");
                        }
                        else
                        {
                            Debug.LogError($"[PaddleController:{NetworkObjectId}] LegendStoneVisualPrefab '{stoneData.legendStoneVisualPrefab.name}' for '{stoneData.stoneName}' is missing LegendStoneController component! Destroying stone.");
                            Destroy(stoneGO);
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[PaddleController:{NetworkObjectId}] Could not find LegendStoneData or its visual prefab for name '{SelectedLegendStoneName.Value}'. Cannot spawn Legend Stone.");
                }
            }
            currentHP.OnValueChanged += ServerOnHpChanged;
        }

        // Buffs are applied on owner after data is potentially synced or available.
        if (IsOwner)
        {
            // Delay buff application slightly or wait for SelectedLegendStoneName to populate on client if needed for buffs.
            // For now, assuming Start() is late enough or buffs can handle a momentarily null stoneData if SelectedLegendStoneName isn't ready immediately on client.
            // GameManager sends deck data via ClientRpc, which populates DeckManager.Instance.CurrentDeck.
            // ApplyLegendStoneBuff now relies on DeckManager.Instance.GetSelectedStone() which should be populated.
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            currentHP.OnValueChanged -= ServerOnHpChanged;
            if (_legendStoneInstance != null && _legendStoneInstance.NetworkObject != null && _legendStoneInstance.NetworkObject.IsSpawned)
            {
                _legendStoneInstance.NetworkObject.Despawn();
            }
        }
        base.OnNetworkDespawn();
    }

    private void ServerOnHpChanged(float previousValue, float newValue)
    {
        if (!IsServer) return;

        // Check for HP below 50%
        if (newValue <= maxHP * 0.5f && !_isHPLowStateTriggered)
        {
            _isHPLowStateTriggered = true;
            if (_legendStoneInstance != null)
            {
                _legendStoneInstance.HandlePaddleHPLow();
            }
        }

        // Check for HP at or below 0 for paddle defeat (handled in ReduceHP)
    }

    void Start()
    {
        // Initial setup in OnNetworkSpawn is preferred for networked objects.
        // Client-side specific Start logic for owner can go here.
        if (IsOwner)
        {
            // Example: Apply buffs if they are purely client-side visual or input related
            // For stats affecting gameplay, they should be server-authoritative or synced.
            ApplyLegendStoneBuff();
            ApplyCastingLegendBuff();
        }
    }

    void ApplyLegendStoneBuff()
    {
        // Now uses DeckManager which should have the up-to-date selected stone from server RPC
        LegendStoneData stoneData = DeckManager.Instance?.GetSelectedStone();
        if (stoneData != null)
        {
            speed += stoneData.paddleSpeedBonus;
            // Apply other stats from stoneData, e.g., damageResistance
            Debug.Log($"[PaddleController] Applied buff from LegendStone: '{stoneData.stoneName}'. New speed: {speed}");
        }
        else
        {
            Debug.LogWarning($"[PaddleController] ApplyLegendStoneBuff: Could not get LegendStoneData from DeckManager. Is CurrentDeck populated?");
        }
    }

    void ApplyCastingLegendBuff()
    {
        var legend = DeckManager.Instance?.GetSelectedLegend();
        if (legend != null)
        {
            Debug.Log($"[PaddleController] Applying casting legend buffs. Damage Boost: {legend.damageBoostPercent}, Mana Cost Reduction: {legend.manaCostReduction}");
            // Future hooks could apply these to a player stats system
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        float input = Input.GetAxis("Horizontal");
        if (transform.position.z > 0) input = -input;
        float move = input * speed * Time.deltaTime;
        Vector3 newPosition = transform.position + new Vector3(move, 0, 0);

        float clampedX = Mathf.Clamp(newPosition.x, -boundary + paddleLength / 2f, boundary - paddleLength / 2f);
        newPosition.x = clampedX;

        transform.position = newPosition;

        // 🔁 Send only occasionally (e.g. every few frames or when changed significantly)
        if (Vector3.Distance(lastSentPosition, newPosition) > 0.1f)
        {
            lastSentPosition = newPosition;
            UpdatePaddlePositionServerRpc(newPosition);
        }

        if (IsServer)
        {
            manaRegenTimer += Time.deltaTime;
            if (manaRegenTimer >= 1f)
            {
                manaRegenTimer = 0f;
                if (currentMana.Value < maxMana)
                {
                    currentMana.Value += 1;
                    Debug.Log($"[PaddleController] {gameObject.name} mana regenerated to {currentMana.Value}");
                }
            }
        }
    }

    [ServerRpc]
    public void ModifyHPServerRpc(int amount)
    {
        if (!IsServer) return;
        currentHP.Value = Mathf.Max(0, currentHP.Value + amount);
    }

    [ServerRpc]
    void UpdatePaddlePositionServerRpc(Vector3 newPosition)
    {
        transform.position = newPosition;
        UpdatePaddlePositionClientRpc(newPosition);
    }

    [ServerRpc]
    public void ModifyManaServerRpc(int amount)
    {
        if (!IsServer) return;
        currentMana.Value = Mathf.Max(0, currentMana.Value + amount);
    }

    [ClientRpc]
    void UpdatePaddlePositionClientRpc(Vector3 newPosition)
    {
        if (!IsOwner)
        {
            transform.position = newPosition;
        }
    }

    public void ReduceHP(float damage)
    {
        if (!IsServer) return;

        float previousHp = currentHP.Value;
        currentHP.Value -= damage;
        Debug.Log($"[PaddleController] {gameObject.name} (Owner: {OwnerClientId}) HP reduced by {damage}. New HP: {currentHP.Value}");

        // ServerOnHpChanged will handle the 50% HP logic due to the NetworkVariable subscription.

        if (currentHP.Value <= 0 && previousHp > 0) // Ensure defeat is triggered only once
        {
            Debug.Log($"[PaddleController] {gameObject.name} (Owner: {OwnerClientId}) HP reached 0. Triggering paddle defeat.");
            GameManager.Instance.TriggerPaddleDefeat(OwnerClientId); // Assuming TriggerPaddleDefeat handles despawn or game over
        }
    }
}