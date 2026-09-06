using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a single row in the card selection UI
/// Uses horizontal layout to arrange cards (3 per row)
/// </summary>
public class CardRow : MonoBehaviour
{
    private HorizontalLayoutGroup layoutGroup;

    private void Awake()
    {
        // Ensure horizontal layout is set up
        layoutGroup = GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        // Configure layout
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = true;

        // Get RectTransform for sizing
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(0, 200f); // Fixed height, flexible width
        }
    }
}
