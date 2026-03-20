// UnityStateExporter.cs
// ============================================================
// Place this file in your Unity project under:
//   Assets/Editor/UnityStateExporter.cs
//
// What it does:
//   - Auto-exports unity_state.json to your project root whenever
//     your selection changes or a new scene is opened.
//   - Process-UnityScreenshot.ps1 reads this file to inject
//     "X-Ray" context into the LLM vision prompt, so the model
//     knows exactly which GameObject you have selected and what
//     components it has — without having to guess from the screenshot.
//
// Output path: <ProjectRoot>/unity_state.json
//   (parent of Assets/ — matches $KnownProjects paths in the PS script)
//
// Manual trigger: Tools > Export Unity State for Greenshot
// ============================================================

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UnityStateExporter
{
    // Path written to — must match the parent of the Assets path
    // configured in $KnownProjects inside Process-UnityScreenshot.ps1
    private static string OutputPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "unity_state.json"));

    // Named handler required — lambda would trigger UDR0004
    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => ExportState();

    // UDR0001 is suppressed here because these are Editor-only events
    // (Selection, EditorSceneManager, EditorApplication). The UDR analyzer
    // incorrectly demands a [RuntimeInitializeOnLoadMethod] counterpart for
    // all static subscriptions, but these events have no runtime equivalent.
    // [InitializeOnLoadMethod] handles re-subscription on every domain reload.
#pragma warning disable UDR0001
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        Selection.selectionChanged         += ExportState;
        EditorSceneManager.sceneOpened     += OnSceneOpened;
        EditorApplication.hierarchyChanged += ExportState;
    }
