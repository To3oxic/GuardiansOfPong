using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // Required for FixedString
using UnityEngine.EventSystems;

public class PaddleController : NetworkBehaviour, IHasHP
{
    [Header("Paddle Settings")]
    public float speed = 25f;
    public int maxHP = 2000;
    public int maxMana = 10;
    public float manaRegenRate = 1f; // Mana per second
    public float manaRegenInterval = 1f; // How often to regenerate (in seconds)
    [HideInInspector] public float paddleLength = 1.0f;
    public float currentDamageResistance = 1.0f; // 1.0 = full damage, 0.5 = half

    private Rigidbody paddleRigidbody;

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
    public NetworkVariable<bool> IsTransformed = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> currentMana = new NetworkVariable<float>(10);

    private float manaRegenTimer = 0f;
    private float boundary = 27f;
    private Vector3 lastSentPosition;
    private bool _isHPLowStateTriggered = false;
    public UnityEngine.UI.Button _leftButton;
    public UnityEngine.UI.Button _rightButton;

    // Mobile input state for continuous movement
    private bool _leftButtonPressed = false;
    private bool _rightButtonPressed = false;

    public string assignedLegendName; // Used for CastingLegend buffs, separate from LegendStone

    // This method is no longer the primary way to set the stone name before spawn.
    // GameManager now sets _tempInitialStoneNameForServer directly.
    // public void SetSelectedLegendStoneName(string stoneName) { ... } // Can be removed

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Setup physics to prevent sliding when ball hits paddle
        SetupPaddlePhysics(); if (IsServer)
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

            Debug.Log($"[PaddleController:{NetworkObjectId}] OnNetworkSpawn (Server). SelectedLegendStoneName: '{SelectedLegendStoneName.Value}'");
            currentHP.Value = maxHP;
            currentMana.Value = 4;

            // Legend stone spawning is now handled by GameManager in SpawnLegendStoneForPlayer()
            // The GameManager will create the legend stone and call Initialize() on it

