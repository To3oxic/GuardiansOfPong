// Editor-only helpers for iterating on GameScene visuals without hand-editing the
// scene YAML. Every action supports Undo and marks the scene dirty; nothing is
// written to disk until you save the scene.
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

    [MenuItem("Tools/Scene Polish/Stone Lights/Set to Realtime (revert)", priority = 20)]
    static void RealtimeStoneLights()
    {
        var lights = FindStoneLights();
        if (Bail(lights.Length)) return;

        Undo.RecordObjects(lights, "Stone Lights -> Realtime");
        foreach (var l in lights)
        {
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.enabled = true;
        }
        MarkDirty(lights);
        Say($"{lights.Length} stone lights back to Realtime + enabled.\n\n" +
            "Also run Window > Rendering > Lighting > (dropdown) > Clear Baked Data to drop the lightmaps.");
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

    // ------------------------------------------------------- post-processing

    const string GameProfilePath = "Assets/Settings/GameVolumeProfile.asset";

    [MenuItem("Tools/Scene Polish/Create Clean Volume Profile", priority = 30)]
    static void CreateCleanVolumeProfile()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GameProfilePath) != null)
        {
            if (!EditorUtility.DisplayDialog("Overwrite?",
                    $"{GameProfilePath} already exists. Rebuild it from scratch?", "Rebuild", "Cancel"))
                return;
            AssetDatabase.DeleteAsset(GameProfilePath);
        }

        var dir = System.IO.Path.GetDirectoryName(GameProfilePath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        // VolumeProfileFactory is the official editor API — it creates the profile
        // asset and each override sub-asset and saves them correctly.
        var profile = VolumeProfileFactory.CreateVolumeProfileAtPath(GameProfilePath);

        // Deliberate, mobile-friendly stack. No motion blur / DoF / chromatic
        // aberration / film grain / lens flare — those are the expensive ones.
        var tone = VolumeProfileFactory.CreateVolumeComponent<Tonemapping>(profile, false, true);
        tone.mode.Override(TonemappingMode.ACES);

        var bloom = VolumeProfileFactory.CreateVolumeComponent<Bloom>(profile, false, true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(0.80f, 0.94f, 1f));

        var vig = VolumeProfileFactory.CreateVolumeComponent<Vignette>(profile, false, true);
        vig.intensity.Override(0.30f);
        vig.smoothness.Override(0.45f);

        var col = VolumeProfileFactory.CreateVolumeComponent<ColorAdjustments>(profile, false, true);
        col.contrast.Override(8f);
        col.saturation.Override(6f);
        col.colorFilter.Override(new Color(1f, 0.99f, 0.96f));

        var wb = VolumeProfileFactory.CreateVolumeComponent<WhiteBalance>(profile, false, true);
        wb.temperature.Override(-6f);

        foreach (var c in profile.components) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(GameProfilePath, ImportAssetOptions.ForceUpdate);

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
        Say($"Built {GameProfilePath} with {profile.components.Count} overrides " +
            "(Tonemapping ACES, Bloom, Vignette, Color Adjustments, White Balance).\n\n" +
            "Assign it: select 'Global Magical Volume' in the Hierarchy -> Volume component -> " +
            "drag this asset into the Profile field (replacing DefaultVolumeProfile). " +
            "Tune values live on the asset while the Game view is visible.");
    }

    // -------------------------------------------------------------------- fog

    [MenuItem("Tools/Scene Polish/Toggle Depth Fog", priority = 31)]
    static void ToggleDepthFog()
    {
        // RenderSettings has no Undo-friendly object; this menu item is its own toggle.
        if (!RenderSettings.fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;                 // soft, tune in Lighting > Environment
            RenderSettings.fogColor = new Color(0.34f, 0.42f, 0.55f); // cool blue-grey
            Say("Depth fog ON — ExpSquared, density 0.0035, cool blue-grey.\n\n" +
                "Tune it in Window > Rendering > Lighting > Environment (Fog section). " +
                "Lower density = subtler. Match the color to your skybox horizon.");
        }
        else
        {
            RenderSettings.fog = false;
            Say("Depth fog OFF.");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------ lightmap prep

    // Ancestor names that mark non-moving scenery safe to bake.
    static readonly string[] DecorRootHints =
        { "Forest", "Magical Stone Paths", "Ground", "Environment", "Rocks", "Rock ", "Arena", "Decor", "Scenery" };

    [MenuItem("Tools/Scene Polish/Lightmap Prep/Mark Decor Static", priority = 40)]
    static void MarkDecorStatic()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        const StaticEditorFlags want = StaticEditorFlags.ContributeGI
                                     | StaticEditorFlags.BatchingStatic
                                     | StaticEditorFlags.OccludeeStatic
                                     | StaticEditorFlags.ReflectionProbeStatic;

        int flagged = 0, skippedDynamic = 0;
        foreach (var r in renderers)
        {
            var go = r.gameObject;

            // never bake anything that is spawned / driven at runtime
            if (HasRuntimeBehaviour(go)) { skippedDynamic++; continue; }
            if (!UnderDecorRoot(go.transform)) continue;

            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((flags & want) == want) continue;

            Undo.RecordObject(go, "Mark Decor Static");
            GameObjectUtility.SetStaticEditorFlags(go, flags | want);
            flagged++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Say($"Marked {flagged} decor renderers static (ContributeGI + Batching). " +
            $"Skipped {skippedDynamic} with runtime scripts.\n\n" +
            "Next: Stone Lights > Set to Baked, then Window > Rendering > Lighting > Generate Lighting.");
    }

    static bool UnderDecorRoot(Transform t)
    {
        for (; t != null; t = t.parent)
            foreach (var h in DecorRootHints)
                if (t.name.Contains(h)) return true;
        return false;
    }

    static bool HasRuntimeBehaviour(GameObject go)
    {
        foreach (var mb in go.GetComponentsInParent<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var n = mb.GetType().Name;
            if (n.Contains("Network") || n.Contains("Controller") || n.Contains("Ball")
                || n.Contains("Paddle") || n.Contains("Guardian") || n.Contains("Manager"))
                return true;
        }
        return false;
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
