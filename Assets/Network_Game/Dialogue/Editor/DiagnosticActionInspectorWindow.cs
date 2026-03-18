using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Network_Game.Dialogue.MCP;
using UnityEditor;
using UnityEngine;

namespace Network_Game.Dialogue.Editor
{
    /// <summary>
    /// Lightweight diagnostics window for inspecting recent dialogue action chains
    /// and opening the most relevant source anchors directly from the editor.
    /// </summary>
    public sealed class DiagnosticActionInspectorWindow : EditorWindow
    {
        private const string MenuPath = "Network Game/MCP/Debug/Diagnostic Action Inspector";
        private const string WindowTitle = "Diagnostic Actions";
        private const double AutoRefreshIntervalSeconds = 1d;

        private readonly List<Dictionary<string, object>> _recommendedChecks = new();
        private readonly List<Dictionary<string, object>> _recentActionChains = new();

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private string _selectedActionId = string.Empty;
        private string _statusMessage = string.Empty;
        private double _lastRefreshAt;
        private DateTime _lastRefreshStamp;
        private Dictionary<string, object> _selectedActionChain;
        private Dictionary<string, object> _brainPacket;
        private Dictionary<string, object> _uiBehavior;
        private Dictionary<string, object> _uiPerformance;
        private string _brainPrompt = string.Empty;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<DiagnosticActionInspectorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            RefreshData();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!_autoRefresh)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRefreshAt < AutoRefreshIntervalSeconds)
            {
                return;
            }

            RefreshData();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawStatus();
            DrawBrainSummary();
            EditorGUILayout.Space(8f);
            DrawRecommendedChecks();
            EditorGUILayout.Space(8f);
            DrawRecentActionChains();
            EditorGUILayout.Space(8f);
            DrawSelectedActionChain();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    RefreshData();
                }

                _autoRefresh = GUILayout.Toggle(
                    _autoRefresh,
                    "Auto Refresh",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(100f)
                );

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Copy Brain Prompt", EditorStyles.toolbarButton, GUILayout.Width(125f)))
                {
                    EditorGUIUtility.systemCopyBuffer = _brainPrompt ?? string.Empty;
                    _statusMessage = string.IsNullOrWhiteSpace(_brainPrompt)
                        ? "Brain prompt is empty."
                        : "Copied diagnostic brain prompt.";
                }

                if (GUILayout.Button("Export Bundle", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    ExportIncidentBundle();
                }

                string refreshed =
                    _lastRefreshAt <= 0d
                        ? "not refreshed yet"
                        : _lastRefreshStamp.ToString("HH:mm:ss");
                GUILayout.Label($"Last UI update: {refreshed}", EditorStyles.miniLabel);
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Action Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Recent action chains come from the runtime diagnostics bridge. "
                    + "Recommended checks reflect the current brain priorities and expose source-backed breakpoint anchors.",
                MessageType.Info
            );

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.None);
            }
        }

        private void DrawBrainSummary()
        {
            EditorGUILayout.LabelField("Brain Summary", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (_brainPacket == null)
                {
                    EditorGUILayout.HelpBox(
                        "Diagnostic brain packet is not available yet.",
                        MessageType.Info
                    );
                    return;
                }

                EditorGUILayout.LabelField(
                    $"Objective: {DisplayOrFallback(GetString(_brainPacket, "objective"))}",
                    EditorStyles.boldLabel
                );
                EditorGUILayout.LabelField(
                    $"Scene: {DisplayOrFallback(GetString(_brainPacket, "scene_name"))}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"Phase: {DisplayOrFallback(GetString(_brainPacket, "current_phase"))}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"Run: {DisplayOrFallback(GetString(_brainPacket, "run_id"))}",
                    EditorStyles.miniLabel
                );

                string summary = GetString(_brainPacket, "summary");
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
                }

                DrawBrainVariableList("Top Priorities", GetList(_brainPacket, "top_priorities"));

                if (_uiBehavior != null || _uiPerformance != null)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("UI Diagnostics", EditorStyles.boldLabel);
                }

                if (_uiBehavior != null)
                {
                    EditorGUILayout.LabelField(
                        $"UI Id: {DisplayOrFallback(GetString(_uiBehavior, "ui_id"))}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Mode: {DisplayOrFallback(GetString(_uiBehavior, "ui_mode"))}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Input Capture: {DisplayOrFallback(GetString(_uiBehavior, "input_capture_state"))}",
                        EditorStyles.miniLabel
                    );
                    string renderState = GetString(_uiBehavior, "response_render_state");
                    if (!string.IsNullOrWhiteSpace(renderState))
                    {
                        EditorGUILayout.LabelField(
                            $"Render State: {renderState}",
                            EditorStyles.miniLabel
                        );
                    }
                }

                if (_uiPerformance != null)
                {
                    string operation = GetString(_uiPerformance, "operation");
                    string durationMs = GetString(_uiPerformance, "duration_ms");
                    string allocations = GetString(_uiPerformance, "gc_alloc_bytes");
                    EditorGUILayout.LabelField(
                        $"Perf: {DisplayOrFallback(operation)}  {DisplayOrFallback(durationMs)} ms  alloc={DisplayOrFallback(allocations)}",
                        EditorStyles.miniLabel
                    );
                }

                if (!string.IsNullOrWhiteSpace(_brainPrompt))
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Brain Prompt", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(
                        _brainPrompt,
                        EditorStyles.textArea,
                        GUILayout.MinHeight(Mathf.Max(64f, 18f * CountDisplayLines(_brainPrompt)))
                    );
                }
            }
        }

        private void DrawRecommendedChecks()
        {
            EditorGUILayout.LabelField("Recommended Checks", EditorStyles.boldLabel);

            if (_recommendedChecks.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No recommended checks are available yet. Enter Play Mode and trigger dialogue actions.",
                    MessageType.Info
                );
                return;
            }

            foreach (Dictionary<string, object> recommendation in _recommendedChecks)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string actionId = GetString(recommendation, "action_id");
                    string stage = GetString(recommendation, "stage");
                    string priority = GetString(recommendation, "priority");
                    string summary = GetString(recommendation, "summary");
                    string anchorId = GetString(
                        recommendation,
                        "recommended_breakpoint_anchor_id"
                    );
                    string location = GetString(
                        recommendation,
                        "recommended_breakpoint_location"
                    );
                    string query = GetString(recommendation, "recommended_mcp_query");

                    EditorGUILayout.LabelField(
                        $"{priority}  {summary}",
                        EditorStyles.boldLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Action: {DisplayOrFallback(actionId)}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Stage: {DisplayOrFallback(stage)}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Anchor: {DisplayOrFallback(anchorId)}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Location: {DisplayOrFallback(location)}",
                        EditorStyles.wordWrappedMiniLabel
                    );

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        EditorGUILayout.SelectableLabel(
                            query,
                            EditorStyles.textField,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight)
                        );
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = !string.IsNullOrWhiteSpace(actionId);
                        if (GUILayout.Button("Inspect Chain"))
                        {
                            LoadActionChain(actionId);
                        }

                        GUI.enabled = !string.IsNullOrWhiteSpace(query);
                        if (GUILayout.Button("Copy MCP Query"))
                        {
                            EditorGUIUtility.systemCopyBuffer = query;
                            _statusMessage = $"Copied MCP query for action '{actionId}'.";
                        }

                        GUI.enabled = !string.IsNullOrWhiteSpace(anchorId);
                        if (GUILayout.Button("Open Anchor"))
                        {
                            OpenAnchor(anchorId);
                        }

                        GUI.enabled = true;
                    }
                }
            }
        }

        private void DrawRecentActionChains()
        {
            EditorGUILayout.LabelField("Recent Action Chains", EditorStyles.boldLabel);

            if (_recentActionChains.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No action chains recorded yet.",
                    MessageType.Info
                );
                return;
            }

            foreach (Dictionary<string, object> chain in _recentActionChains)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string actionId = GetString(chain, "action_id");
                    string latestStage = GetString(chain, "latest_stage");
                    string summary = GetString(chain, "summary");
                    string failureStage = GetString(chain, "failure_stage");
                    bool hasClientVisible = GetBool(chain, "has_client_visible");

                    EditorGUILayout.LabelField(
                        DisplayOrFallback(summary),
                        EditorStyles.boldLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Action: {DisplayOrFallback(actionId)}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Latest Stage: {DisplayOrFallback(latestStage)}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Client Visible: {(hasClientVisible ? "yes" : "no")}",
                        EditorStyles.miniLabel
                    );
                    if (!string.IsNullOrWhiteSpace(failureStage))
                    {
                        EditorGUILayout.LabelField(
                            $"Failure Stage: {failureStage}",
                            EditorStyles.miniLabel
                        );
                    }

                    GUI.enabled = !string.IsNullOrWhiteSpace(actionId);
                    if (GUILayout.Button("Inspect Chain"))
                    {
                        LoadActionChain(actionId);
                    }

                    GUI.enabled = true;
                }
            }
        }

        private void DrawSelectedActionChain()
        {
            EditorGUILayout.LabelField("Selected Action Chain", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _selectedActionId = EditorGUILayout.TextField(
                    "Action Id",
                    _selectedActionId ?? string.Empty
                );

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = !string.IsNullOrWhiteSpace(_selectedActionId);
                    if (GUILayout.Button("Load By Action Id"))
                    {
                        LoadActionChain(_selectedActionId);
                    }

                    GUI.enabled = true;
                    if (GUILayout.Button("Load Latest"))
                    {
                        LoadActionChain(string.Empty);
                    }
                }

                if (_selectedActionChain == null)
                {
                    EditorGUILayout.HelpBox(
                        "Load an action chain to inspect validation, execution, and replication stages.",
                        MessageType.None
                    );
                    return;
                }

                bool found = GetBool(_selectedActionChain, "found", true);
                if (!found)
                {
                    EditorGUILayout.HelpBox(
                        GetString(_selectedActionChain, "error"),
                        MessageType.Warning
                    );
                    return;
                }

                EditorGUILayout.LabelField(
                    $"Resolved Action: {DisplayOrFallback(GetString(_selectedActionChain, "action_id"))}",
                    EditorStyles.boldLabel
                );
                EditorGUILayout.LabelField(
                    $"Latest Stage: {DisplayOrFallback(GetString(_selectedActionChain, "latest_stage"))}",
                    EditorStyles.miniLabel
                );
                EditorGUILayout.LabelField(
                    $"Client Visible: {(GetBool(_selectedActionChain, "has_client_visible") ? "yes" : "no")}",
                    EditorStyles.miniLabel
                );

                Dictionary<string, object> recommendedCheck = GetDictionary(
                    _selectedActionChain,
                    "recommended_check"
                );
                if (recommendedCheck != null)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Recommended Check", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        DisplayOrFallback(GetString(recommendedCheck, "summary")),
                        EditorStyles.wordWrappedMiniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Anchor: {DisplayOrFallback(GetString(recommendedCheck, "recommended_breakpoint_anchor_id"))}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Location: {DisplayOrFallback(GetString(recommendedCheck, "recommended_breakpoint_location"))}",
                        EditorStyles.wordWrappedMiniLabel
                    );

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string anchorId = GetString(
                            recommendedCheck,
                            "recommended_breakpoint_anchor_id"
                        );
                        string query = GetString(recommendedCheck, "recommended_mcp_query");

                        GUI.enabled = !string.IsNullOrWhiteSpace(anchorId);
                        if (GUILayout.Button("Open Recommended Anchor"))
                        {
                            OpenAnchor(anchorId);
                        }

                        GUI.enabled = !string.IsNullOrWhiteSpace(query);
                        if (GUILayout.Button("Copy Recommended Query"))
                        {
                            EditorGUIUtility.systemCopyBuffer = query;
                            _statusMessage = "Copied recommended MCP query.";
                        }

                        GUI.enabled = true;
                    }
                }

                EditorGUILayout.Space(4f);
                DrawObjectList(
                    "Validation Results",
                    GetList(_selectedActionChain, "validation_results")
                );
                DrawObjectList(
                    "Execution Traces",
                    GetList(_selectedActionChain, "execution_traces")
                );
                DrawObjectList(
                    "Replication Traces",
                    GetList(_selectedActionChain, "replication_traces")
                );
            }
        }

        private void DrawObjectList(string label, IList items)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (items == null || items.Count == 0)
            {
                EditorGUILayout.HelpBox("No entries recorded.", MessageType.None);
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    object item = items[index];
                    string text = FormatValue(item);
                    EditorGUILayout.SelectableLabel(
                        text,
                        EditorStyles.textArea,
                        GUILayout.MinHeight(Mathf.Max(48f, 18f * CountDisplayLines(text)))
                    );
                }
            }
        }

        private void DrawBrainVariableList(string label, IList items)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (items == null || items.Count == 0)
            {
                EditorGUILayout.HelpBox("No entries recorded.", MessageType.None);
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (items[index] is not Dictionary<string, object> variable)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string priority = GetString(variable, "priority");
                    string key = GetString(variable, "key");
                    string value = GetString(variable, "value");
                    string source = GetString(variable, "source");
                    string phase = GetString(variable, "phase");

                    EditorGUILayout.LabelField(
                        $"{DisplayOrFallback(priority)}  {DisplayOrFallback(key)}",
                        EditorStyles.boldLabel
                    );
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        EditorGUILayout.LabelField(value, EditorStyles.wordWrappedMiniLabel);
                    }

                    EditorGUILayout.LabelField(
                        $"Source: {DisplayOrFallback(source)}  Phase: {DisplayOrFallback(phase)}",
                        EditorStyles.miniLabel
                    );
                }
            }
        }

        private void RefreshData()
        {
            try
            {
                _recommendedChecks.Clear();
                _recommendedChecks.AddRange(DialogueMCPBridge.GetRecommendedActionChecks());

                _recentActionChains.Clear();
                _recentActionChains.AddRange(DialogueMCPBridge.GetRecentDiagnosticActionChains(8));

                _brainPacket = DialogueMCPBridge.GetDiagnosticBrainPacket();
                _brainPrompt = DialogueMCPBridge.GetDiagnosticBrainPrompt();
                _uiBehavior = DialogueMCPBridge.GetLatestUiBehaviorSnapshot();
                _uiPerformance = DialogueMCPBridge.GetLatestUiPerformanceSample();

                if (!string.IsNullOrWhiteSpace(_selectedActionId))
                {
                    _selectedActionChain = DialogueMCPBridge.GetDiagnosticActionChain(_selectedActionId);
                }

                _statusMessage =
                    $"Loaded {_recommendedChecks.Count} recommended checks and {_recentActionChains.Count} recent chains.";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Diagnostics refresh failed: {ex.Message}";
            }

            _lastRefreshAt = EditorApplication.timeSinceStartup;
            _lastRefreshStamp = DateTime.Now;
        }

        private void LoadActionChain(string actionId)
        {
            try
            {
                _selectedActionChain = DialogueMCPBridge.GetDiagnosticActionChain(actionId);
                _selectedActionId = GetString(_selectedActionChain, "action_id");
                _statusMessage = string.IsNullOrWhiteSpace(_selectedActionId)
                    ? "No diagnostic action id is available yet."
                    : $"Loaded action chain '{_selectedActionId}'.";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Action chain lookup failed: {ex.Message}";
            }
        }

        private void OpenAnchor(string anchorId)
        {
            try
            {
                Dictionary<string, object> result =
                    DialogueMCPBridge.OpenDiagnosticBreakpointAnchor(anchorId);
                bool ok = GetBool(result, "ok");
                _statusMessage = ok
                    ? $"Opened anchor '{GetString(result, "anchor_id")}' at line {GetString(result, "line")}."
                    : GetString(result, "error");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Opening anchor failed: {ex.Message}";
            }
        }

        private void ExportIncidentBundle()
        {
            try
            {
                Dictionary<string, object> result =
                    DialogueMCPBridge.ExportDiagnosticIncidentBundle("editor_window");
                bool ok = GetBool(result, "ok");
                _statusMessage = ok
                    ? $"Incident bundle exported: {GetString(result, "bundle_directory")}"
                    : GetString(result, "error");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Incident bundle export failed: {ex.Message}";
            }
        }

        private static string DisplayOrFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }

        private static int CountDisplayLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1;
            }

            int lineCount = 1;
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '\n')
                {
                    lineCount++;
                }
            }

            return lineCount;
        }

        private static Dictionary<string, object> GetDictionary(
            Dictionary<string, object> source,
            string key
        )
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (!source.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }

            return value as Dictionary<string, object>;
        }

        private static IList GetList(Dictionary<string, object> source, string key)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (!source.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }

            return value as IList;
        }

        private static bool GetBool(
            Dictionary<string, object> source,
            string key,
            bool defaultValue = false
        )
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            if (!source.TryGetValue(key, out object value) || value == null)
            {
                return defaultValue;
            }

            return value switch
            {
                bool boolValue => boolValue,
                string stringValue when bool.TryParse(stringValue, out bool parsed) => parsed,
                _ => defaultValue,
            };
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (!source.TryGetValue(key, out object value) || value == null)
            {
                return string.Empty;
            }

            return value.ToString();
        }

        private static string FormatValue(object value)
        {
            var builder = new StringBuilder(256);
            AppendValue(builder, value, 0);
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object value, int depth)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is Dictionary<string, object> dictionary)
            {
                AppendDictionary(builder, dictionary, depth);
                return;
            }

            if (value is IList list && value is not string)
            {
                AppendList(builder, list, depth);
                return;
            }

            builder.Append(value);
        }

        private static void AppendDictionary(
            StringBuilder builder,
            Dictionary<string, object> dictionary,
            int depth
        )
        {
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                builder.Append(' ', depth * 2);
                builder.Append(pair.Key);
                builder.Append(": ");
                AppendValue(builder, pair.Value, depth + 1);
                builder.AppendLine();
            }
        }

        private static void AppendList(StringBuilder builder, IList list, int depth)
        {
            for (int index = 0; index < list.Count; index++)
            {
                builder.Append(' ', depth * 2);
                builder.Append("- ");
                AppendValue(builder, list[index], depth + 1);
                builder.AppendLine();
            }
        }
    }
}