            currentHP.OnValueChanged += ServerOnHpChanged;
            currentMana.OnValueChanged += OnManaChanged;
        }

        // All clients should listen to mana changes for UI updates
        if (IsClient)
        {
            currentMana.OnValueChanged += OnManaChanged;
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
            currentMana.OnValueChanged -= OnManaChanged;
        }

        if (IsClient)
        {
            currentMana.OnValueChanged -= OnManaChanged;
        }

        if (IsServer)
        {
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

    private void OnManaChanged(float previousValue, float newValue)
    {
        Debug.Log($"[PaddleController] Mana changed from {previousValue} to {newValue} on {(IsServer ? "Server" : "Client")} for paddle {gameObject.name}");

        // This callback will be triggered on all clients when mana changes
        // The GameManager's UpdatePlayerStatsUI will handle the UI update in its Update loop
    }

    void Start()
    {
        // Get buttons from GameManager
        if (GameManager.Instance != null)
        {
            _leftButton = GameManager.Instance.leftMoveButton;
            _rightButton = GameManager.Instance.rightMoveButton;

            // Setup button pointer down/up listeners for continuous movement
            if (_leftButton != null)
            {
                EventTrigger leftTrigger = _leftButton.GetComponent<EventTrigger>();
                if (leftTrigger == null)
                    leftTrigger = _leftButton.gameObject.AddComponent<EventTrigger>();

                // Pointer Down - start moving left
                EventTrigger.Entry pointerDownLeft = new EventTrigger.Entry();
                pointerDownLeft.eventID = EventTriggerType.PointerDown;
                pointerDownLeft.callback.AddListener((data) => OnLeftButtonDown());
                leftTrigger.triggers.Add(pointerDownLeft);

                // Pointer Up - stop moving left
                EventTrigger.Entry pointerUpLeft = new EventTrigger.Entry();
                pointerUpLeft.eventID = EventTriggerType.PointerUp;
                pointerUpLeft.callback.AddListener((data) => OnLeftButtonUp());
                leftTrigger.triggers.Add(pointerUpLeft);
            }

            if (_rightButton != null)
            {
                EventTrigger rightTrigger = _rightButton.GetComponent<EventTrigger>();
                if (rightTrigger == null)
                    rightTrigger = _rightButton.gameObject.AddComponent<EventTrigger>();

                // Pointer Down - start moving right
                EventTrigger.Entry pointerDownRight = new EventTrigger.Entry();
                pointerDownRight.eventID = EventTriggerType.PointerDown;
                pointerDownRight.callback.AddListener((data) => OnRightButtonDown());
                rightTrigger.triggers.Add(pointerDownRight);

                // Pointer Up - stop moving right
                EventTrigger.Entry pointerUpRight = new EventTrigger.Entry();
                pointerUpRight.eventID = EventTriggerType.PointerUp;
                pointerUpRight.callback.AddListener((data) => OnRightButtonUp());
                rightTrigger.triggers.Add(pointerUpRight);
            }
        }

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

    /// <summary>
    /// Called when left button pointer down
    /// </summary>
    private void OnLeftButtonDown()
    {
        if (!IsOwner) return;
        _leftButtonPressed = true;
    }

    /// <summary>
    /// Called when left button pointer up
    /// </summary>
    private void OnLeftButtonUp()
    {
        if (!IsOwner) return;
        _leftButtonPressed = false;
    }

    /// <summary>
    /// Called when right button pointer down
    /// </summary>
    private void OnRightButtonDown()
    {
        if (!IsOwner) return;
        _rightButtonPressed = true;
    }

    /// <summary>
    /// Called when right button pointer up
    /// </summary>
    private void OnRightButtonUp()
    {
        if (!IsOwner) return;
        _rightButtonPressed = false;
    }

    /// <summary>
    /// Called when left button is pressed
    /// </summary>
    public void MoveLeft()
    {
        if (!IsOwner) return;

        float move = -speed * Time.deltaTime;
        Vector3 newPosition = transform.position + new Vector3(move, 0, 0);
        float clampedX = Mathf.Clamp(newPosition.x, -boundary + paddleLength / 2f, boundary - paddleLength / 2f);
        newPosition.x = clampedX;

        transform.position = newPosition;

        if (Vector3.Distance(lastSentPosition, newPosition) > 0.1f)
        {
            lastSentPosition = newPosition;
            UpdatePaddlePositionServerRpc(newPosition);
        }
    }

    /// <summary>
    /// Called when right button is pressed
    /// </summary>
    public void MoveRight()
    {
        if (!IsOwner) return;

        float move = speed * Time.deltaTime;
        Vector3 newPosition = transform.position + new Vector3(move, 0, 0);
        float clampedX = Mathf.Clamp(newPosition.x, -boundary + paddleLength / 2f, boundary - paddleLength / 2f);
        newPosition.x = clampedX;

        transform.position = newPosition;

        if (Vector3.Distance(lastSentPosition, newPosition) > 0.1f)
        {
            lastSentPosition = newPosition;
            UpdatePaddlePositionServerRpc(newPosition);
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

    private void SetupPaddlePhysics()
    {
        // Get or add Rigidbody component
        paddleRigidbody = GetComponent<Rigidbody>();
        if (paddleRigidbody == null)
        {
            paddleRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        // Set to kinematic to prevent physics forces from affecting the paddle
        // This stops sliding when ball hits, but keeps collision detection
        paddleRigidbody.isKinematic = true;
        paddleRigidbody.useGravity = false;

        // Ensure the paddle has a collider for ball bouncing
        Collider paddleCollider = GetComponent<Collider>();
        if (paddleCollider == null)
        {
            // Add a box collider if none exists
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            // You may need to adjust these dimensions based on your paddle mesh
            boxCollider.size = new Vector3(paddleLength, 0.5f, 0.2f);
        }

        Debug.Log($"[PaddleController] Physics setup complete - Kinematic: {paddleRigidbody.isKinematic}");
    }

    private void Update()
    {
        if (!IsOwner) return;

        float input = Input.GetAxis("Horizontal");

        // Handle mobile button input (continuous while held)
        if (_leftButtonPressed)
            input = -1f;
        else if (_rightButtonPressed)
            input = 1f;

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
            // Mana regeneration
            manaRegenTimer += Time.deltaTime;
            if (manaRegenTimer >= manaRegenInterval)
            {
                manaRegenTimer = 0f;
                if (currentMana.Value < maxMana)
                {
                    float newMana = Mathf.Min(currentMana.Value + manaRegenRate, maxMana);
                    currentMana.Value = newMana;
                    Debug.Log($"[PaddleController] {gameObject.name} mana regenerated to {currentMana.Value}/{maxMana}");
                }
            }

        }

        // Test mana consumption (remove this in production)
        if (IsOwner && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log($"[PaddleController] Testing mana consumption. Current mana: {currentMana.Value}");
            // This would be called when actually playing a card through the card system
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

        // Apply transformation damage resistance ONLY if it is actual damage (positive)
        if (IsTransformed.Value && damage > 0f)
        {
            damage *= currentDamageResistance;
        }

        float previousHp = currentHP.Value;
        // Clamp currentHP.Value between 0 and maxHP
        currentHP.Value = Mathf.Clamp(currentHP.Value - damage, 0f, maxHP);

        if (damage > 0)
        {
            Debug.Log($"[PaddleController] {gameObject.name} (Owner: {OwnerClientId}) HP reduced by {damage}. New HP: {currentHP.Value}");
        }
        else if (damage < 0)
        {
            Debug.Log($"[PaddleController] {gameObject.name} (Owner: {OwnerClientId}) HP healed by {-damage}. New HP: {currentHP.Value}");
        }

        // Check for 50% HP threshold crossing for legend stone transformation
        float hpPercentage = GetHPPercentage();
        float previousHpPercentage = maxHP > 0 ? previousHp / maxHP : 0f;

        if (previousHpPercentage >= 0.5f && hpPercentage < 0.5f)
        {
            Debug.Log($"[PaddleController] {gameObject.name} HP dropped below 50%! Server notifying GameManager about legend stone transformation.");
            // Server directly calls GameManager to handle transformation
            GameManager.Instance.HandleLegendStoneTransformation(OwnerClientId);
        }

        // Notify all clients about the HP change for UI updates
        OnHPChangedClientRpc(OwnerClientId, currentHP.Value, maxHP, damage);

        // ServerOnHpChanged will handle the 50% HP logic due to the NetworkVariable subscription.

        if (currentHP.Value <= 0 && previousHp > 0) // Ensure defeat is triggered only once
        {
            Debug.Log($"[PaddleController] {gameObject.name} (Owner: {OwnerClientId}) HP reached 0. Triggering paddle defeat.");
            GameManager.Instance.TriggerPaddleDefeat(OwnerClientId); // Assuming TriggerPaddleDefeat handles despawn or game over
        }
    }

    [ClientRpc]
    private void OnHPChangedClientRpc(ulong paddleOwnerClientId, float newHP, float maxHP, float damageDealt)
    {
        Debug.Log($"[PaddleController] HP Change RPC received - Owner: {paddleOwnerClientId}, HP: {newHP}/{maxHP}, Damage: {damageDealt}");

        // Notify GameManager about the HP change for UI updates
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnPaddleHPChanged(paddleOwnerClientId, newHP, maxHP, damageDealt);
        }

        // Play green healing visual effect if HP was increased (healing has negative damageDealt)
        if (damageDealt < 0f)
        {
            StartCoroutine(PlayHealVisualEffectCo());
        }
    }

    private System.Collections.IEnumerator PlayHealVisualEffectCo()
    {
        // 1. Create the particle system for rising green lines
        GameObject psObj = new GameObject("HealParticles");
        psObj.transform.SetParent(this.transform, false);
        psObj.transform.localPosition = new Vector3(0f, -0.5f, 0f); // Start slightly below the paddle
        psObj.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();

        // Setup Main module
        var main = ps.main;
        main.duration = 1.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.15f); // Width of lines
        main.startSizeY = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);  // Length of stretched lines
        main.startColor = new Color(0.2f, 1.0f, 0.4f, 0.7f); // Beautiful semi-transparent neon green
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;

        // Setup Shape module: box shape along the length of the paddle
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8.0f, 0.2f, 0.8f);

        // Setup Velocity over lifetime (rise upwards along positive Y axis)
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(8f, 16f); // Upwards movement
        velocity.x = 0f;
        velocity.z = 0f;

        // Setup Color Over Lifetime (fade out)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color(0.2f, 1.0f, 0.4f), 0.0f), 
                new GradientColorKey(new Color(0.1f, 0.8f, 0.2f), 1.0f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1.0f, 0.0f), 
                new GradientAlphaKey(0.0f, 1.0f) 
            }
        );
        colorOverLifetime.color = grad;

        // Setup Renderer to support stretched lines
        var psr = psObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.renderMode = ParticleSystemRenderMode.VerticalBillboard; // Perfect for vertical rising lines
            
            Shader particleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (particleShader != null)
            {
                psr.material = new Material(particleShader);
            }
        }

        // Configure burst emission
        var emission = ps.emission;
        emission.rateOverTime = 0;
        var burst = new ParticleSystem.Burst(0.0f, 35);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        // Play the particles!
        ps.Play();

        // 2. Animate the paddle glow
        Renderer paddleRenderer = GetComponent<Renderer>();
        if (paddleRenderer != null)
        {
            // Use .material to get/create an instance of the material so we don't leak to the project asset
            Material paddleMat = paddleRenderer.material;
            if (paddleMat != null)
            {
                Color originalBaseColor = paddleMat.HasProperty("_BaseColor") ? paddleMat.GetColor("_BaseColor") : Color.white;
                Color originalRimColor = paddleMat.HasProperty("_RimColor") ? paddleMat.GetColor("_RimColor") : Color.clear;

                Color targetBaseColor = new Color(0.3f, 1.0f, 0.5f); // Soft bright green
                Color targetRimColor = new Color(0.1f, 1.0f, 0.3f) * 2f; // Highly intense rim glow

                float elapsed = 0f;
                float duration = 1.2f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / duration;

                    // Quick spike up, smooth fade down
                    float t = 0f;
                    if (progress < 0.15f)
                    {
                        t = progress / 0.15f; // transition to green
                    }
                    else
                    {
                        t = 1.0f - ((progress - 0.15f) / 0.85f); // transition back to original
                    }

                    if (paddleMat.HasProperty("_BaseColor"))
                    {
                        paddleMat.SetColor("_BaseColor", Color.Lerp(originalBaseColor, targetBaseColor, t));
                    }
                    if (paddleMat.HasProperty("_RimColor"))
                    {
                        paddleMat.SetColor("_RimColor", Color.Lerp(originalRimColor, targetRimColor, t));
                    }

                    yield return null;
                }

                // Restore original values
                if (paddleMat.HasProperty("_BaseColor")) paddleMat.SetColor("_BaseColor", originalBaseColor);
                if (paddleMat.HasProperty("_RimColor")) paddleMat.SetColor("_RimColor", originalRimColor);
            }
        }

        Destroy(psObj, 2.0f);
    }

    #region IHasHP Implementation

    public float GetCurrentHP()
    {
        return currentHP.Value;
    }

    public float GetMaxHP()
    {
        return maxHP;
    }

    public float GetHPPercentage()
    {
        if (maxHP <= 0) return 0f;
        return currentHP.Value / maxHP;
    }

    public bool IsAlive()
    {
        return currentHP.Value > 0;
    }

    #endregion
}