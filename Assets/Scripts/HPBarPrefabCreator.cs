using UnityEngine;
using UnityEngine.UI;

// This script helps create HP bar prefabs in the editor
public class HPBarPrefabCreator : MonoBehaviour
{
    [Header("HP Bar Prefab Settings")]
    public Vector2 barSize = new Vector2(200, 20);
    public Color backgroundColor = Color.black;
    public Color fullHPColor = Color.green;
    public Color mediumHPColor = Color.yellow;
    public Color lowHPColor = Color.red;

    [ContextMenu("Create HP Bar Prefab")]
    public void CreateHPBarPrefab()
    {
        // Create main HP bar GameObject
        GameObject hpBar = new GameObject("HP Bar Prefab");

        // Add Canvas for world space rendering
        Canvas canvas = hpBar.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Add CanvasScaler
        CanvasScaler scaler = hpBar.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Add GraphicRaycaster (required for UI)
        hpBar.AddComponent<GraphicRaycaster>();

        // Set RectTransform
        RectTransform mainRect = hpBar.GetComponent<RectTransform>();
        mainRect.sizeDelta = barSize;

        // Create background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(hpBar.transform);
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = backgroundColor;

        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Create border (optional)
        GameObject border = new GameObject("Border");
        border.transform.SetParent(hpBar.transform);
        Image borderImage = border.AddComponent<Image>();
        borderImage.color = Color.white;

        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);

        // Create slider for HP
        GameObject sliderObj = new GameObject("HP Slider");
        sliderObj.transform.SetParent(hpBar.transform);
        Slider slider = sliderObj.AddComponent<Slider>();

        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        // Create fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform);

        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Create fill
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = fullHPColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Configure slider
        slider.fillRect = fillRect;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;
        slider.interactable = false;

        // Add HP bar controller script
        HPBarController hpBarController = hpBar.AddComponent<HPBarController>();
        hpBarController.slider = slider;
        hpBarController.fillImage = fillImage;
        hpBarController.fullHPColor = fullHPColor;
        hpBarController.mediumHPColor = mediumHPColor;
        hpBarController.lowHPColor = lowHPColor;

        Debug.Log($"[HPBarPrefabCreator] Created HP bar prefab: {hpBar.name}");

        // Select the created object in hierarchy
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = hpBar;
#endif
    }
}

// Controller script for HP bar prefabs
public class HPBarController : MonoBehaviour
{
    [Header("HP Bar Components")]
    public Slider slider;
    public Image fillImage;

    [Header("HP Bar Colors")]
    public Color fullHPColor = Color.green;
    public Color mediumHPColor = Color.yellow;
    public Color lowHPColor = Color.red;

    [Header("Animation")]
    public bool animateChanges = true;
    public float animationSpeed = 2f;

    private float targetValue = 1f;
    private float currentValue = 1f;

    void Update()
    {
        if (animateChanges && Mathf.Abs(currentValue - targetValue) > 0.01f)
        {
            currentValue = Mathf.MoveTowards(currentValue, targetValue, animationSpeed * Time.deltaTime);
            UpdateSlider(currentValue);
        }
    }

    public void SetHP(float percentage, bool immediate = false)
    {
        targetValue = Mathf.Clamp01(percentage);

        if (immediate || !animateChanges)
        {
            currentValue = targetValue;
            UpdateSlider(currentValue);
        }
    }

    private void UpdateSlider(float value)
    {
        if (slider != null)
        {
            slider.value = value;
        }

        if (fillImage != null)
        {
            // Update color based on HP percentage
            Color targetColor;
            if (value > 0.6f)
                targetColor = fullHPColor;
            else if (value > 0.3f)
                targetColor = Color.Lerp(lowHPColor, mediumHPColor, (value - 0.3f) / 0.3f);
            else
                targetColor = Color.Lerp(lowHPColor, mediumHPColor, value / 0.3f);

            fillImage.color = targetColor;
        }
    }

    // Method to pulse or flash the HP bar (for taking damage)
    public void PulseRed()
    {
        if (fillImage != null)
        {
            StartCoroutine(PulseEffect());
        }
    }

    private System.Collections.IEnumerator PulseEffect()
    {
        Color originalColor = fillImage.color;
        fillImage.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        fillImage.color = originalColor;
    }
}