using UnityEngine;
using System.Collections.Generic;

public class GameBoardZone : MonoBehaviour
{
    [Header("Board Setup")]
    [SerializeField] private Transform boardCenter;


    [Header("UI")]
    [SerializeField] private Transform uiPanel; // Parent for player info + start button

    private List<BaseUnitController> patrollingUnits = new List<BaseUnitController>();

    public void Initialize()
    {
        Debug.Log("[GameBoardZone] Initializing...");

        // Spawn the game board (visual only, no physics)
        SetupBoard();

        // Spawn units to patrol
        SpawnPatrollingUnits();

        // Position UI panel
        PositionUI();
    }

    private void SetupBoard()
    {
        // TODO: Instantiate a simplified game board
        // This should have:
        // - Ground/terrain visual
        // - Two paddle positions (visual only, no physics)
        // - Lighting and effects

        Debug.Log("[GameBoardZone] Setting up board visuals...");
    }

    private void SpawnPatrollingUnits()
    {
        // TODO: Spawn 3-5 units at different positions
        // Units should patrol between two waypoints

        Debug.Log("[GameBoardZone] Spawning patrol units...");

        // Example:
        // Vector3 pos1 = new Vector3(-5, 0, 5);
        // Vector3 pos2 = new Vector3(5, 0, 5);
        // SpawnPatrolUnit(BraveKnightPrefab, pos1, pos2);
    }

    private void PositionUI()
    {
        // TODO: Position player info panel at top
        // TODO: Position "Start Game" button at bottom-right

        Debug.Log("[GameBoardZone] Positioning UI...");
    }

    public void SpawnPatrolUnit(GameObject unitPrefab, Vector3 waypointA, Vector3 waypointB)
    {
        GameObject unitObj = Instantiate(unitPrefab, waypointA, Quaternion.identity, transform);
        BaseUnitController unit = unitObj.GetComponent<BaseUnitController>();

        if (unit != null)
        {
            // TODO: Initialize with patrol behavior
            // unit.SetPatrolWaypoints(waypointA, waypointB);
            patrollingUnits.Add(unit);
        }
    }

    public List<BaseUnitController> GetPatrollingUnits() => patrollingUnits;
}
