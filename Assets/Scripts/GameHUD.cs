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

    private PaddleController localPaddle;
    private PaddleController opponentPaddle;

    void Start()
    {
        StartCoroutine(WaitForPlayers());
        StartCoroutine(FindLocalPaddle());
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


}
