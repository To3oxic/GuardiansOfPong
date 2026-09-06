using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
public class DeckSelectionUI : MonoBehaviour
{
    public Transform cardListContainer;
    public Transform legendListContainer;
    public Transform stoneListContainer;
    public GameObject cardButtonPrefab;
    public GameObject legendButtonPrefab;
    public GameObject stoneButtonPrefab;
    public Button startMatchButton;
    public GameObject connectionUI; // UI with Host/Client buttons
    public GameObject deckSelectionUI; // This panel
    private List<CardData> selectedCards = new List<CardData>();
    private CastingLegendData selectedLegend;
    private LegendStoneData selectedStone;
    void Start()
    {
        connectionUI.SetActive(true);
        deckSelectionUI.SetActive(false);
        NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkConnected;
    }
    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkConnected;
        }
    }
    void OnNetworkConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            connectionUI.SetActive(false);
            deckSelectionUI.SetActive(true);
            PopulateCardList();
            PopulateLegendList();
            PopulateStoneList();
        }
    }
    void PopulateCardList()
    {
        foreach (CardData card in DeckRegistry.Instance.allCards)
        {
            GameObject buttonObj = Instantiate(cardButtonPrefab, cardListContainer);
            buttonObj.GetComponentInChildren<Text>().text = card.id;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => ToggleCard(card));
        }
    }
    void ToggleCard(CardData card)
    {
        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
        }
        else if (selectedCards.Count < 8)
        {
            selectedCards.Add(card);
        }
    }
    void PopulateLegendList()
    {
        foreach (CastingLegendData legend in DeckRegistry.Instance.allGuardians)
        {
            GameObject buttonObj = Instantiate(legendButtonPrefab, legendListContainer);
            buttonObj.GetComponentInChildren<Text>().text = legend.id;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => selectedLegend = legend);
        }
    }
    void PopulateStoneList()
    {
        foreach (LegendStoneData stone in DeckRegistry.Instance.allLegendStones)
        {
            GameObject buttonObj = Instantiate(stoneButtonPrefab, stoneListContainer);
            buttonObj.GetComponentInChildren<Text>().text = stone.id;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => selectedStone = stone);
        }
    }
}
