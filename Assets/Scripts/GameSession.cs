using UnityEngine;
// Simple static class to hold session state like login status or user data (simulated)
public static class GameSession
{
    public static bool IsLoggedIn { get; set; } = false; // Default to not logged in
    // Example of simulated user data you might "fetch"
    public static string UserName { get; private set; } = "Player123";
    public static int UserLevel { get; private set; } = 10;
    public static string UserRank { get; private set; } = "Gold III";
    public static void SimulateLogin(string username = "Player123", int level = 10, string rank = "Gold III")
    {
        IsLoggedIn = true;
        UserName = username;
        UserLevel = level;
        UserRank = rank;
    }
    public static void SimulateLogout()
    {
        IsLoggedIn = false;
    }
}
