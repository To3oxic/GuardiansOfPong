using UnityEngine;
using System.Collections.Generic;

public class ManaVisualizer : MonoBehaviour
{
    [SerializeField] private List<ManaCell> manaCells = new List<ManaCell>();

    private float maxMana = 10f;
    private float cellCount = 10f;
    private float lastManaValue = -1f;

    private void Start()
    {
        // Initialize all cells
        if (manaCells.Count > 0)
        {
            InitializeCells();
        }

        // Reset all cells to empty state
        ResetManaDisplay();
    }

    /// <summary>
    /// Set up the linked list structure for cells
    /// </summary>
    private void InitializeCells()
    {
        for (int i = 0; i < manaCells.Count; i++)
        {
            if (manaCells[i] == null) continue;

            // Link cells together
            if (i > 0)
            {
                manaCells[i].previous = manaCells[i - 1];
            }

            if (i < manaCells.Count - 1)
            {
                manaCells[i].next = manaCells[i + 1];
            }
        }
    }

    /// <summary>
    /// Updates the mana bar visualization based on current mana value
    /// </summary>
    public void UpdateManaDisplay(float currentMana)
    {
        // Calculate how many cells should be filled

        // First, empty all cells
        foreach (ManaCell cell in manaCells)
        {
            if (cell != null)
            {
                cell.ResetCell();
            }
        }

        // Then instantly fill the required number of cells
        for (int i = 0; i < currentMana; i++)
        {
            if (manaCells[i] != null)
            {
                manaCells[i].InstantFill();
            }
        }
    }

    /// <summary>
    /// Reset all mana cells to empty state
    /// </summary>
    public void ResetManaDisplay()
    {
        foreach (ManaCell cell in manaCells)
        {
            if (cell != null)
            {
                cell.ResetCell();
            }
        }
    }

    /// <summary>
    /// Find the next empty cell using the linked list and animate its fill
    /// </summary>
    public void AnimateManaFill()
    {
        // Start from the first cell and traverse using the linked list
        ManaCell currentCell = manaCells.Count > 0 ? manaCells[0] : null;

        while (currentCell != null)
        {
            if (!currentCell.filled)
            {
                // Found an empty cell, animate it
                currentCell.PlayFillAnimation();
                return;
            }

            // Move to the next cell in the linked list
            currentCell = currentCell.next;
        }
    }

    /// <summary>
    /// Set the maximum mana value for calculations
    /// </summary>
    public void SetMaxMana(float max)
    {
        maxMana = Mathf.Max(1f, max);
    }

    /// <summary>
    /// Set the number of mana cells (default 10)
    /// </summary>
    public void SetCellCount(float count)
    {
        cellCount = Mathf.Max(1f, count);
    }
}
