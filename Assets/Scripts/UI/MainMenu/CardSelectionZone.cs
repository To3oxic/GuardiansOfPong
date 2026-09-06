using UnityEngine;
using TMPro;

public class CardSelectionZone : MonoBehaviour
{
    [Header("Legend Display")]
    [SerializeField] private Transform legendSpawnPoint;
    [SerializeField] private GameObject selectedLegendPrefab; // Which legend to display

    [Header("Card Grid")]
    [SerializeField] private Transform cardGridParent;
    [SerializeField] private CardHolderUI cardPrefab;
    [SerializeField] private int cardsPerRow = 3;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI legendNameText;
    [SerializeField] private TextMeshProUGUI deckInfoText;

    private GameObject legendInstance;
    private CardHolderUI[] cardDisplays;

    public void Initialize()
    {
        Debug.Log("[CardSelectionZone] Initializing...");

        // Spawn legend guardian in background
        SpawnLegend();

        // Create card grid for player's deck
        CreateCardGrid();

        // Position UI elements
        PositionUI();
    }

    private void SpawnLegend()
    {
        // TODO: Get player's selected legend from PlayerData
        // Instantiate it at legendSpawnPoint
        // Make sure it has idle animation

        Debug.Log("[CardSelectionZone] Spawning legend...");

        if (legendSpawnPoint != null && selectedLegendPrefab != null)
        {
            legendInstance = Instantiate(selectedLegendPrefab, legendSpawnPoint.position,
                legendSpawnPoint.rotation, transform);

            // Disable attack/movement components - just show idle animation
            // TODO: Set animator parameter to "Idle"
        }
    }

    private void CreateCardGrid()
    {
        // TODO: Fetch player's deck from PlayerData
        // Instantiate CardHolderUI prefabs in a grid

        Debug.Log("[CardSelectionZone] Creating card grid...");

        // Example layout:
        // CardData[] playerDeck = PlayerData.GetPlayerDeck();
        // cardDisplays = new CardHolderUI[playerDeck.Length];
        //
        // for (int i = 0; i < playerDeck.Length; i++)
        // {
        //     CardHolderUI cardUI = Instantiate(cardPrefab, cardGridParent);
        //     cardUI.Setup(playerDeck[i]);
        //     cardDisplays[i] = cardUI;
        //     
        //     // Position in grid
        //     LayoutElement layout = cardUI.GetComponent<LayoutElement>();
        //     // Set position based on i and cardsPerRow
        // }
    }

    private void PositionUI()
    {
        // TODO: Position legend name at top
        // TODO: Position deck info at bottom

        Debug.Log("[CardSelectionZone] Positioning UI...");
    }

    public void RefreshCardDisplay()
    {
        // TODO: Called when player's deck changes
        // Recreate card grid with new cards
    }
}
