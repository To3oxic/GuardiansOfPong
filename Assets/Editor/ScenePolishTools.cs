// Editor-only helpers for iterating on GameScene visuals without hand-editing the
// scene YAML. Every action supports Undo and marks the scene dirty; nothing is
// written to disk until you save the scene.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScenePolishTools
{
    // GameObjects whose name contains this are the decorative path point-lights.
    const string StoneLightNameHint = "Stone Light";

    // ---------------------------------------------------------------- reporting

    [MenuItem("Tools/Scene Polish/Report Lights", priority = 0)]
    static void ReportLights()
    {
        var all = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int realtime = 0, mixed = 0, baked = 0;
        int point = 0, spot = 0, dir = 0, area = 0, shadowed = 0;

        foreach (var l in all)
        {
            switch (l.lightmapBakeType)
            {
                case LightmapBakeType.Realtime: realtime++; break;
                case LightmapBakeType.Mixed: mixed++; break;
                case LightmapBakeType.Baked: baked++; break;
            }
            switch (l.type)
            {
                case LightType.Point: point++; break;
                case LightType.Spot: spot++; break;
                case LightType.Directional: dir++; break;
                default: area++; break;
            }
            if (l.shadows != LightShadows.None) shadowed++;
        }

        var stone = FindStoneLights();
        var sb = new StringBuilder();
        sb.AppendLine($"{all.Length} lights: {realtime} Realtime / {mixed} Mixed / {baked} Baked");
        sb.AppendLine($"types: {point} Point, {spot} Spot, {dir} Directional, {area} Area  ({shadowed} cast shadows)");
        sb.AppendLine();
        sb.AppendLine($"'{StoneLightNameHint}' point lights: {stone.Length}");
        if (stone.Length > 0)
        {
            var s = stone[0];
            sb.AppendLine($"  sample -> intensity {s.intensity}, range {s.range}, color {s.color}");
            sb.AppendLine($"           bake {s.lightmapBakeType}, shadows {s.shadows}, enabled {s.enabled}");
            int live = stone.Count(x => x.enabled && x.lightmapBakeType != LightmapBakeType.Baked);
            sb.AppendLine($"  {live} of these hit the renderer every frame (expensive on mobile).");
        }

        Debug.Log("[ScenePolish] " + sb);
        EditorUtility.DisplayDialog("Scene Polish — Lights", sb.ToString(), "OK");
    }

    // ------------------------------------------------------------ stone lights

    [MenuItem("Tools/Scene Polish/Stone Lights/Toggle Enabled (A-B test)", priority = 20)]
    static void ToggleStoneLights()
    {
        var lights = FindStoneLights();
        if (Bail(lights.Length)) return;

        bool anyOn = lights.Any(l => l.enabled);
        Undo.RecordObjects(lights, "Toggle Stone Lights");
        foreach (var l in lights) l.enabled = !anyOn;
        MarkDirty(lights);
        Say($"{lights.Length} stone lights -> {(!anyOn ? "ENABLED" : "DISABLED")}.\n\n" +
            "Check the Game view. The glowing path stones use an emissive material, so the glow " +
            "should survive with the lights off. If so, use 'Delete GameObjects' (free perf) " +
            "or 'Set to Baked' (keeps a soft bounce, costs nothing at runtime after a bake).");
    }

    [MenuItem("Tools/Scene Polish/Stone Lights/Set to Baked", priority = 21)]
    static void BakeStoneLights()
    {
        var lights = FindStoneLights();
        if (Bail(lights.Length)) return;

        Undo.RecordObjects(lights, "Bake Stone Lights");
        foreach (var l in lights) l.lightmapBakeType = LightmapBakeType.Baked;
        MarkDirty(lights);
        Say($"{lights.Length} stone lights set to Baked.\n\n" +
            "They contribute nothing at runtime until you: mark the floor/rocks 'Contribute GI' " +
            "(Static), then Window > Rendering > Lighting > Generate Lighting.");
    }

    [MenuItem("Tools/Scene Polish/Stone Lights/Delete GameObjects", priority = 22)]
    static void DeleteStoneLights()
    {
        var lights = FindStoneLights();
        if (Bail(lights.Length)) return;

        if (!EditorUtility.DisplayDialog("Delete Stone Lights",
            $"Delete {lights.Length} '{StoneLightNameHint}' GameObjects? Undo is available.",
            "Delete", "Cancel")) return;

        foreach (var l in lights) Undo.DestroyObjectImmediate(l.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Say($"Deleted {lights.Length} stone light GameObjects.");
    }

    // ---------------------------------------------------------------- helpers

    static Light[] FindStoneLights()
    {
        return Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(l => l != null
                        && l.type == LightType.Point
                        && l.gameObject.name.Contains(StoneLightNameHint))
            .ToArray();
    }

    static bool Bail(int count)
    {
        if (count > 0) return false;
        Say($"No '{StoneLightNameHint}' point lights found in the open scene.");
        return true;
    }

    static void MarkDirty(Object[] objs)
    {
        foreach (var o in objs) if (o != null) EditorUtility.SetDirty(o);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static void Say(string msg)
    {
        Debug.Log("[ScenePolish] " + msg);
        EditorUtility.DisplayDialog("Scene Polish", msg, "OK");
    }
}
