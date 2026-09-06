using UnityEngine;

[System.Serializable]
public class GameStartAnimationSetup : MonoBehaviour
{
    [Header("Setup Instructions")]
    [Multiline(10)]
    public string instructions = @"
GAME START ANIMATION SETUP:

1. CAMERA POSITIONS:
   - Create empty GameObjects for camera start/end positions
   - Start position: High above arena (y=20), looking down (rotation: 90,0,0)
   - Final position: Normal gameplay camera position

2. LEGEND STONE POSITIONS:
   - Create empty GameObjects for underground hatch positions
   - Hatch positions: Same X,Z as final but Y = -2 (underground)
   - Final positions: Normal LegendStone positions in scene

3. PARTICLE EFFECTS:
   - Add dust particle effects at each hatch position
   - Configure to play when stones rise from ground

4. AUDIO:
   - Assign countdown beeps and rising sounds
   - Use separate AudioSources for different effects

5. COUNTDOWN UI:
   - Create TextMeshPro text for countdown display
   - Should be initially inactive (script will enable it)
";

    [Header("Quick Setup")]
    public Transform arenaCenter;
    public float cameraStartHeight = 20f;
    public float legendStoneUndergroundDepth = -2f;

    [Space]
    [Button("Auto Setup Positions")]
    public bool autoSetupButton;

    [Button("Create Camera Start Position")]
    public bool createCameraStartButton;

    [Button("Create Legend Stone Hatch Positions")]
    public bool createHatchPositionsButton;

    void OnValidate()
    {
        if (autoSetupButton)
        {
            autoSetupButton = false;
            AutoSetupPositions();
        }

        if (createCameraStartButton)
        {
            createCameraStartButton = false;
            CreateCameraStartPosition();
        }

        if (createHatchPositionsButton)
        {
            createHatchPositionsButton = false;
            CreateHatchPositions();
        }
    }

    private void AutoSetupPositions()
    {
        CreateCameraStartPosition();
        CreateHatchPositions();
    }

    private void CreateCameraStartPosition()
    {
        if (arenaCenter == null)
        {
            return;
        }

        GameObject cameraStart = new GameObject("CameraStartPosition");
        cameraStart.transform.parent = transform;
        cameraStart.transform.position = arenaCenter.position + Vector3.up * cameraStartHeight;
        cameraStart.transform.rotation = Quaternion.Euler(90, 0, 0);

        // Add visual indicator
        var icon = cameraStart.AddComponent<CameraSetupIcon>();
    }

    private void CreateHatchPositions()
    {
        // Find existing legend stone positions
        LegendStoneController[] stones = FindObjectsByType<LegendStoneController>(FindObjectsSortMode.None);

        if (stones.Length == 0)
        {
            return;
        }

        for (int i = 0; i < stones.Length; i++)
        {
            GameObject hatchPos = new GameObject($"LegendStoneHatch_{i}");
            hatchPos.transform.parent = transform;

            Vector3 stonePos = stones[i].transform.position;
            hatchPos.transform.position = new Vector3(stonePos.x, legendStoneUndergroundDepth, stonePos.z);
            hatchPos.transform.rotation = stones[i].transform.rotation;

            // Add visual indicator
            var icon = hatchPos.AddComponent<HatchSetupIcon>();
        }
    }
}

// Visual indicator components for editor
public class CameraSetupIcon : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}

public class HatchSetupIcon : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        Gizmos.DrawRay(transform.position, Vector3.up * 3f);
    }
}

// Custom button attribute for inspector buttons
public class ButtonAttribute : PropertyAttribute
{
    public string MethodName { get; }

    public ButtonAttribute(string methodName)
    {
        MethodName = methodName;
    }
}