using UnityEngine;

/// Ensures DeckRegistry.Instance is initialized in non-game scenes (e.g., Main Menu)
/// by loading the DeckRegistry ScriptableObject from Resources and calling InitializeRegistry().
/// Place this on any GameObject in your Main Menu scene.
public class DeckRegistryLoader : MonoBehaviour
{
    [Tooltip("Optional: Assign a DeckRegistry asset to bypass Resources.Load lookup.")]
    public DeckRegistry registryAsset;

    [Header("Resources Settings")]
    [Tooltip("The name of the DeckRegistry asset inside a Resources folder. Default: 'DeckRegistry'.")]
    public string resourcesKey = "DeckRegistry";

    private void Awake()
    {
        if (DeckRegistry.Instance != null)
            return; // Already initialized (e.g., came from a previous scene)

        DeckRegistry loaded = registryAsset != null ? registryAsset : Resources.Load<DeckRegistry>(resourcesKey);
        if (loaded != null)
        {
            loaded.InitializeRegistry();
        }
    }
}
