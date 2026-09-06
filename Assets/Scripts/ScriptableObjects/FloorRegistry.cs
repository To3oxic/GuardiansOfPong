using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "FloorRegistry", menuName = "Decks/Floor Registry")]
public class FloorRegistry : ScriptableObject
{
    public List<FloorData> allFloors;

    public static FloorRegistry Instance;

    public void InitializeRegistry()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public FloorData GetFloorById(string id)
    {
        return allFloors.FirstOrDefault(f => f.floorId == id);
    }
}
