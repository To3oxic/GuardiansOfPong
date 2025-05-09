using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EndGameUI : MonoBehaviour
{
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI timeText;
    public Image[] stars;
    public Sprite filledStar;
    public Sprite emptyStar;
    public CanvasGroup canvasGroup;
    void Start()
    {
        gameObject.SetActive(false);
    }
    public void ShowResult(bool isWinner, int starCount, float matchDuration)
    {
        gameObject.SetActive(true);

        resultText.text = isWinner ? "YOU WIN" : "YOU LOSE";
        resultText.color = isWinner ? Color.green : Color.red;

        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].sprite = i < starCount ? filledStar : emptyStar;
        }

        int minutes = Mathf.FloorToInt(matchDuration / 60f);
        int seconds = Mathf.FloorToInt(matchDuration % 60f);
        timeText.text = $"Match Duration: {minutes:00}:{seconds:00}";

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
}
