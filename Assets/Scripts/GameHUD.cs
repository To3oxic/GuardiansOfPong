using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class GameHUD : MonoBehaviour
{
    // UI elements
    public Slider localHpSlider;
    public Slider localManaSlider;
    public Slider opponentHpSlider;
    public Transform cardPanel;
    public GameObject cardButtonPrefab;

    private PaddleController localPaddle;
    private PaddleController opponentPaddle;
    public List<CardData> playerDeck = new List<CardData>();

    void Start()
    {
        StartCoroutine(WaitForPlayers());
        StartCoroutine(FindLocalPaddle());
        PopulateDeckUI();
    }

    IEnumerator WaitForPlayers()
    {
        while (true)
        {
            PaddleController[] paddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
            localPaddle = null;
            opponentPaddle = null;
            foreach (PaddleController paddle in paddles)
            {
                if (paddle.IsOwner)
                {
                    localPaddle = paddle;
                }
                else
                {
                    opponentPaddle = paddle;
                }
            }

            if (localPaddle != null && opponentPaddle != null)
            {
                Debug.Log("[GameHUD] Both local and opponent paddles found.");
                break;
            }
            yield return null;
        }

        InitializeUI();
    }

    IEnumerator FindLocalPaddle()
    {
        while (localPaddle == null)
        {
            PaddleController[] paddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
            foreach (var paddle in paddles)
            {
                if (paddle.IsOwner)
                {
                    localPaddle = paddle;
                    break;
                }
            }
            yield return null;
        }
    }

    void InitializeUI()
    {
        if (localPaddle != null)
        {

        }
        else
        {
            Debug.LogWarning("[GameHUD] Local paddle not found!");
        }

        if (opponentPaddle != null)
        {

        }
        else
        {
            Debug.LogWarning("[GameHUD] Opponent paddle not found!");
        }
    }

    void OnLocalHPChanged(int previousValue, int newValue)
    {

    }

    void OnLocalManaChanged(int previousValue, int newValue)
    {

    }

    void OnOpponentHPChanged(int previousValue, int newValue)
    {

    }

    void PopulateDeckUI()
    {
        foreach (Transform child in cardPanel)
        {
            Destroy(child.gameObject);
        }

        foreach (CardData card in playerDeck)
        {
            GameObject btnObj = Instantiate(cardButtonPrefab, cardPanel);
            Button btn = btnObj.GetComponent<Button>();
            Image img = btnObj.GetComponentInChildren<Image>();
            Text txt = btnObj.GetComponentInChildren<Text>();

            if (img != null) img.sprite = card.cardImage;
            if (txt != null) txt.text = card.cardName;

            btn.onClick.AddListener(() => OnCardPlayed(card));
        }
    }

    void OnCardPlayed(CardData card)
    {
        if (localPaddle != null && localPaddle.currentMana.Value >= card.manaCost)
        {
            Debug.Log("Card played: " + card.cardName);
            GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId);
            localPaddle.ModifyManaServerRpc(-Mathf.RoundToInt(card.manaCost));
        }
        else
        {
            Debug.Log("Not enough mana!");
        }
    }
}
