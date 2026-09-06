using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Example implementation showing how to make PaddleController work with IHasHP interface
/// This can be used as a template for other objects that need HP tracking
/// </summary>
public class PaddleHPAdapter : NetworkBehaviour, IHasHP
{
    private PaddleController paddleController;

    private void Awake()
    {
        paddleController = GetComponent<PaddleController>();
        if (paddleController == null)
        {
        }
    }

    public float GetCurrentHP()
    {
        return paddleController != null ? paddleController.currentHP.Value : 0f;
    }

    public float GetMaxHP()
    {
        return paddleController != null ? paddleController.maxHP : 0f;
    }

    public float GetHPPercentage()
    {
        if (paddleController == null) return 0f;

        float maxHP = GetMaxHP();
        if (maxHP <= 0f) return 0f;

        return GetCurrentHP() / maxHP;
    }

    public bool IsAlive()
    {
        return GetCurrentHP() > 0f;
    }
}

/// <summary>
/// Example implementation for units that extend BaseUnitController
/// </summary>
public class UnitHPAdapter : NetworkBehaviour, IHasHP
{
    private BaseUnitController unitController;

    private void Awake()
    {
        unitController = GetComponent<BaseUnitController>();
        if (unitController == null)
        {
        }
    }

    public float GetCurrentHP()
    {
        if (unitController == null) return 0f;

        // Access the protected currentHP field via reflection
        var currentHPField = typeof(BaseUnitController).GetField("currentHP",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (currentHPField != null)
        {
            var networkVar = currentHPField.GetValue(unitController) as NetworkVariable<int>;
            return networkVar?.Value ?? 0f;
        }

        return 0f;
    }

    public float GetMaxHP()
    {
        if (unitController == null) return 0f;

        // Access the protected maxHP field via reflection
        var maxHPField = typeof(BaseUnitController).GetField("maxHP",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (maxHPField != null)
        {
            return (int)maxHPField.GetValue(unitController);
        }

        return 0f;
    }

    public float GetHPPercentage()
    {
        float maxHP = GetMaxHP();
        if (maxHP <= 0f) return 0f;

        return GetCurrentHP() / maxHP;
    }

    public bool IsAlive()
    {
        return GetCurrentHP() > 0f;
    }
}

/// <summary>
/// Simple HP adapter for objects that implement IPlaceable but need HP tracking
/// This assumes the object has a simple HP system that we can access
/// </summary>
public class PlaceableHPAdapter : MonoBehaviour, IHasHP
{
    [Header("HP Configuration")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public float GetCurrentHP()
    {
        return currentHealth;
    }

    public float GetMaxHP()
    {
        return maxHealth;
    }

    public float GetHPPercentage()
    {
        if (maxHealth <= 0) return 0f;
        return (float)currentHealth / maxHealth;
    }

    public bool IsAlive()
    {
        return currentHealth > 0;
    }

    // Method to take damage (call this from IPlaceable.TakeDamage)
    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
    }

    // Method to heal
    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
}