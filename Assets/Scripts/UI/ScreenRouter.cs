using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ScreenRouter : MonoBehaviour
{
    [System.Serializable]
    public class ScreenRef
    {
        public string name;
        public GameObject root;
    }

    public List<ScreenRef> screens = new List<ScreenRef>();
    public string startScreen = "Main";

    private Dictionary<string, GameObject> _map;

    void Awake()
    {
        _map = screens.Where(s => s != null && s.root != null)
                      .GroupBy(s => s.name)
                      .ToDictionary(g => g.Key, g => g.First().root);
        Show(startScreen);
    }

    public void Show(string name)
    {
        foreach (var kv in _map)
            kv.Value.SetActive(kv.Key == name);
    }
}