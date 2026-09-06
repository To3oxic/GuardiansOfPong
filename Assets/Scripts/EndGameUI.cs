using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class EndGameUI : MonoBehaviour
{
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI timeText;
    public Image[] stars;
    public Sprite filledStar;
    public Sprite emptyStar;
    public Button leaveButton;
    public CanvasGroup canvasGroup;

    void Start()
    {
        gameObject.SetActive(false);

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        }
    }

    public void ShowResult(bool isWinner, int starCount, float matchDuration)
    {
        Debug.Log($"[EndGameUI] ShowResult called: isWinner={isWinner}, starCount={starCount}, matchDuration={matchDuration}");
        gameObject.SetActive(true);

        if (resultText)
        {
            resultText.text = isWinner ? "VICTORY" : "DEFEAT";
            resultText.color = isWinner ? Color.green : Color.red;
        }
        else
        {
            Debug.LogError("[EndGameUI] resultText is null!");
        }

        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i])
                    stars[i].sprite = i < starCount ? filledStar : emptyStar;
            }
        }
        else
        {
            Debug.LogError("[EndGameUI] stars array is null!");
        }

        int minutes = Mathf.FloorToInt(matchDuration / 60f);
        int seconds = Mathf.FloorToInt(matchDuration % 60f);
        timeText.text = $"{minutes:00}:{seconds:00}";

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float duration = 1f;
        float elapsed = 0f;
        canvasGroup.alpha = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
    }

    private void OnLeaveButtonClicked()
    {
        Debug.Log("[EndGameUI] Leave button clicked");
        Time.timeScale = 1f; // Ensure time is running before loading scene
        SceneManager.LoadScene("MainMenu"); // Load main menu scene
    }

    void OnDestroy()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);
        }
    }
}

