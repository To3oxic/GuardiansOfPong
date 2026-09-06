using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Displays loot box rewards (cards, gold, gems) one by one
/// Shows each unique card with its quantity
/// </summary>
public class LootBoxRewardUI : MonoBehaviour
{
    /// <summary>
    /// Wrapper class to track an item and its quantity
    /// </summary>
    private class RewardInfo
    {
        public object item; // CardData, CastingLegendData, or LegendStoneData
        public int quantity;

        public RewardInfo(object item, int quantity = 1)
        {
            this.item = item;
            this.quantity = quantity;
        }

        public string GetItemId()
        {
            if (item is CardData cardData) return cardData.id;
            if (item is CastingLegendData legendData) return legendData.id;
            if (item is LegendStoneData stoneData) return stoneData.id;
            return "";
        }
    }

    [Header("UI References")]
    [SerializeField] private Canvas rewardCanvas;
    [SerializeField] private Transform cardHolderContainer; // Parent transform to instantiate reward holders
    [SerializeField] private GameObject cardHolderPrefab; // Card holder prefab
                                                          // LegendStone holder prefab
    [SerializeField] private TextMeshProUGUI nextPromptText; // "Click to continue" text
    [SerializeField] private CanvasGroup fadeCanvasGroup; // For fade in/out animations

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    private List<RewardInfo> rewardItems = new List<RewardInfo>(); // Consolidated rewards with quantities
    private int currentItemIndex = 0;
    private bool waitingForClick = false;

    private void Awake()
    {
        if (fadeCanvasGroup == null && rewardCanvas != null)
        {
            fadeCanvasGroup = rewardCanvas.GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        // Initially hide the reward UI
        if (rewardCanvas != null)
        {
            rewardCanvas.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Check for click to advance to next card
        if (waitingForClick && Input.GetMouseButtonDown(0))
        {
            HandleCardAdvance();
        }
    }

    /// <summary>
    /// Show rewards from loot box opening
    /// Consolidates duplicate items and displays quantity
    /// </summary>
    public void ShowRewards(List<object> items)
    {
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("[LootBoxRewardUI] No items to show!");
            return;
        }

        // Consolidate rewards by item ID
        rewardItems = ConsolidateRewards(items);
        currentItemIndex = 0;
        waitingForClick = false;

        // Show the UI
        if (rewardCanvas != null)
        {
            rewardCanvas.gameObject.SetActive(true);
        }

        // Start showing items
        StartCoroutine(ShowNextItem());
    }

    /// <summary>
    /// Consolidate duplicate items by ID and sum their quantities
    /// </summary>
    private List<RewardInfo> ConsolidateRewards(List<object> items)
    {
        Dictionary<string, RewardInfo> consolidatedDict = new Dictionary<string, RewardInfo>();

        foreach (object item in items)
        {
            RewardInfo rewardInfo = item as RewardInfo;

            // If item is already wrapped as RewardInfo, use it
            if (rewardInfo != null)
            {
                string itemId = rewardInfo.GetItemId();
                if (consolidatedDict.ContainsKey(itemId))
                {
                    consolidatedDict[itemId].quantity += rewardInfo.quantity;
                }
                else
                {
                    consolidatedDict[itemId] = new RewardInfo(rewardInfo.item, rewardInfo.quantity);
                }
            }
            else
            {
                // If item is a raw object (CardData, etc.), wrap it with quantity 1
                string itemId = "";
                if (item is CardData cardData) itemId = cardData.id;
                else if (item is CastingLegendData legendData) itemId = legendData.id;
                else if (item is LegendStoneData stoneData) itemId = stoneData.id;

                if (!string.IsNullOrEmpty(itemId))
                {
                    if (consolidatedDict.ContainsKey(itemId))
                    {
                        consolidatedDict[itemId].quantity += 1;
                    }
                    else
                    {
                        consolidatedDict[itemId] = new RewardInfo(item, 1);
                    }
                }
            }
        }

        return consolidatedDict.Values.ToList();
    }

    /// <summary>
    /// Handle click to show next item
    /// </summary>
    private void HandleCardAdvance()
    {
        currentItemIndex++;
        waitingForClick = false;

        if (currentItemIndex >= rewardItems.Count)
        {
            // All items shown, close the UI
            CloseRewardUI();
        }
        else
        {
            // Show next item
            StartCoroutine(ShowNextItem());
        }
    }

    /// <summary>
    /// Show the next item with fade animation
    /// </summary>
    private IEnumerator ShowNextItem()
    {
        // Clear previous holder
        foreach (Transform child in cardHolderContainer)
        {
            Destroy(child.gameObject);
        }

        // Fade out
        if (fadeCanvasGroup != null)
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutDuration);
        }

