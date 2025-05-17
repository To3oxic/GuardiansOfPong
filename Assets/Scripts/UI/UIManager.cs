using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [System.Serializable]
    public class UIScreen
    {
        public string screenName;
        public GameObject screenObject;
    }

    public List<UIScreen> uiScreens = new List<UIScreen>();
    private Dictionary<string, GameObject> screenRegistry = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make UIManager persistent across scenes if needed
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        foreach (var screen in uiScreens)
        {
            if (!string.IsNullOrEmpty(screen.screenName) && screen.screenObject != null)
            {
                screenRegistry[screen.screenName] = screen.screenObject;
            }
        }
    }

    public void ShowScreen(string screenName)
    {
        HideAllScreens();
        if (screenRegistry.TryGetValue(screenName, out GameObject screenToShow))
        {
            screenToShow.SetActive(true);
            Debug.Log($"[UIManager] Showing screen: {screenName}");
        }
        else
        {
            Debug.LogWarning($"[UIManager] Screen '{screenName}' not found in registry.");
        }
    }

    public void HideScreen(string screenName)
    {
        if (screenRegistry.TryGetValue(screenName, out GameObject screenToHide))
        {
            screenToHide.SetActive(false);
            Debug.Log($"[UIManager] Hiding screen: {screenName}");
        }
    }

    public void HideAllScreens()
    {
        foreach (var screen in screenRegistry.Values)
        {
            screen.SetActive(false);
        }
        Debug.Log("[UIManager] Hid all registered screens.");
    }

    // Helper to get a screen object if needed by other systems
    public GameObject GetScreen(string screenName)
    {
        screenRegistry.TryGetValue(screenName, out GameObject screen);
        return screen;
    }
}