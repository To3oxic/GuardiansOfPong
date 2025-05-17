using UnityEngine;

public class DontDestroyOnLoadHelper : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}