// NetworkUI.cs (Updated to support dedicated server-client model)
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class NetworkUI : MonoBehaviour
{
    public Button hostButton;
    public Button clientButton;
    public Button serverButton;
    public GameObject networkUIRoot;
    public GameObject deckSelectionUIRoot;
    public Transform cardListContainer;
    public Transform legendListContainer;
    public Transform stoneListContainer;
    public GameObject cardButtonPrefab;
    public GameObject legendButtonPrefab;
    public GameObject stoneButtonPrefab;
    public Button startMatchButton;


    private bool isClientPendingConnection = false;
    private List<CardData> selectedCards = new List<CardData>();
    private CastingLegendData selectedLegend;
    private LegendStoneData selectedStone;

    void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        clientButton.onClick.AddListener(StartClient);
        serverButton.onClick.AddListener(StartServer);

        if (startMatchButton != null)
            startMatchButton.onClick.AddListener(OnStartMatchClicked);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedAfterDeckReady;
        }
    }

    void ShowDeckUI()
    {
        ClearDeckUI();
        PopulateCardList();
        PopulateLegendList();
        PopulateStoneList();
        
    }

    void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("[NetworkUI] Host started.");
        if (networkUIRoot != null) networkUIRoot.SetActive(false);
    }

    void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        Debug.Log("[NetworkUI] Dedicated server started.");
        if (networkUIRoot != null) networkUIRoot.SetActive(false);
    }
    void StartClient()
    {
        isClientPendingConnection = true;
        networkUIRoot.SetActive(false);
        ShowDeckUI();
        deckSelectionUIRoot.SetActive(true);
    }


    public void HideDeckSelectionUI()
    {
        if (deckSelectionUIRoot != null)
        {
            deckSelectionUIRoot.SetActive(false);
        }
    }

    void ClearDeckUI()
    {
        foreach (Transform child in cardListContainer)
        {
            Destroy(child.gameObject);
        }
        foreach (Transform child in legendListContainer)
        {
            Destroy(child.gameObject);
        }
        foreach (Transform child in stoneListContainer)
        {
            Destroy(child.gameObject);
        }

        selectedCards.Clear();
        selectedLegend = null;
        selectedStone = null;
    }

    void PopulateCardList()
    {
        foreach (CardData card in DeckRegistry.Instance.allCards)
        {
            GameObject buttonObj = Instantiate(cardButtonPrefab, cardListContainer);
            buttonObj.GetComponentInChildren<Text>().text = card.cardName;
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
        Debug.Log($"[DeckSelection] Selected Cards: {selectedCards.Count}");
    }

    void PopulateLegendList()
    {
        foreach (CastingLegendData legend in DeckRegistry.Instance.allCastingLegends)
        {
            GameObject buttonObj = Instantiate(legendButtonPrefab, legendListContainer);
            buttonObj.GetComponentInChildren<Text>().text = legend.legendName;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => selectedLegend = legend);
        }
    }

    void PopulateStoneList()
    {
        foreach (LegendStoneData stone in DeckRegistry.Instance.allLegendStones)
        {
            GameObject buttonObj = Instantiate(stoneButtonPrefab, stoneListContainer);
            buttonObj.GetComponentInChildren<Text>().text = stone.stoneName;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => selectedStone = stone);
        }
    }

    void OnStartMatchClicked()
    {
        if (isClientPendingConnection)
        {
            NetworkManager.Singleton.StartClient();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedAfterDeckReady;
        }
    }

    void OnClientConnectedAfterDeckReady(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        DeckPayload payload = new DeckPayload
        {
            cardNames = selectedCards.ConvertAll(card => card.cardName),
            castingLegendName = selectedLegend.name,
            legendStoneName = selectedStone.name
        };

        GameManager.Instance.SubmitDeckServerRpc(payload);
        Debug.Log("[NetworkUI] Deck submitted after connection.");

        CardManager cm = FindFirstObjectByType<CardManager>();
        if (cm != null)
        {
            cm.StartCardUIInitialization();
        }
        else
        {
            Debug.LogWarning("[NetworkUI] CardManager not found in scene!");
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedAfterDeckReady;
        HideDeckSelectionUI();
    }

}