        // Show new item
        RewardInfo rewardInfo = rewardItems[currentItemIndex];
        InstantiateRewardHolder(rewardInfo);

        // Fade in
        if (fadeCanvasGroup != null)
        {
            yield return FadeCanvasGroup(0f, 1f, fadeInDuration);
        }

        // Update prompt text
        if (nextPromptText != null)
        {
            if (currentItemIndex < rewardItems.Count - 1)
            {
                nextPromptText.text = "Click to continue";
            }
            else
            {
                nextPromptText.text = "Click to finish";
            }
        }

        // Wait for click
        waitingForClick = true;
    }

    /// <summary>
    /// Instantiate appropriate holder based on reward item type
    /// </summary>
    private void InstantiateRewardHolder(RewardInfo rewardInfo)
    {
        if (rewardInfo.item is CardData cardData)
        {
            InstantiateCardHolder(cardData, rewardInfo.quantity);
        }
        else if (rewardInfo.item is CastingLegendData legendData)
        {
            InstantiateGuardianHolder(legendData, rewardInfo.quantity);
        }
        else if (rewardInfo.item is LegendStoneData stoneData)
        {
            InstantiateLegendStoneHolder(stoneData, rewardInfo.quantity);
        }
        else
        {
            Debug.LogError($"[LootBoxRewardUI] Unknown reward type: {rewardInfo.item.GetType()}");
        }
    }

    /// <summary>
    /// Instantiate a card holder for the given card with quantity
    /// </summary>
    private void InstantiateCardHolder(CardData cardData, int quantity)
    {
        if (cardHolderPrefab == null || cardHolderContainer == null)
        {
            Debug.LogError("[LootBoxRewardUI] Card holder prefab or container not assigned!");
            return;
        }

        GameObject holderInstance = Instantiate(cardHolderPrefab, cardHolderContainer);

        // Find the card holder component and set it up
        LootBoxCardHolder holder = holderInstance.GetComponent<LootBoxCardHolder>();
        if (holder != null)
        {
            holder.SetupCard(cardData, quantity);
        }
        else
        {
            Debug.LogWarning("[LootBoxRewardUI] Card holder prefab doesn't have LootBoxCardHolder component!");
        }
    }

    /// <summary>
    /// Instantiate a legend holder for the given legend with quantity
    /// </summary>
    private void InstantiateGuardianHolder(CastingLegendData legendData, int quantity)
    {
        if (cardHolderContainer == null || cardHolderContainer == null)
        {
            Debug.LogError("[LootBoxRewardUI] Legend holder prefab or container not assigned!");
            return;
        }

        GameObject holderInstance = Instantiate(cardHolderPrefab, cardHolderContainer);

        // Find the legend holder component and set it up
        LootBoxCardHolder holder = holderInstance.GetComponent<LootBoxCardHolder>();
        if (holder != null)
        {
            holder.SetupGuardian(legendData, quantity);
        }
        else
        {
            Debug.LogWarning("[LootBoxRewardUI] Legend holder prefab doesn't have LootBoxLegendHolder component!");
        }
    }

    /// <summary>
    /// Instantiate a legend stone holder for the given stone with quantity
    /// </summary>
    private void InstantiateLegendStoneHolder(LegendStoneData stoneData, int quantity)
    {
        if (cardHolderPrefab == null || cardHolderContainer == null)
        {
            Debug.LogError("[LootBoxRewardUI] LegendStone holder prefab or container not assigned!");
            return;
        }

        GameObject holderInstance = Instantiate(cardHolderPrefab, cardHolderContainer);

        // Find the stone holder component and set it up
        LootBoxCardHolder holder = holderInstance.GetComponent<LootBoxCardHolder>();
        if (holder != null)
        {
            holder.SetupLegendStone(stoneData, quantity);
        }
        else
        {
            Debug.LogWarning("[LootBoxRewardUI] LegendStone holder prefab doesn't have LootBoxLegendStoneHolder component!");
        }
    }

    /// <summary>
    /// Fade canvas group in or out
    /// </summary>
    private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
            yield break;

        fadeCanvasGroup.alpha = startAlpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = endAlpha;
    }

    /// <summary>
    /// Close the reward UI
    /// </summary>
    private void CloseRewardUI()
    {
        Debug.Log("[LootBoxRewardUI] Closing reward UI");

        if (rewardCanvas != null)
        {
            rewardCanvas.gameObject.SetActive(false);
        }

        // Clear the list
        rewardItems.Clear();
        currentItemIndex = 0;
    }
}
