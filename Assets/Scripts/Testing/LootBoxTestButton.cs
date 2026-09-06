using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test script to open a loot box by pressing a button
/// Displays the result in debug logs for verification
/// </summary>
public class LootBoxTestButton : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Button testOpenLootBoxButton;
    [SerializeField] private int testLootBoxLevel = 1; // Level 1-10

    private void Start()
    {
        if (testOpenLootBoxButton != null)
        {
            testOpenLootBoxButton.onClick.AddListener(OnTestOpenLootBoxClicked);
            Debug.Log("[LootBoxTestButton] Test button registered. Click to open loot box level " + testLootBoxLevel);
        }
        else
        {
            Debug.LogWarning("[LootBoxTestButton] Test button not assigned!");
        }
    }

    private void OnTestOpenLootBoxClicked()
    {
        Debug.Log("[LootBoxTestButton] Opening loot box level " + testLootBoxLevel);

        if (PlayFabBackendManager.Instance == null)
        {
            Debug.LogError("[LootBoxTestButton] PlayFabBackendManager not found!");
            return;
        }

        if (!PlayFabBackendManager.Instance.IsLoggedIn())
        {
            Debug.LogError("[LootBoxTestButton] Not logged in!");
            return;
        }

        // Call CloudScript to open loot box
        var request = new PlayFab.ClientModels.ExecuteCloudScriptRequest
        {
            FunctionName = "openLootBox",
            FunctionParameter = new { boxLevel = testLootBoxLevel },
            GeneratePlayStreamEvent = true
        };

        PlayFabBackendManager.Instance.ExecuteCloudScript("openLootBox",
            new System.Collections.Generic.Dictionary<string, object> { { "boxLevel", testLootBoxLevel } },
            result =>
            {
                OnLootBoxOpenSuccess(result);
            },
            error =>
            {
                Debug.LogError($"[LootBoxTestButton] OpenLootBox failed: {error}");
            }
        );
    }

    private void OnLootBoxOpenSuccess(object result)
    {
        Debug.Log("[LootBoxTestButton] ===== LOOT BOX RESULT =====");
        Debug.Log($"[LootBoxTestButton] Result type: {result.GetType()}");
        Debug.Log($"[LootBoxTestButton] Result: {result}");

        // Handle both JsonObject and Dictionary
        System.Collections.Generic.Dictionary<string, object> resultDict = null;

        // Try as JsonObject first (PlayFab SDK format)
        if (result is PlayFab.Json.JsonObject jsonObj)
        {
            Debug.Log("[LootBoxTestButton] Result is JsonObject, converting...");
            resultDict = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var key in jsonObj.Keys)
            {
                if (key == null) continue;
                resultDict[key] = jsonObj[key];
            }
        }
        // Try as Dictionary
        else if (result is System.Collections.Generic.Dictionary<string, object> dict)
        {
            Debug.Log("[LootBoxTestButton] Result is Dictionary");
            resultDict = dict;
        }
        else
        {
            Debug.LogWarning($"[LootBoxTestButton] Unknown result type: {result.GetType()}");
            Debug.Log($"[LootBoxTestButton] Result toString: {result.ToString()}");
            return;
        }

        if (resultDict != null)
        {
            Debug.Log("[LootBoxTestButton] ===== LOOT BOX CONTENTS =====");
            foreach (var kvp in resultDict)
            {
                Debug.Log($"[LootBoxTestButton] {kvp.Key}: {kvp.Value}");
            }

            // Check for success
            if (resultDict.ContainsKey("success"))
            {
                bool success = (bool)resultDict["success"];
                if (success)
                {
                    Debug.Log("[LootBoxTestButton] ✓ LOOT BOX OPENED SUCCESSFULLY");

                    // Show cards awarded
                    if (resultDict.ContainsKey("cardsAwarded"))
                    {
                        Debug.Log("[LootBoxTestButton] Cards awarded: " + resultDict["cardsAwarded"]);
                    }

                    // Show gold/gems
                    if (resultDict.ContainsKey("goldAwarded"))
                    {
                        Debug.Log($"[LootBoxTestButton] Gold awarded: {resultDict["goldAwarded"]}");
                    }
                    if (resultDict.ContainsKey("gemsAwarded"))
                    {
                        Debug.Log($"[LootBoxTestButton] Gems awarded: {resultDict["gemsAwarded"]}");
                    }

                    // Show message
                    if (resultDict.ContainsKey("message"))
                    {
                        Debug.Log("[LootBoxTestButton] Message: " + resultDict["message"]);
                    }
                }
                else
                {
                    Debug.LogError("[LootBoxTestButton] ✗ LOOT BOX FAILED");
                    if (resultDict.ContainsKey("error"))
                    {
                        Debug.LogError("[LootBoxTestButton] Error: " + resultDict["error"]);
                    }
                }
            }

            Debug.Log("[LootBoxTestButton] ===== END LOOT BOX RESULT =====");
        }
    }

    private void OnDestroy()
    {
        if (testOpenLootBoxButton != null)
        {
            testOpenLootBoxButton.onClick.RemoveListener(OnTestOpenLootBoxClicked);
        }
    }
}
