using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuCubeRotator menuCubeRotator;
    [SerializeField] private CanvasGroup selectionUIPanel;

    [Header("Toggle Button Settings")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private RectTransform containerToResize;

    [Header("Guardian Display")]
    [SerializeField] private Transform guardianSpawnPoint; // Transform where guardian prefab will be spawned
    [SerializeField] private DeckRegistry deckRegistry; // Reference to deck registry for guardian prefabs

    private Vector2 originalSize;
    private Vector2 shrunkSize;
    private bool isExpanded = true;
    private Coroutine showCoroutine;
    private GameObject currentGuardianInstance;
    private SelectionCardManager selectionCardManager;

    private void Start()
    {
        // Store original container size
        if (containerToResize != null)
        {
            originalSize = containerToResize.sizeDelta;
            shrunkSize = new Vector2(originalSize.x, 20f); // Height to 20, keep width
        }

        // Setup button click
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(OnToggleButtonPressed);
        }

        // Hide UI by default
        if (selectionUIPanel != null)
        {
            selectionUIPanel.alpha = 0;
            selectionUIPanel.interactable = false;
            selectionUIPanel.blocksRaycasts = false;
        }

        // Shrink the cards container by default
        ShrinkContainer();

        // Find SelectionCardManager
        selectionCardManager = FindFirstObjectByType<SelectionCardManager>();

        // Spawn guardian when deck is loaded
        SpawnSelectedGuardian();
    }

    private void Update()
    {
        // Update is no longer needed - Activate() is called from MenuCubeRotator
    }

    public void Activate()
    {
        Debug.Log("[SelectionUI] Activate called.");
        // Stop any existing coroutine
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
        }

        // Start the show sequence
        showCoroutine = StartCoroutine(ShowUIAfterDelay());
    }

    public void Deactivate()
    {
        // Stop coroutine if running
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
            showCoroutine = null;
        }

        HideUI();
    }
    private IEnumerator ShowUIAfterDelay()
    {
        // Wait 2 seconds
        yield return new WaitForSeconds(2f);

        // Fade in
        float elapsed = 0f;
        float fadeDuration = 0.5f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            selectionUIPanel.alpha = Mathf.Lerp(0, 1, t);
            yield return null;
        }

        selectionUIPanel.alpha = 1;
        selectionUIPanel.interactable = true;
        selectionUIPanel.blocksRaycasts = true;

        showCoroutine = null;
    }

    private void HideUI()
    {
        selectionUIPanel.alpha = 0;
        selectionUIPanel.interactable = false;
        selectionUIPanel.blocksRaycasts = false;
    }

    private void OnToggleButtonPressed()
    {
        Debug.Log("[SelectionUI] Toggle button pressed.");
        if (containerToResize == null)
            return;

        isExpanded = !isExpanded;

        Vector2 targetSize = isExpanded ? originalSize : shrunkSize;
        containerToResize.sizeDelta = targetSize;

        Debug.Log($"[SelectionUI] Container toggled. Expanded: {isExpanded}, Size: {targetSize}");
    }

    /// <summary>
    /// Shrink the cards container to default collapsed state
    /// </summary>
    private void ShrinkContainer()
    {
        if (containerToResize == null)
            return;

        isExpanded = false;
        containerToResize.sizeDelta = shrunkSize;
        Debug.Log($"[SelectionUI] Container shrunk by default. Size: {shrunkSize}");
    }

    /// <summary>
    /// Spawn the selected guardian's prefab at the guardian spawn point
    /// </summary>
    public void SpawnSelectedGuardian()
    {
        if (selectionCardManager == null)
        {
            Debug.LogWarning("[SelectionUI] SelectionCardManager not found!");
            return;
        }

        SelectionCardHolder guardianHolder = selectionCardManager.GetGuardian();
        if (guardianHolder == null)
        {
            Debug.LogWarning("[SelectionUI] No guardian selected!");
            return;
        }

        object guardianData = guardianHolder.GetPublicCardData();
        if (guardianData is not CastingLegendData castingLegend)
        {
            Debug.LogWarning("[SelectionUI] Guardian data is not CastingLegendData!");
            return;
        }

        // Find the guardian prefab in the registry
        if (deckRegistry == null || deckRegistry.allGuardians == null)
        {
            Debug.LogError("[SelectionUI] DeckRegistry not found or has no guardians!");
            return;
        }

        GameObject guardianPrefab = null;
        foreach (var guardian in deckRegistry.allGuardians)
        {
            if (guardian.legendName == castingLegend.legendName)
            {
                guardianPrefab = guardian.legendAvatarPrefab;
                break;
            }
        }

        if (guardianPrefab == null)
        {
            Debug.LogError($"[SelectionUI] Guardian prefab not found for: {castingLegend.legendName}");
            return;
        }

        // Destroy existing guardian if any
        if (currentGuardianInstance != null)
        {
            Destroy(currentGuardianInstance);
        }

        // Spawn the guardian at the spawn point
        if (guardianSpawnPoint != null)
        {
            currentGuardianInstance = Instantiate(guardianPrefab, guardianSpawnPoint.position,
                guardianSpawnPoint.rotation);
            currentGuardianInstance.transform.SetParent(guardianSpawnPoint.parent);
            Debug.Log($"[SelectionUI] Spawned guardian: {castingLegend.legendName}");
        }
        else
        {
            Debug.LogWarning("[SelectionUI] Guardian spawn point not assigned!");
        }
    }

    /// <summary>
    /// Update guardian display when deck selection changes
    /// </summary>
    public void UpdateGuardianDisplay()
    {
        SpawnSelectedGuardian();
    }
}

