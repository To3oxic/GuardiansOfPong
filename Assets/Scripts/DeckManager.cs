using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Linq;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance;

    public PlayerDeckData CurrentDeck { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadDeck(PlayerDeckData deck)
    {
        CurrentDeck = deck;
    }

    public List<CardData> GetSelectedCards() => CurrentDeck.selectedCards;
    public CastingLegendData GetSelectedLegend() => CurrentDeck.selectedCastingLegend;
    public LegendStoneData GetSelectedStone() => CurrentDeck.selectedLegendStone;

    public void SpawnCastingLegend(Transform spawnPoint)
    {
        var legendData = GetSelectedLegend();
        if (legendData != null && legendData.legendAvatarPrefab != null)
        {
            GameObject legend = Instantiate(legendData.legendAvatarPrefab, spawnPoint.position, spawnPoint.rotation);
            NetworkObject netObj = legend.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Spawn();
            }
        }
    }

    public void SpawnLegendStone(Transform spawnPoint)
    {
        var stoneData = GetSelectedStone();
        if (stoneData != null && stoneData.legendStoneVisualPrefab != null)
        {
            GameObject stone = Instantiate(stoneData.legendStoneVisualPrefab, spawnPoint.position, spawnPoint.rotation);
            NetworkObject netObj = stone.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Spawn();
            }
        }
    }
    public DeckPayload GetDeckPayload()
    {
        return new DeckPayload
        {
            cardNames = CurrentDeck.selectedCards.Select(c => c.cardName).ToList(),
            castingLegendName = CurrentDeck.selectedCastingLegend.name,
            legendStoneName = CurrentDeck.selectedLegendStone.name
        };

    }

    public PlayerDeckData GetSelectedDeck()
    {
        return CurrentDeck;
    }



}
