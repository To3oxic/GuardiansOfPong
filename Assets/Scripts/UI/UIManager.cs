using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [System.Serializable]
    public class ScreenRef
    {
        public string name;
        public GameObject root;
    }

    public List<ScreenRef> screens = new List<ScreenRef>();
    public string startScreen = "MainMenuScreen";

    Dictionary<string, GameObject> map;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        map = screens.Where(s => s != null && s.root != null)
                     .GroupBy(s => s.name)
                     .ToDictionary(g => g.Key, g => g.First().root);

        ShowScreen(startScreen);
    }

    public void ShowScreen(string name)
    {
        foreach (var kv in map)
            kv.Value.SetActive(kv.Key == name);
    }
}