#pragma warning restore UDR0001

    [MenuItem("Tools/Export Unity State for Greenshot")]
    public static void ExportState()
    {
        try
        {
            string json = BuildStateJson();
            File.WriteAllText(OutputPath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UnityStateExporter] Could not write state: {e.Message}");
        }
    }

    // --------------------------------------------------------
    // JSON builder (manual — avoids Newtonsoft dependency)
    // --------------------------------------------------------
    private static string BuildStateJson()
    {
        var scene           = EditorSceneManager.GetActiveScene();
        var selectedObjects = Selection.gameObjects;
        var rootNames       = scene.GetRootGameObjects()
                                   .Select(g => EscapeJson(g.name))
                                   .Take(50)   // cap for very large scenes
                                   .ToArray();

        var selectedJson = string.Join(",\n    ", selectedObjects.Select(BuildObjectJson));
        var rootJson     = string.Join(", ", rootNames.Select(n => $"\"{n}\""));

        return $@"{{
  ""exported_at"": ""{DateTime.UtcNow:o}"",
  ""project_name"": ""{EscapeJson(Path.GetFileName(Path.GetDirectoryName(Application.dataPath)))}"",
  ""project_path"": ""{EscapeJson(Path.GetDirectoryName(Application.dataPath))}"",
  ""unity_version"": ""{Application.unityVersion}"",
  ""active_scene"": ""{EscapeJson(scene.name)}"",
  ""scene_path"": ""{EscapeJson(scene.path)}"",
  ""scene_object_count"": {CountAllObjects(scene)},
  ""hierarchy_root_objects"": [{rootJson}],
  ""selected_objects"": [
    {selectedJson}
  ]
}}";
    }

    private static string BuildObjectJson(GameObject go)
    {
        var t          = go.transform;
        var components = go.GetComponents<Component>()
                           .Where(c => c != null)
                           .Select(c => $"\"{EscapeJson(c.GetType().Name)}\"")
                           .ToArray();

        // ---- component detail: inspect well-known ML-Agents types ----
        var componentDetails = BuildComponentDetails(go);

        string pos = $"[{t.position.x:F3}, {t.position.y:F3}, {t.position.z:F3}]";
        string rot = $"[{t.eulerAngles.x:F1}, {t.eulerAngles.y:F1}, {t.eulerAngles.z:F1}]";
        string scl = $"[{t.localScale.x:F3}, {t.localScale.y:F3}, {t.localScale.z:F3}]";

        return $@"{{
      ""name"": ""{EscapeJson(go.name)}"",
      ""path"": ""{EscapeJson(GetHierarchyPath(go))}"",
      ""tag"": ""{EscapeJson(go.tag)}"",
      ""layer"": ""{EscapeJson(LayerMask.LayerToName(go.layer))}"",
      ""active"": {go.activeInHierarchy.ToString().ToLower()},
      ""components"": [{string.Join(", ", components)}],
      ""transform"": {{ ""position"": {pos}, ""rotation"": {rot}, ""scale"": {scl} }}{componentDetails}
    }}";
    }

    /// <summary>
    /// Extract structured data from specific component types your project uses.
    /// Add cases here as your project evolves — the richer this is, the better
    /// the LLM vision prompt context will be.
    /// </summary>
    private static string BuildComponentDetails(GameObject go)
    {
        var parts = new List<string>();

        // ML-Agents: BehaviorParameters
        var bp = go.GetComponent("BehaviorParameters");
        if (bp != null)
        {
            var so = new SerializedObject(bp);
            string behaviorName  = so.FindProperty("m_BehaviorName")?.stringValue ?? "";
            string inferenceDevice = so.FindProperty("m_InferenceDevice")?.enumDisplayNames?
                                       [so.FindProperty("m_InferenceDevice")?.enumValueIndex ?? 0] ?? "";
            bool   useChildSensors = so.FindProperty("m_UseChildSensors")?.boolValue ?? false;
            int    obsSize         = so.FindProperty("m_BrainParameters.vectorObservationSize")?.intValue ?? 0;
            int    actSize         = so.FindProperty("m_BrainParameters.m_NumDiscreteActions")?.intValue ?? 0;

            parts.Add($@"""behavior_parameters"": {{
          ""behavior_name"": ""{EscapeJson(behaviorName)}"",
          ""inference_device"": ""{inferenceDevice}"",
          ""use_child_sensors"": {useChildSensors.ToString().ToLower()},
          ""obs_size"": {obsSize},
          ""discrete_action_size"": {actSize}
        }}");
        }

        // ML-Agents: DecisionRequester
        var dr = go.GetComponent("DecisionRequester");
        if (dr != null)
        {
            var so = new SerializedObject(dr);
            int  period       = so.FindProperty("DecisionPeriod")?.intValue ?? 0;
            bool takeActions  = so.FindProperty("TakeActionsBetweenDecisions")?.boolValue ?? false;
            parts.Add($@"""decision_requester"": {{
          ""decision_period"": {period},
          ""take_actions_between_decisions"": {takeActions.ToString().ToLower()}
        }}");
        }

        // Animator: current state
        var animator = go.GetComponent<Animator>();
        if (animator != null && Application.isPlaying)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            parts.Add($@"""animator_state"": {{
          ""runtime_controller"": ""{EscapeJson(animator.runtimeAnimatorController?.name ?? "none")}"",
          ""current_state_hash"": {info.shortNameHash},
          ""normalized_time"": {info.normalizedTime:F3}
        }}");
        }
        else if (animator != null)
        {
            parts.Add($@"""animator"": {{
          ""controller"": ""{EscapeJson(animator.runtimeAnimatorController?.name ?? "none")}""
        }}");
        }

        // Network Object (Unity Netcode / Mirror) — component name varies
        var netObj = go.GetComponent("NetworkObject") ?? go.GetComponent("NetworkIdentity");
        if (netObj != null)
        {
            var so = new SerializedObject(netObj);
            string netId = so.FindProperty("NetworkObjectId")?.intValue.ToString()
                        ?? so.FindProperty("netId")?.intValue.ToString()
                        ?? "unknown";
            parts.Add($@"""network_object"": {{ ""id"": ""{netId}"" }}");
        }

        if (parts.Count == 0)
            return "";

        return ",\n      " + string.Join(",\n      ", parts);
    }

    // --------------------------------------------------------
    // Helpers
    // --------------------------------------------------------
    private static string GetHierarchyPath(GameObject go)
    {
        var parts   = new List<string>();
        var current = go.transform;
        while (current != null)
        {
            parts.Insert(0, current.name);
            current = current.parent;
        }
        return "/" + string.Join("/", parts);
    }

    private static int CountAllObjects(Scene scene)
    {
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
            count += CountRecursive(root.transform);
        return count;
    }

    private static int CountRecursive(Transform t)
    {
        int count = 1;
        for (int i = 0; i < t.childCount; i++)
            count += CountRecursive(t.GetChild(i));
        return count;
    }

    private static string EscapeJson(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                 .Replace("\n", "\\n").Replace("\r", "\\r")
                 .Replace(Path.DirectorySeparatorChar.ToString(), "/");
}
