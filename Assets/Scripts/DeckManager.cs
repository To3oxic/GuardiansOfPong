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
        var deck = new DeckPayload
        {
            floorId = CurrentDeck.selectedCastingLegend?.floorId ?? string.Empty,
            // Create simple DTO objects for network serialization
guardian_card = new DeckPayload.GuardianCardDto
            {
                registry_name = CurrentDeck.selectedCastingLegend?.name ?? string.Empty,
                id = CurrentDeck.selectedCastingLegend?.id ?? string.Empty
            },
            legend_stone = new DeckPayload.LegendStoneDto
            {
                registry_name = CurrentDeck.selectedLegendStone?.stoneName ?? string.Empty,
                id = CurrentDeck.selectedLegendStone?.id ?? string.Empty
            }
        };

        // Add card IDs
        if (CurrentDeck.selectedCards != null)
        {
            deck.cards = new List<DeckPayload.CardDataDto>();
            foreach (var card in CurrentDeck.selectedCards)
            {
                deck.cards.Add(new DeckPayload.CardDataDto
                {
                    registry_name = card.cardName,
                    id = card.id ?? card.id,
                    level = 5  // Default level 5 for local testing
                });
            }
        }

        return deck;
    }

    public PlayerDeckData GetSelectedDeck()
    {
        return CurrentDeck;
    }



}
