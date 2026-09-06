using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the Apply/Submit button for the selected deck
/// </summary>
public class DeckSubmitButton : MonoBehaviour
{
    [SerializeField] private Button submitButton;
    [SerializeField] private SelectionCardManager selectionCardManager;

    private void Start()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmitPressed);
        }

        if (selectionCardManager == null)
        {
            selectionCardManager = FindFirstObjectByType<SelectionCardManager>();
        }
    }

    private void OnSubmitPressed()
    {
        Debug.Log("[DeckSubmitButton] Submit button pressed");

        if (selectionCardManager == null)
        {
            Debug.LogError("[DeckSubmitButton] SelectionCardManager not found!");
            return;
        }

        // Validate the deck
        if (selectionCardManager.GetRegularCardCount() == 0)
        {
            Debug.LogWarning("[DeckSubmitButton] Please select at least one card");
            return;
        }

        if (!selectionCardManager.IsGuardianSelected())
        {
            Debug.LogWarning("[DeckSubmitButton] Please select a guardian");
            return;
        }

        if (!selectionCardManager.IsLegendStoneSelected())
        {
            Debug.LogWarning("[DeckSubmitButton] Please select a legend stone");
            return;
        }

        // Submit the deck
        selectionCardManager.SubmitDeck();
    }
}
