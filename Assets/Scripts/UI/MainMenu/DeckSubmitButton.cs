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
        if (selectionCardManager == null)
        {
            return;
        }

        // Validate the deck
        if (selectionCardManager.GetRegularCardCount() == 0)
        {
            return;
        }

        if (!selectionCardManager.IsGuardianSelected())
        {
            return;
        }

        if (!selectionCardManager.IsLegendStoneSelected())
        {
            return;
        }

        // Submit the deck
        selectionCardManager.SubmitDeck();
    }
}
