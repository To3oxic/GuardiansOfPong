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
        }
        else
        {
        }
    }

    private void OnTestOpenLootBoxClicked()
    {
        if (PlayFabBackendManager.Instance == null)
        {
            return;
        }

        if (!PlayFabBackendManager.Instance.IsLoggedIn())
        {
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
            }
        );
    }

    private void OnLootBoxOpenSuccess(object result)
    {
        // Handle both JsonObject and Dictionary
        System.Collections.Generic.Dictionary<string, object> resultDict = null;

        // Try as JsonObject first (PlayFab SDK format)
        if (result is PlayFab.Json.JsonObject jsonObj)
        {
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
            resultDict = dict;
        }
        else
        {
            return;
        }

        if (resultDict != null)
        {
            foreach (var kvp in resultDict)
            {
            }

            // Check for success
            if (resultDict.ContainsKey("success"))
            {
                bool success = (bool)resultDict["success"];
                if (success)
                {
                    // Show cards awarded
                    if (resultDict.ContainsKey("cardsAwarded"))
                    {
                    }

                    // Show gold/gems
                    if (resultDict.ContainsKey("goldAwarded"))
                    {
                    }
                    if (resultDict.ContainsKey("gemsAwarded"))
                    {
                    }

                    // Show message
                    if (resultDict.ContainsKey("message"))
                    {
                    }
                }
                else
                {
                    if (resultDict.ContainsKey("error"))
                    {
                    }
                }
            }
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
