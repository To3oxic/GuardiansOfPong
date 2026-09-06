using UnityEngine;
using TMPro;

public class ShopZone : MonoBehaviour
{
    [Header("Shop Setup")]
    [SerializeField] private Transform shopItemsGridParent;
    [SerializeField] private GameObject shopItemPrefab;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI currencyDisplay;
    [SerializeField] private int playerCoins = 500; // TODO: Load from PlayerData

    public void Initialize()
    {
        // Display player's current currency
        UpdateCurrencyDisplay();

        // Create shop items grid
        CreateShopItemsGrid();
    }

    private void UpdateCurrencyDisplay()
    {
        // TODO: Load actual player coins from PlayerData
        if (currencyDisplay != null)
        {
            currencyDisplay.text = $"Coins: {playerCoins}";
        }
    }

    private void CreateShopItemsGrid()
    {
        // TODO: Fetch shop items from backend/database
        // For now, create placeholder items

        // Example:
        // ShopItem[] items = GetShopItemsFromServer();
        // displayedItems = new ShopItem[items.Length];
        //
        // for (int i = 0; i < items.Length; i++)
        // {
        //     ShopItem item = Instantiate(shopItemPrefab, shopItemsGridParent).GetComponent<ShopItem>();
        //     item.Setup(items[i]);
        //     item.OnPurchaseClicked += OnItemPurchased;
        //     displayedItems[i] = item;
        // }
    }
}
