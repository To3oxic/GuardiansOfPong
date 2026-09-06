using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Serializable classes for loot box response parsing
/// </summary>
[System.Serializable]
public class LootBoxResponse
{
    public bool success;
    public string message;
    public int boxLevel;
    public RewardData rewards;
    public InventoryData inventory;
}

[System.Serializable]
public class RewardData
{
    public CardReward[] cards;
    public int goldGranted;
}

[System.Serializable]
public class CardReward
{
    public string cardId;
    public string cardName;
    public string rarity;
    public int copies;
}

[System.Serializable]
public class InventoryData
{
    public int gems;
    public int gold;
}

/// <summary>
/// Handles loot box interaction on main menu
/// - Click loot box to zoom in with camera animation
/// - Shows UI with Open/Leave buttons
/// - Leave button zooms camera back out
/// </summary>
public class LootBoxInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject lootBoxGameObject; // The loot box in the scene
    [SerializeField] private Canvas lootBoxUICanvas; // Canvas containing the loot box UI
    [SerializeField] private Button openLootBoxButton;
    [SerializeField] private Button leaveLootBoxButton;
    [SerializeField] private TextMeshProUGUI lootBoxLevelText;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private MenuCubeRotator menuCubeRotator; // Cube rotator to disable during loot box view
    [SerializeField] private LootBoxRewardUI rewardUI; // UI to show loot box rewards

    [Header("Camera Settings")]
    [SerializeField] private float cameraMoveSpeed = 5f; // Lerp speed for camera movement

    // Target position and rotation when looking at loot box
    private Vector3 lootBoxCameraPos = new Vector3(0.32f, 21.58f, 0.00f);
    private Quaternion lootBoxCameraRot = new Quaternion(0.456160873f, 0.169830292f, -0.0971911773f, 0.868117988f);

    // Original camera position/rotation (stored on start)
    private Vector3 originalCameraPos;
    private Quaternion originalCameraRot;
    private float originalCameraSize;

    // Current state
    private bool isLootBoxViewActive = false;
    private bool freezeCameraPosition = false;
    private Vector3 frozenCameraPos;
    private Quaternion frozenCameraRot;
    private float frozenCameraSize;
    private int currentLootBoxLevel = 9; // Default level, update based on player progress

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Store original camera position and rotation
        originalCameraPos = mainCamera.transform.position;
        originalCameraRot = mainCamera.transform.rotation;
        originalCameraSize = mainCamera.orthographicSize;

        // Setup loot box click detection
        if (lootBoxGameObject != null)
        {
            // Add collider if not present
            if (lootBoxGameObject.GetComponent<Collider>() == null)
            {
                lootBoxGameObject.AddComponent<BoxCollider>();
            }
        }

        // Setup UI buttons
        if (openLootBoxButton != null)
        {
            openLootBoxButton.onClick.AddListener(OnOpenLootBoxClicked);
        }

        if (leaveLootBoxButton != null)
        {
            leaveLootBoxButton.onClick.AddListener(OnLeaveLootBoxClicked);
        }

        // Initially hide the loot box UI
        if (lootBoxUICanvas != null)
        {
            lootBoxUICanvas.gameObject.SetActive(false);
        }

        // Update level text
        UpdateLootBoxLevelText();

        Debug.Log("[LootBoxInteraction] Loot box interaction initialized");
    }

    private void Update()
    {
        // Check for loot box click
        if (!isLootBoxViewActive && Input.GetMouseButtonDown(0))
        {
            HandleLootBoxClick();
        }
        Debug.Log("[LootBoxInteraction] Update - maincamera position: " + mainCamera.transform.position);

    }

    private void HandleLootBoxClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == lootBoxGameObject)
            {
                Debug.Log("[LootBoxInteraction] Loot box clicked!");
                ShowLootBoxView();
            }
        }
    }

    private void ShowLootBoxView()
    {
        if (isLootBoxViewActive)
            return;

        isLootBoxViewActive = true;

        // Stop any existing animations
        StopAllCoroutines();

        // Disable cube rotation
        if (menuCubeRotator != null)
        {
            menuCubeRotator.enabled = false;
            Debug.Log("[LootBoxInteraction] Disabled cube rotation");
        }

        // Show UI
        if (lootBoxUICanvas != null)
        {
            lootBoxUICanvas.gameObject.SetActive(true);
        }

        // Animate camera to loot box view
        StartCoroutine(AnimateCameraToLootBox());
        Debug.Log("[LootBoxInteraction] maincamera position: " + mainCamera.transform.position);
    }

    private IEnumerator AnimateCameraToLootBox()
    {
        float elapsedTime = 0f;
        float duration = 1f / cameraMoveSpeed; // Duration based on speed

        Debug.Log($"[LootBoxInteraction] Starting camera animation from {mainCamera.transform.position} to {lootBoxCameraPos}");

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            mainCamera.transform.position = Vector3.Lerp(originalCameraPos, lootBoxCameraPos, t);
            mainCamera.transform.rotation = Quaternion.Lerp(originalCameraRot, lootBoxCameraRot, t);
            mainCamera.orthographicSize = Mathf.Lerp(originalCameraSize, 12f, t);

            yield return null;
        }

        // Ensure exact final position and size
        mainCamera.transform.position = lootBoxCameraPos;
        mainCamera.transform.rotation = lootBoxCameraRot;
        mainCamera.orthographicSize = 12f;

        // Store the frozen position (in case parent transform affects world position)
        frozenCameraPos = mainCamera.transform.position;
        frozenCameraRot = mainCamera.transform.rotation;
        frozenCameraSize = mainCamera.orthographicSize;
        freezeCameraPosition = true;

        Debug.Log($"[LootBoxInteraction] Camera animation complete. Final position: {mainCamera.transform.position}, Frozen position stored: {frozenCameraPos}");
    }

    private void OnOpenLootBoxClicked()
    {
        Debug.Log("[LootBoxInteraction] Open Loot Box clicked - level " + currentLootBoxLevel);

        if (PlayFabBackendManager.Instance == null)
        {
            Debug.LogError("[LootBoxInteraction] PlayFabBackendManager not found!");
            return;
        }

        if (!PlayFabBackendManager.Instance.IsLoggedIn())
        {
            Debug.LogError("[LootBoxInteraction] Not logged in!");
            return;
        }

        // Call CloudScript to open loot box
        PlayFabBackendManager.Instance.ExecuteCloudScript("openLootBox",
            new System.Collections.Generic.Dictionary<string, object> { { "boxLevel", currentLootBoxLevel } },
            result =>
            {
                OnLootBoxOpenSuccess(result);
            },
            error =>
            {
                Debug.LogError($"[LootBoxInteraction] OpenLootBox failed: {error}");
            }
        );
    }

    private void OnLootBoxOpenSuccess(object result)
    {
        Debug.Log("[LootBoxInteraction] ===== LOOT BOX OPENED SUCCESSFULLY =====");

        // Convert result to JSON string
        string resultJson = result.ToString();
        Debug.Log("[LootBoxInteraction] Result JSON: " + resultJson);

        // Parse the JSON string
        LootBoxResponse response = null;
        try
        {
            response = JsonUtility.FromJson<LootBoxResponse>(resultJson);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LootBoxInteraction] Failed to parse loot box response: {e.Message}");
            return;
        }

        if (response == null || !response.success)
        {
            Debug.LogError("[LootBoxInteraction] Loot box response failed!");
            return;
        }

        // Extract cards from response
        List<object> awardedItems = new List<object>();

        if (response.rewards != null && response.rewards.cards != null)
        {
            foreach (var cardReward in response.rewards.cards)
            {
                // Use cardId to find the reward item
                object rewardItem = FindRewardItemById(cardReward.cardId);
                if (rewardItem != null)
                {
                    // Add the item once for each copy
                    for (int i = 0; i < cardReward.copies; i++)
                    {
                        awardedItems.Add(rewardItem);
                    }

                    if (rewardItem is CardData cardData)
                    {
                        Debug.Log($"[LootBoxInteraction] Found card: {cardData.cardName} [ID: {cardReward.cardId}] (x{cardReward.copies})");
                    }
                    else if (rewardItem is CastingLegendData)
                    {
                        Debug.Log($"[LootBoxInteraction] Found legend [ID: {cardReward.cardId}] (x{cardReward.copies})");
                    }
                    else if (rewardItem is LegendStoneData)
                    {
                        Debug.Log($"[LootBoxInteraction] Found legend stone [ID: {cardReward.cardId}] (x{cardReward.copies})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LootBoxInteraction] Reward item not found in registry with ID: {cardReward.cardId}");
                }
            }
        }

        // Show rewards to player
        if (rewardUI != null)
        {
            rewardUI.ShowRewards(awardedItems);
        }
        else
        {
            Debug.LogWarning("[LootBoxInteraction] Reward UI not assigned!");
        }

        Debug.Log($"[LootBoxInteraction] Showing {awardedItems.Count} items in reward UI");
    }

    private void OnLeaveLootBoxClicked()
    {
        if (!isLootBoxViewActive)
            return;

        Debug.Log("[LootBoxInteraction] Leave Loot Box clicked");
        isLootBoxViewActive = false;
        freezeCameraPosition = false;

        // Re-enable cube rotation
        if (menuCubeRotator != null)
        {
            menuCubeRotator.enabled = true;
            Debug.Log("[LootBoxInteraction] Enabled cube rotation");
        }

        // Hide UI
        if (lootBoxUICanvas != null)
        {
            lootBoxUICanvas.gameObject.SetActive(false);
        }

        // Animate camera back to original position
        StartCoroutine(AnimateCameraToOriginal());
    }

    private IEnumerator AnimateCameraToOriginal()
    {
        float elapsedTime = 0f;
        float duration = 1f / cameraMoveSpeed;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            mainCamera.transform.position = Vector3.Lerp(lootBoxCameraPos, originalCameraPos, t);
            mainCamera.transform.rotation = Quaternion.Lerp(lootBoxCameraRot, originalCameraRot, t);
            mainCamera.orthographicSize = Mathf.Lerp(12f, originalCameraSize, t);

            yield return null;
        }

        // Ensure exact final position and size
        mainCamera.transform.position = originalCameraPos;
        mainCamera.transform.rotation = originalCameraRot;
        mainCamera.orthographicSize = originalCameraSize;

        Debug.Log("[LootBoxInteraction] Camera returned to original position");
    }

    private void UpdateLootBoxLevelText()
    {
        // TODO: Get actual loot box level from player state
        // For now, use a default value
        currentLootBoxLevel = 9;

        if (lootBoxLevelText != null)
        {
            lootBoxLevelText.text = "Loot Box Level: " + currentLootBoxLevel;
        }
    }

    /// <summary>
    /// Update loot box level (call this when level increases)
    /// </summary>
    public void SetLootBoxLevel(int level)
    {
        currentLootBoxLevel = Mathf.Clamp(level, 1, 10);
        UpdateLootBoxLevelText();
        Debug.Log("[LootBoxInteraction] Loot box level set to: " + currentLootBoxLevel);
    }

    private void OnDestroy()
    {
        if (openLootBoxButton != null)
            openLootBoxButton.onClick.RemoveListener(OnOpenLootBoxClicked);

        if (leaveLootBoxButton != null)
            leaveLootBoxButton.onClick.RemoveListener(OnLeaveLootBoxClicked);
    }

    /// <summary>
    /// Safe extraction of int from dictionary, handling multiple numeric types
    /// </summary>
    private int SafeGetIntFromDict(Dictionary<string, object> dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return 0;

        var val = dict[key];
        if (val is int i) return i;
        if (val is long l) return (int)l;
        if (val is ulong ul) return (int)ul;
        if (val is uint ui) return (int)ui;
        if (val is double d) return (int)d;
        if (val is float f) return (int)f;

        return 0;
    }

    /// <summary>
    /// Safe extraction of string from dictionary
    /// </summary>
    private string SafeGetStringFromDict(Dictionary<string, object> dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return null;

        var val = dict[key];
        return val?.ToString();
    }

    /// <summary>
    /// Find a reward item by ID from the DeckRegistry (searches cards, legends, and legend stones)
    /// Returns CardData, CastingLegendData, or LegendStoneData
    /// </summary>
    private object FindRewardItemById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        DeckRegistry registry = Resources.Load<DeckRegistry>("DeckRegistry");
        if (registry == null)
        {
            Debug.LogError("[LootBoxInteraction] DeckRegistry not found!");
            return null;
        }

        // Try to find by ID using registry methods
        CardData card = registry.GetCardById(itemId);
        if (card != null)
        {
            return card;
        }

        CastingLegendData legend = registry.GetCastingLegendById(itemId);
        if (legend != null)
        {
            return legend;
        }

        LegendStoneData stone = registry.GetLegendStoneById(itemId);
        if (stone != null)
        {
            return stone;
        }

        Debug.LogWarning($"[LootBoxInteraction] Reward item with ID '{itemId}' not found in registry");
        return null;
    }

    /// <summary>
    /// Find a card by name from the DeckRegistry (searches cards, guardians, and legend stones)
    /// Returns CardData, CastingLegendData, or LegendStoneData
    /// </summary>
    private object FindCardByName(string cardName)
    {
        if (string.IsNullOrEmpty(cardName))
            return null;

        DeckRegistry registry = Resources.Load<DeckRegistry>("DeckRegistry");
        if (registry == null)
        {
            Debug.LogError("[LootBoxInteraction] DeckRegistry not found!");
            return null;
        }

        // Search in allCards
        CardData cardData = registry.GetCardByName(cardName);
        if (cardData != null)
        {
            return cardData;
        }

        // Search in allGuardians
        CastingLegendData castingLegendData = registry.GetCastingLegendByName(cardName);
        if (castingLegendData != null)
        {
            return castingLegendData;
        }

        // Search in allLegendStones
        LegendStoneData legendStoneData = registry.GetLegendStoneByName(cardName);
        if (legendStoneData != null)
        {
            return legendStoneData;
        }

        Debug.LogWarning($"[LootBoxInteraction] Card/Legend/Guardian not found in registry: {cardName}");
        return null;
    }

    /// <summary>
    /// Find a card by its ID from the DeckRegistry
    /// </summary>
    private CardData FindCardById(int cardId)
    {
        DeckRegistry registry = Resources.Load<DeckRegistry>("DeckRegistry");
        if (registry == null)
        {
            Debug.LogError("[LootBoxInteraction] DeckRegistry not found!");
            return null;
        }

        // CardData doesn't have a cardID field, so we can't search by ID
        // The CloudScript should return cardName instead
        Debug.LogWarning($"[LootBoxInteraction] Card lookup by ID not supported. Use cardName instead. (ID: {cardId})");
        return null;
    }
}
