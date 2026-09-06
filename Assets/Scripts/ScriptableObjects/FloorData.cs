using UnityEngine;

[CreateAssetMenu(fileName = "NewFloorData", menuName = "Decks/Floor Data")]
public class FloorData : ScriptableObject
{
    public string floorId;
    public Material floorMaterial;
}
