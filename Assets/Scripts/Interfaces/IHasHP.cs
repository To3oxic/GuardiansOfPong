using Unity.Netcode;

/// <summary>
/// Interface for objects that have trackable HP systems
/// </summary>
public interface IHasHP
{
    /// <summary>
    /// Get the current HP value
    /// </summary>
    float GetCurrentHP();

    /// <summary>
    /// Get the maximum HP value
    /// </summary>
    float GetMaxHP();

    /// <summary>
    /// Get the current HP as a percentage (0.0 to 1.0)
    /// </summary>
    float GetHPPercentage();

    /// <summary>
    /// Check if the object is currently alive (HP > 0)
    /// </summary>
    bool IsAlive();
}