using UnityEngine;

/// <summary>
/// Conditional debug logging system
/// Disable all logs in mobile builds to improve performance
/// </summary>
public static class DebugHelper
{
    // Set to false in mobile builds to disable all logging
#if UNITY_ANDROID || UNITY_IOS
    private static readonly bool ENABLE_LOGS = false;
#else
    private static readonly bool ENABLE_LOGS = true;
#endif

    public static void Log(string message)
    {
        if (ENABLE_LOGS)
            Debug.Log(message);
    }

    public static void LogWarning(string message)
    {
        if (ENABLE_LOGS)
            Debug.LogWarning(message);
    }

    public static void LogError(string message)
    {
        // ALWAYS show errors, even in production
        Debug.LogError(message);
    }
}
