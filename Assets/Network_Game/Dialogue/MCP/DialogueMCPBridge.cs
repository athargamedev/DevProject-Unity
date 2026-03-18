using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Network_Game.Auth;
using Network_Game.Diagnostics;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue.MCP
{
    /// <summary>
    /// Bridge class that exposes dialogue system data for MCP tools.
    /// This class is in Assembly-CSharp so it can be accessed by MCP.
    /// </summary>
    public class DialogueMCPBridge
    {
        private const int DefaultSceneElementCount = 10;
        private const float DefaultSceneProbeDistance = 120f;
        private const string DiagnosticIncidentBundlesRelativeDirectory = "output/diagnostic_incidents";
        private static int s_NextGameplayCopilotRequestId = 99400;

        private struct SceneElement
        {
            public string Name;
            public string Path;
            public Vector3 Position;
            public Vector3 Size;
            public float Distance;
            public string SemanticId;
            public string SemanticRole;
            public string[] SemanticAliases;
            public int SemanticPriority;
        }

        /// <summary>
        /// Get dialogue service stats as a dictionary
        /// </summary>
        public static Dictionary<string, object> GetStats()
        {
            var service = NetworkDialogueService.Instance;
            if (service == null)
                return null;

            int total = service.TotalCompleted + service.TotalFailed;
            float successRate = total > 0 ? (float)service.TotalCompleted / total : 0;

            return new Dictionary<string, object>
            {
                ["pending"] = service.PendingRequestCount,
                ["active"] = service.ActiveRequestCount,
                ["total_enqueued"] = service.TotalRequestsEnqueued,
                ["total_finished"] = service.TotalRequestsFinished,
                ["total_completed"] = service.TotalCompleted,
                ["total_failed"] = service.TotalFailed,
                ["timeout_count"] = service.TimeoutCount,
                ["success_rate"] = Math.Round(successRate * 100, 1),
                ["is_llm_ready"] = service.IsLLMReady,
                ["warmup_degraded"] = service.IsWarmupDegraded,
                ["warmup_failure_count"] = service.WarmupFailureCount,
                ["rejection_reasons"] = service.GetRejectionReasons(),
            };
        }

        /// <summary>
        /// Get LLM status
        /// </summary>
        public static Dictionary<string, object> GetLLMStatus()
        {
            var service = NetworkDialogueService.Instance;
            if (service == null)
                return null;

            return new Dictionary<string, object>
            {
                ["is_ready"] = service.IsLLMReady,
                ["backend"] = service.ActiveInferenceBackendName,
                ["has_backend_config"] = service.HasDialogueBackendConfig,
                ["remote_only"] = true,
                ["warmup_degraded"] = service.IsWarmupDegraded,
                ["warmup_failure_count"] = service.WarmupFailureCount,
                ["remote"] = service.UsesRemoteInference,
                ["remote_endpoint"] = service.RemoteInferenceEndpoint,
            };
        }

        /// <summary>
        /// Get the latest authority snapshot collected by the diagnostic brain runtime.
        /// </summary>
        public static Dictionary<string, object> GetAuthoritySnapshot()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            return diagnosticsBridge != null
                && diagnosticsBridge.TryGetAuthoritySnapshot(out AuthoritySnapshot snapshot)
                ? SerializeAuthoritySnapshot(snapshot)
                : null;
        }

        /// <summary>
        /// Get the latest curated scene projection used by Sprint 1 diagnostics.
        /// </summary>
        public static Dictionary<string, object> GetAuthoritativeSceneSnapshot(
            int maxObjects = 12,
            float maxDistance = 120f
        )
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null)
            {
                return null;
            }

            AuthoritativeSceneSnapshot snapshot =
                diagnosticsBridge.TryGetSceneSnapshot(out AuthoritativeSceneSnapshot latestSnapshot)
                    ? latestSnapshot
                    : diagnosticsBridge.BuildSceneSnapshot(maxObjects, maxDistance);
            return SerializeSceneSnapshot(snapshot);
        }

        /// <summary>
        /// Get the most recent inference envelope captured from the live dialogue pipeline.
        /// </summary>
        public static Dictionary<string, object> GetLatestInferenceEnvelope()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetLatestInferenceEnvelope(out DialogueInferenceEnvelope envelope))
            {
                return null;
            }

            return SerializeInferenceEnvelope(envelope);
        }

        /// <summary>
        /// Get the most recent dialogue execution trace spanning server dispatch and client-visible outcomes.
        /// </summary>
        public static Dictionary<string, object> GetLatestDialogueExecutionTrace()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace trace))
            {
                return null;
            }

            return SerializeDialogueExecutionTrace(trace);
        }

        /// <summary>
        /// Get the most recent dialogue action validation result recorded during effect/action validation.
        /// </summary>
        public static Dictionary<string, object> GetLatestDialogueActionValidationResult()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult result))
            {
                return null;
            }

            return SerializeDialogueActionValidationResult(result);
        }

        /// <summary>
        /// Get the most recent dialogue replication trace spanning RPC send/receive and client-visible application.
        /// </summary>
        public static Dictionary<string, object> GetLatestDialogueReplicationTrace()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace trace))
            {
                return null;
            }

            return SerializeDialogueReplicationTrace(trace);
        }

        /// <summary>
        /// Get the correlated validation, execution, and replication trace chain for a specific action id.
        /// If no action id is supplied, the latest trace-backed action id is used.
        /// </summary>
        public static Dictionary<string, object> GetDiagnosticActionChain(string actionId = "")
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null)
            {
                return null;
            }

            string resolvedActionId = ResolveDiagnosticActionId(diagnosticsBridge, actionId);
            if (string.IsNullOrWhiteSpace(resolvedActionId))
            {
                return new Dictionary<string, object>
                {
                    ["action_id"] = string.Empty,
                    ["found"] = false,
                    ["error"] = "No diagnostic action id is available yet.",
                    ["validation_results"] = new List<object>(),
                    ["execution_traces"] = new List<object>(),
                    ["replication_traces"] = new List<object>(),
                };
            }

            DialogueActionValidationResult[] validationMatches =
                diagnosticsBridge.GetRecentDialogueActionValidationResults()
                    .Where(result => string.Equals(result.ActionId, resolvedActionId, StringComparison.Ordinal))
                    .OrderBy(result => result.RealtimeSinceStartup)
                    .ThenBy(result => result.Frame)
                    .ToArray();
            DialogueExecutionTrace[] executionMatches =
                diagnosticsBridge.GetRecentDialogueExecutionTraces()
                    .Where(trace => string.Equals(trace.ActionId, resolvedActionId, StringComparison.Ordinal))
                    .OrderBy(trace => trace.RealtimeSinceStartup)
                    .ThenBy(trace => trace.Frame)
                    .ToArray();
            DialogueReplicationTrace[] replicationMatches =
                diagnosticsBridge.GetRecentDialogueReplicationTraces()
                    .Where(trace => string.Equals(trace.ActionId, resolvedActionId, StringComparison.Ordinal))
                    .OrderBy(trace => trace.RealtimeSinceStartup)
                    .ThenBy(trace => trace.Frame)
                    .ToArray();

            bool found =
                validationMatches.Length > 0
                || executionMatches.Length > 0
                || replicationMatches.Length > 0;
            DiagnosticActionChainSummary summary = DiagnosticActionChainSummarizer.BuildSummary(
                resolvedActionId,
                validationMatches,
                executionMatches,
                replicationMatches
            );

            DiagnosticActionRecommendation[] recommendations =
                DiagnosticActionRecommendationEngine.BuildRecommendations(
                    new[] { summary },
                    1
                );

            return new Dictionary<string, object>
            {
                ["action_id"] = resolvedActionId,
                ["found"] = found,
                ["latest_stage"] = summary.LatestStage,
                ["has_client_visible"] = summary.HasClientVisible,
                ["summary"] = SerializeDiagnosticActionChainSummary(summary),
                ["recommended_check"] = recommendations.Length > 0
                    ? SerializeDiagnosticActionRecommendation(recommendations[0])
                    : null,
                ["validation_results"] = validationMatches
                    .Select(SerializeDialogueActionValidationResult)
                    .Cast<object>()
                    .ToList(),
                ["execution_traces"] = executionMatches
                    .Select(SerializeDialogueExecutionTrace)
                    .Cast<object>()
                    .ToList(),
                ["replication_traces"] = replicationMatches
                    .Select(SerializeDialogueReplicationTrace)
                    .Cast<object>()
                    .ToList(),
            };
        }

        /// <summary>
        /// Get compact summaries for the most recent diagnostic action chains.
        /// </summary>
        public static List<Dictionary<string, object>> GetRecentDiagnosticActionChains(int limit = 5)
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null)
            {
                return new List<Dictionary<string, object>>();
            }

            int clampedLimit = Mathf.Clamp(limit, 1, 32);
            DiagnosticActionChainSummary[] summaries = DiagnosticActionChainSummarizer.BuildRecentSummaries(
                diagnosticsBridge.GetRecentDialogueActionValidationResults(),
                diagnosticsBridge.GetRecentDialogueExecutionTraces(),
                diagnosticsBridge.GetRecentDialogueReplicationTraces(),
                clampedLimit
            );
            return summaries.Select(SerializeDiagnosticActionChainSummary).ToList();
        }

        /// <summary>
        /// Get the current diagnostic brain packet used to prioritize blockers.
        /// </summary>
        public static Dictionary<string, object> GetDiagnosticBrainPacket()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetDiagnosticBrainPacket(out DiagnosticBrainPacket packet))
            {
                return null;
            }

            return SerializeBrainPacket(packet);
        }

        /// <summary>
        /// Get the most recent UI behavior snapshot published by dialogue UI surfaces.
        /// </summary>
        public static Dictionary<string, object> GetLatestUiBehaviorSnapshot(string uiId = "")
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetLatestUiBehaviorSnapshot(uiId, out UIBehaviorSnapshot snapshot))
            {
                return null;
            }

            return SerializeUiBehaviorSnapshot(snapshot);
        }

        /// <summary>
        /// Get the most recent UI performance sample published by dialogue UI surfaces.
        /// </summary>
        public static Dictionary<string, object> GetLatestUiPerformanceSample(string uiId = "")
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null || !diagnosticsBridge.TryGetLatestUiPerformanceSample(uiId, out UIPerformanceSample sample))
            {
                return null;
            }

            return SerializeUiPerformanceSample(sample);
        }

        /// <summary>
        /// Build the compact AI-facing prompt from the current diagnostic packet.
        /// </summary>
        public static string GetDiagnosticBrainPrompt()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            return diagnosticsBridge == null ? string.Empty : diagnosticsBridge.BuildDiagnosticBrainPrompt();
        }

        /// <summary>
        /// Get the current recommended next checks derived from recent dialogue action chains.
        /// </summary>
        public static List<Dictionary<string, object>> GetRecommendedActionChecks()
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (
                diagnosticsBridge == null
                || !diagnosticsBridge.TryGetDiagnosticBrainPacket(out DiagnosticBrainPacket packet)
                || packet.RecommendedActionChecks == null
            )
            {
                return new List<Dictionary<string, object>>();
            }

            return packet.RecommendedActionChecks
                .Select(SerializeDiagnosticActionRecommendation)
                .ToList();
        }

        /// <summary>
        /// Get the stable breakpoint anchor catalog used by diagnostics recommendations.
        /// </summary>
        public static List<Dictionary<string, object>> GetDiagnosticBreakpointAnchors()
        {
            return DiagnosticBreakpointAnchors.GetAll()
                .Select(anchor => new Dictionary<string, object>
                {
                    ["id"] = anchor.Id,
                    ["display_name"] = anchor.DisplayName,
                    ["location"] = anchor.Location,
                    ["summary"] = anchor.Summary,
                    ["relative_file_path"] = anchor.RelativeFilePath,
                    ["search_hint"] = anchor.SearchHint,
                })
                .ToList();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Open a diagnostic breakpoint anchor in the editor using its stable anchor id.
        /// </summary>
        public static Dictionary<string, object> OpenDiagnosticBreakpointAnchor(string anchorId)
        {
            if (!DiagnosticBreakpointAnchors.TryGet(anchorId, out DiagnosticBreakpointAnchor anchor))
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = $"Unknown diagnostic breakpoint anchor '{anchorId}'.",
                };
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relativePath = (anchor.RelativeFilePath ?? string.Empty)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = $"Anchor file was not found: {fullPath}",
                    ["anchor_id"] = anchor.Id,
                };
            }

            int line = ResolveBreakpointAnchorLine(fullPath, anchor.SearchHint);
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, line);

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["anchor_id"] = anchor.Id,
                ["display_name"] = anchor.DisplayName,
                ["full_path"] = fullPath,
                ["line"] = line,
                ["location"] = anchor.Location,
            };
        }
#else
        public static Dictionary<string, object> OpenDiagnosticBreakpointAnchor(string anchorId)
        {
            return new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = "Opening diagnostic breakpoint anchors is only available in the Unity Editor.",
                ["anchor_id"] = anchorId ?? string.Empty,
            };
        }
#endif

        private static string ResolveDiagnosticActionId(
            IDiagnosticsRuntimeBridge diagnosticsBridge,
            string preferredActionId
        )
        {
            if (!string.IsNullOrWhiteSpace(preferredActionId))
            {
                return preferredActionId.Trim();
            }

            if (
                diagnosticsBridge != null
                && diagnosticsBridge.TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace replicationTrace)
                && !string.IsNullOrWhiteSpace(replicationTrace.ActionId)
            )
            {
                return replicationTrace.ActionId;
            }

            if (
                diagnosticsBridge != null
                && diagnosticsBridge.TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace executionTrace)
                && !string.IsNullOrWhiteSpace(executionTrace.ActionId)
            )
            {
                return executionTrace.ActionId;
            }

            if (
                diagnosticsBridge != null
                && diagnosticsBridge.TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult actionValidation)
                && !string.IsNullOrWhiteSpace(actionValidation.ActionId)
            )
            {
                return actionValidation.ActionId;
            }

            return string.Empty;
        }

        private static int ResolveBreakpointAnchorLine(string fullPath, string searchHint)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                return 1;
            }

            if (string.IsNullOrWhiteSpace(searchHint))
            {
                return 1;
            }

            string[] lines = File.ReadAllLines(fullPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(searchHint, StringComparison.Ordinal) >= 0)
                {
                    return i + 1;
                }
            }

            return 1;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Export a self-contained diagnostic incident bundle for the current runtime state.
        /// </summary>
        public static Dictionary<string, object> ExportDiagnosticIncidentBundle(
            string label = null,
            int maxDebugEntries = 30,
            int maxLmStudioEntries = 10
        )
        {
            IDiagnosticsRuntimeBridge diagnosticsBridge = DiagnosticsRuntimeBridgeRegistry.Current;
            if (diagnosticsBridge == null)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = "Diagnostics runtime bridge is not available.",
                };
            }

            Dictionary<string, object> authority = diagnosticsBridge.TryGetAuthoritySnapshot(out AuthoritySnapshot authoritySnapshot)
                ? SerializeAuthoritySnapshot(authoritySnapshot)
                : null;
            Dictionary<string, object> sceneSnapshot = SerializeSceneSnapshot(
                diagnosticsBridge.TryGetSceneSnapshot(out AuthoritativeSceneSnapshot latestSceneSnapshot)
                    ? latestSceneSnapshot
                    : diagnosticsBridge.BuildSceneSnapshot(12, 120f)
            );
            Dictionary<string, object> inferenceEnvelope =
                diagnosticsBridge.TryGetLatestInferenceEnvelope(out DialogueInferenceEnvelope envelope)
                    ? SerializeInferenceEnvelope(envelope)
                    : null;
            Dictionary<string, object> executionTrace =
                diagnosticsBridge.TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace trace)
                    ? SerializeDialogueExecutionTrace(trace)
                    : null;
            Dictionary<string, object> actionValidation =
                diagnosticsBridge.TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult actionValidationResult)
                    ? SerializeDialogueActionValidationResult(actionValidationResult)
                    : null;
            Dictionary<string, object> replicationTrace =
                diagnosticsBridge.TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace latestReplicationTrace)
                    ? SerializeDialogueReplicationTrace(latestReplicationTrace)
                    : null;
            Dictionary<string, object> actionChain = GetDiagnosticActionChain(
                ResolveDiagnosticActionId(diagnosticsBridge, string.Empty)
            );
            List<Dictionary<string, object>> recentActionChains = GetRecentDiagnosticActionChains(5);
            List<Dictionary<string, object>> recommendedActionChecks = GetRecommendedActionChecks();
            List<Dictionary<string, object>> breakpointAnchors = GetDiagnosticBreakpointAnchors();
            Dictionary<string, object> brainPacket =
                diagnosticsBridge.TryGetDiagnosticBrainPacket(out DiagnosticBrainPacket packet)
                    ? SerializeBrainPacket(packet)
                    : null;
            Dictionary<string, object> uiBehavior =
                diagnosticsBridge.TryGetLatestUiBehaviorSnapshot(string.Empty, out UIBehaviorSnapshot uiBehaviorSnapshot)
                    ? SerializeUiBehaviorSnapshot(uiBehaviorSnapshot)
                    : null;
            Dictionary<string, object> uiPerformance =
                diagnosticsBridge.TryGetLatestUiPerformanceSample(string.Empty, out UIPerformanceSample uiPerformanceSample)
                    ? SerializeUiPerformanceSample(uiPerformanceSample)
                    : null;

            string conversationKey =
                trace.ConversationKey
                ?? envelope.ConversationKey
                ?? string.Empty;
            List<Dictionary<string, string>> conversationHistory =
                string.IsNullOrWhiteSpace(conversationKey) ? new List<Dictionary<string, string>>() : GetHistory(conversationKey);
            List<Dictionary<string, object>> debugLog = GetDebugLog(maxDebugEntries);
            List<Dictionary<string, object>> lmStudioLog = SerializeLmStudioLogEntries(
                GetLMStudioLog(maxLmStudioEntries)
            );
            string diagnosticPrompt = diagnosticsBridge.BuildDiagnosticBrainPrompt();

            string bundleDirectoryPath = ResolveDiagnosticIncidentBundleDirectoryPath(
                label,
                packet.SceneName,
                packet.CurrentPhase
            );
            Directory.CreateDirectory(bundleDirectoryPath);

            var snapshot = new Dictionary<string, object>
            {
                ["label"] = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim(),
                ["generated_at_utc"] = DateTime.UtcNow.ToString("o"),
                ["authority"] = authority,
                ["scene_snapshot"] = sceneSnapshot,
                ["latest_inference_envelope"] = inferenceEnvelope,
                ["latest_execution_trace"] = executionTrace,
                ["latest_action_validation"] = actionValidation,
                ["latest_replication_trace"] = replicationTrace,
                ["latest_action_chain"] = actionChain,
                ["recent_action_chains"] = recentActionChains,
                ["recommended_action_checks"] = recommendedActionChecks,
                ["breakpoint_anchors"] = breakpointAnchors,
                ["diagnostic_brain_packet"] = brainPacket,
                ["latest_ui_behavior"] = uiBehavior,
                ["latest_ui_performance"] = uiPerformance,
                ["conversation_key"] = conversationKey,
                ["conversation_history"] = conversationHistory,
                ["recent_debug_log"] = debugLog,
                ["recent_lmstudio_log"] = lmStudioLog,
            };

            string summaryPath = Path.Combine(bundleDirectoryPath, "summary.md");
            string snapshotPath = Path.Combine(bundleDirectoryPath, "snapshot.json");
            string timelinePath = Path.Combine(bundleDirectoryPath, "timeline.jsonl");
            string recentLogsPath = Path.Combine(bundleDirectoryPath, "recent_logs.txt");
            string promptPath = Path.Combine(bundleDirectoryPath, "brain_prompt.txt");

            File.WriteAllText(
                summaryPath,
                BuildDiagnosticIncidentSummaryMarkdown(
                    label,
                    packet,
                    authoritySnapshot,
                    envelope,
                    trace,
                    actionValidationResult,
                    latestReplicationTrace,
                    uiBehaviorSnapshot,
                    uiPerformanceSample,
                    conversationKey,
                    debugLog.Count,
                    lmStudioLog.Count
                )
            );
            File.WriteAllText(snapshotPath, SimpleJson(snapshot));
            File.WriteAllText(
                timelinePath,
                BuildDiagnosticIncidentTimelineJsonl(
                    envelope,
                    trace,
                    actionValidationResult,
                    latestReplicationTrace,
                    uiBehaviorSnapshot,
                    uiPerformanceSample,
                    debugLog,
                    lmStudioLog
                )
            );
            File.WriteAllText(
                recentLogsPath,
                BuildDiagnosticIncidentRecentLogsText(debugLog, lmStudioLog)
            );
            File.WriteAllText(promptPath, diagnosticPrompt ?? string.Empty);

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["bundle_directory"] = bundleDirectoryPath,
                ["summary_path"] = summaryPath,
                ["snapshot_path"] = snapshotPath,
                ["timeline_path"] = timelinePath,
                ["recent_logs_path"] = recentLogsPath,
                ["brain_prompt_path"] = promptPath,
                ["conversation_key"] = conversationKey,
            };
        }
#else
        public static Dictionary<string, object> ExportDiagnosticIncidentBundle(
            string label = null,
            int maxDebugEntries = 30,
            int maxLmStudioEntries = 10
        )
        {
            return new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = "Diagnostic incident bundle export is only available in the Unity Editor.",
            };
        }
#endif

        /// <summary>
        /// Get all NPC profiles
        /// </summary>
        public static List<Dictionary<string, object>> GetProfiles()
        {
            var profiles = NpcDialogueProfile.GetAllProfiles();
            return profiles
                .Select(p => new Dictionary<string, object>
                {
                    ["id"] = p.ProfileId,
                    ["display_name"] = p.DisplayName,
                    ["keywords"] = p.GetKeywords(),
                    ["power_count"] = p.PrefabPowers?.Length ?? 0,
                })
                .ToList();
        }

        /// <summary>
        /// Get a specific profile by ID
        /// </summary>
        public static Dictionary<string, object> GetProfile(string profileId)
        {
            var profile = NpcDialogueProfile.GetProfile(profileId);
            if (profile == null)
                return null;

            var result = new Dictionary<string, object>
            {
                ["id"] = profile.ProfileId,
                ["display_name"] = profile.DisplayName,
                ["system_prompt"] = profile.SystemPrompt,
                ["lore"] = profile.Lore,
                ["keywords"] = profile.GetKeywords(),
            };

            if (profile.PrefabPowers != null)
            {
                result["powers"] = profile
                    .PrefabPowers.Select(power => new Dictionary<string, object>
                    {
                        ["name"] = power.PowerName,
                        ["enabled"] = power.Enabled,
                        ["keywords"] = power.Keywords,
                        ["duration"] = power.DurationSeconds,
                        ["scale"] = power.Scale,
                        ["element"] = power.Element,
                    })
                    .ToList();
            }

            result["effect_settings"] = new Dictionary<string, object>
            {
                ["bored_enabled"] = profile.EnableBoredLightEffect,
                ["bored_keywords"] = profile.BoredKeywords,
                ["dynamic_params_enabled"] = profile.EnableDynamicEffectParameters,
                ["min_multiplier"] = profile.DynamicEffectMinMultiplier,
                ["max_multiplier"] = profile.DynamicEffectMaxMultiplier,
            };

            return result;
        }

        /// <summary>
        /// Get available effect types
        /// </summary>
        public static string[] GetEffectTypes()
        {
            return DialogueSceneEffectsController.GetAvailableEffects();
        }

        /// <summary>
        /// Get history for a conversation
        /// </summary>
        public static List<Dictionary<string, string>> GetHistory(string conversationKey)
        {
            var service = NetworkDialogueService.Instance;
            if (service == null)
                return null;

            var history = service.GetHistoryPublic(conversationKey);
            if (history == null)
                return new List<Dictionary<string, string>>();

            return history
                .Select(m => new Dictionary<string, string>
                {
                    ["role"] = m.Role ?? string.Empty,
                    ["content"] = m.Content ?? string.Empty,
                })
                .ToList();
        }

        /// <summary>
        /// Get all conversation keys
        /// </summary>
        public static string[] GetConversationKeys()
        {
            var service = NetworkDialogueService.Instance;
            if (service == null)
                return Array.Empty<string>();

            return service.GetConversationKeys();
        }

        /// <summary>
        /// Get queue status
        /// </summary>
        public static Dictionary<string, int> GetQueueStatus()
        {
            var service = NetworkDialogueService.Instance;
            if (service == null)
                return null;

            return new Dictionary<string, int>
            {
                ["pending"] = service.PendingRequestCount,
                ["active"] = service.ActiveRequestCount,
                ["total_enqueued"] = service.TotalRequestsEnqueued,
                ["total_finished"] = service.TotalRequestsFinished,
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Batch dialogue testing
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Start an automated batch dialogue test run.
        /// Prompts are sent sequentially via <see cref="NetworkDialogueService.RequestDialogue"/>;
        /// results (speech, parsed actions, latency) are written to a JSONL file when finished.
        /// </summary>
        /// <param name="prompts">Array of prompt strings to send to the NPC.</param>
        /// <param name="speakerNetworkObjectId">NetworkObjectId of the NPC that should respond.</param>
        /// <param name="label">Optional label used in the output filename.</param>
        /// <param name="listenerNetworkObjectId">Listener player NetworkObjectId (0 = local player).</param>
        public static Dictionary<string, object> RunBatchTest(
            string[] prompts,
            ulong speakerNetworkObjectId,
            string label = null,
            ulong listenerNetworkObjectId = 0)
        {
            if (!Application.isPlaying)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "Enter Play Mode first." };

            if (prompts == null || prompts.Length == 0)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "No prompts provided." };

            if (!TryResolveGameplayProbeContext(
                    out NetworkDialogueService service,
                    out _,
                    out ulong resolvedListenerNetworkId,
                    out _,
                    out string error,
                    listenerNetworkObjectId))
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = error };

            NpcDialogueActor[] actors = FindNpcActors();
            NpcDialogueActor targetActor = null;
            for (int i = 0; actors != null && i < actors.Length; i++)
            {
                if (actors[i] != null && actors[i].NetworkObjectId == speakerNetworkObjectId)
                {
                    targetActor = actors[i];
                    break;
                }
            }

            if (targetActor == null)
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = $"NPC with NetworkObjectId={speakerNetworkObjectId} not found.",
                };

            var cases = new List<DialogueBatchTester.BatchTestCase>(prompts.Length);
            for (int i = 0; i < prompts.Length; i++)
                cases.Add(new DialogueBatchTester.BatchTestCase { Prompt = prompts[i], Description = $"prompt_{i}" });

            DialogueBatchTester tester = DialogueBatchTester.GetOrCreate();
            tester.StartBatch(cases, speakerNetworkObjectId, resolvedListenerNetworkId, label);

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["prompt_count"] = prompts.Length,
                ["speaker_network_id"] = speakerNetworkObjectId,
                ["npc_name"] = GetNpcDisplayName(targetActor),
                ["label"] = label ?? string.Empty,
                ["note"] = "Call GetBatchResults() to poll progress and retrieve the output path.",
            };
        }

        /// <summary>
        /// Poll for batch test progress and results.
        /// Returns status, completed count, and the output file path once finished.
        /// </summary>
        public static Dictionary<string, object> GetBatchResults()
        {
            if (!Application.isPlaying)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "Not in Play Mode." };

            DialogueBatchTester tester = DialogueBatchTester.GetOrCreate();

            var result = new Dictionary<string, object>
            {
                ["is_running"] = tester.IsRunning,
                ["is_complete"] = tester.IsComplete,
                ["total"] = tester.Results.Count,
                ["finished"] = 0,
                ["output_path"] = tester.LastOutputPath ?? string.Empty,
            };

            int finished = 0;
            for (int i = 0; i < tester.Results.Count; i++)
                if (tester.Results[i].IsFinished || tester.Results[i].Status != null)
                    finished++;

            result["finished"] = finished;

            if (tester.IsComplete)
            {
                var summary = new List<Dictionary<string, object>>(tester.Results.Count);
                foreach (DialogueBatchTester.BatchTestResult r in tester.Results)
                {
                    summary.Add(new Dictionary<string, object>
                    {
                        ["index"] = r.Index,
                        ["prompt"] = r.Prompt,
                        ["status"] = r.Status ?? "pending",
                        ["speech"] = r.Speech ?? string.Empty,
                        ["action_count"] = r.ActionCount,
                        ["model_ms"] = Math.Round(r.ModelLatencyMs, 1),
                        ["error"] = r.Error ?? string.Empty,
                    });
                }
                result["results"] = summary;
            }

            return result;
        }

        private static Dictionary<string, object> SerializeAuthoritySnapshot(AuthoritySnapshot snapshot)
        {
            return new Dictionary<string, object>
            {
                ["run_id"] = snapshot.RunId,
                ["boot_id"] = snapshot.BootId,
                ["scene_name"] = snapshot.SceneName,
                ["frame"] = snapshot.Frame,
                ["realtime_since_startup"] = snapshot.RealtimeSinceStartup,
                ["network_manager_present"] = snapshot.NetworkManagerPresent,
                ["is_listening"] = snapshot.IsListening,
                ["is_server"] = snapshot.IsServer,
                ["is_host"] = snapshot.IsHost,
                ["is_client"] = snapshot.IsClient,
                ["is_connected_client"] = snapshot.IsConnectedClient,
                ["local_client_id"] = snapshot.LocalClientId,
                ["local_player_resolved"] = snapshot.LocalPlayerResolved,
                ["local_player_network_object_id"] = snapshot.LocalPlayerNetworkObjectId,
                ["local_player_owner_client_id"] = snapshot.LocalPlayerOwnerClientId,
                ["local_player_object_name"] = snapshot.LocalPlayerObjectName,
                ["local_player_is_spawned"] = snapshot.LocalPlayerIsSpawned,
                ["local_player_is_owner"] = snapshot.LocalPlayerIsOwner,
                ["local_controller_present"] = snapshot.LocalControllerPresent,
                ["local_controller_enabled"] = snapshot.LocalControllerEnabled,
                ["local_input_component_present"] = snapshot.LocalInputComponentPresent,
                ["local_input_enabled"] = snapshot.LocalInputEnabled,
                ["local_action_map"] = snapshot.LocalActionMap,
                ["camera_follow_assigned"] = snapshot.CameraFollowAssigned,
                ["auth_service_present"] = snapshot.AuthServicePresent,
                ["has_authenticated_player"] = snapshot.HasAuthenticatedPlayer,
                ["auth_name_id"] = snapshot.AuthNameId,
                ["auth_attached_network_object_id"] = snapshot.AuthAttachedNetworkObjectId,
                ["prompt_context_initialized"] = snapshot.PromptContextInitialized,
                ["prompt_context_applied_to_dialogue"] = snapshot.PromptContextAppliedToDialogue,
                ["current_phase"] = snapshot.CurrentPhase,
                ["summary"] = snapshot.Summary,
                ["primary_problem"] = snapshot.ResolvePrimaryAuthorityProblem(),
            };
        }

        private static Dictionary<string, object> SerializeSceneSnapshot(
            AuthoritativeSceneSnapshot snapshot
        )
        {
            return new Dictionary<string, object>
            {
                ["snapshot_id"] = snapshot.SnapshotId,
                ["snapshot_hash"] = snapshot.SnapshotHash,
                ["scene_name"] = snapshot.SceneName,
                ["frame"] = snapshot.Frame,
                ["realtime_since_startup"] = snapshot.RealtimeSinceStartup,
                ["probe_listener_network_object_id"] = snapshot.ProbeListenerNetworkObjectId,
                ["probe_listener_name"] = snapshot.ProbeListenerName,
                ["probe_origin"] = SerializeVector3(snapshot.ProbeOrigin),
                ["max_distance"] = snapshot.MaxDistance,
                ["semantic_summary"] = snapshot.SemanticSummary,
                ["objects"] = snapshot.Objects != null
                    ? snapshot.Objects.Select(SerializeSceneObject).Cast<object>().ToList()
                    : new List<object>(),
            };
        }

        private static Dictionary<string, object> SerializeSceneObject(SceneObjectDescriptor descriptor)
        {
            return new Dictionary<string, object>
            {
                ["object_id"] = descriptor.ObjectId,
                ["semantic_id"] = descriptor.SemanticId,
                ["display_name"] = descriptor.DisplayName,
                ["role"] = descriptor.Role,
                ["aliases"] = descriptor.Aliases != null
                    ? descriptor.Aliases.Cast<object>().ToList()
                    : new List<object>(),
                ["is_network_object"] = descriptor.IsNetworkObject,
                ["network_object_id"] = descriptor.NetworkObjectId,
                ["owner_client_id"] = descriptor.OwnerClientId,
                ["is_spawned"] = descriptor.IsSpawned,
                ["position"] = SerializeVector3(descriptor.Position),
                ["euler_angles"] = SerializeVector3(descriptor.EulerAngles),
                ["bounds_size"] = SerializeVector3(descriptor.BoundsSize),
                ["distance_from_probe"] = descriptor.DistanceFromProbe,
                ["renderer_summary"] = descriptor.RendererSummary,
                ["material_summary"] = descriptor.MaterialSummary,
                ["mesh_summary"] = descriptor.MeshSummary,
                ["animation_summary"] = descriptor.AnimationSummary,
                ["gameplay_summary"] = descriptor.GameplaySummary,
                ["mutable_surfaces"] = descriptor.MutableSurfaces != null
                    ? descriptor.MutableSurfaces.Select(SerializeMutableSurface).Cast<object>().ToList()
                    : new List<object>(),
            };
        }

        private static Dictionary<string, object> SerializeMutableSurface(
            MutableSurfaceDescriptor descriptor
        )
        {
            return new Dictionary<string, object>
            {
                ["kind"] = descriptor.Kind.ToString(),
                ["surface_id"] = descriptor.SurfaceId,
                ["display_name"] = descriptor.DisplayName,
                ["allowed_properties"] = descriptor.AllowedProperties != null
                    ? descriptor.AllowedProperties.Cast<object>().ToList()
                    : new List<object>(),
                ["allowed_operations"] = descriptor.AllowedOperations != null
                    ? descriptor.AllowedOperations.Cast<object>().ToList()
                    : new List<object>(),
                ["required_authority"] = descriptor.RequiredAuthority,
                ["replicated"] = descriptor.Replicated,
            };
        }

        private static Dictionary<string, object> SerializeInferenceEnvelope(
            DialogueInferenceEnvelope envelope
        )
        {
            return new Dictionary<string, object>
            {
                ["envelope_id"] = envelope.EnvelopeId,
                ["flow_id"] = envelope.FlowId,
                ["request_id"] = envelope.RequestId,
                ["client_request_id"] = envelope.ClientRequestId,
                ["requesting_client_id"] = envelope.RequestingClientId,
                ["speaker_network_id"] = envelope.SpeakerNetworkId,
                ["listener_network_id"] = envelope.ListenerNetworkId,
                ["conversation_key"] = envelope.ConversationKey,
                ["backend_name"] = envelope.BackendName,
                ["endpoint_label"] = envelope.EndpointLabel,
                ["model_name"] = envelope.ModelName,
                ["system_prompt_id"] = envelope.SystemPromptId,
                ["prompt_template_id"] = envelope.PromptTemplateId,
                ["prompt_template_version"] = envelope.PromptTemplateVersion,
                ["scene_snapshot_id"] = envelope.SceneSnapshotId,
                ["scene_snapshot_hash"] = envelope.SceneSnapshotHash,
                ["temperature"] = envelope.Temperature,
                ["top_p"] = envelope.TopP,
                ["top_k"] = envelope.TopK,
                ["frequency_penalty"] = envelope.FrequencyPenalty,
                ["presence_penalty"] = envelope.PresencePenalty,
                ["repeat_penalty"] = envelope.RepeatPenalty,
                ["max_tokens"] = envelope.MaxTokens,
                ["stop_sequences"] = envelope.StopSequences != null
                    ? envelope.StopSequences.Cast<object>().ToList()
                    : new List<object>(),
                ["prompt_char_count"] = envelope.PromptCharCount,
                ["scene_snapshot_char_count"] = envelope.SceneSnapshotCharCount,
                ["prompt_token_estimate"] = envelope.PromptTokenEstimate,
                ["enqueued_at"] = envelope.EnqueuedAt,
                ["started_at"] = envelope.StartedAt,
                ["retry_count"] = envelope.RetryCount,
                ["prompt_preview"] = envelope.PromptPreview,
            };
        }

        private static Dictionary<string, object> SerializeBrainPacket(DiagnosticBrainPacket packet)
        {
            return new Dictionary<string, object>
            {
                ["run_id"] = packet.RunId,
                ["boot_id"] = packet.BootId,
                ["objective"] = packet.Objective,
                ["scene_name"] = packet.SceneName,
                ["current_phase"] = packet.CurrentPhase,
                ["summary"] = packet.Summary,
                ["authority"] = SerializeAuthoritySnapshot(packet.Authority),
                ["scene_snapshot"] = SerializeSceneSnapshot(packet.SceneSnapshot),
                ["latest_envelope"] = string.IsNullOrWhiteSpace(packet.LatestEnvelope.EnvelopeId)
                    ? null
                    : SerializeInferenceEnvelope(packet.LatestEnvelope),
                ["latest_execution_trace"] = string.IsNullOrWhiteSpace(packet.LatestExecutionTrace.TraceId)
                    ? null
                    : SerializeDialogueExecutionTrace(packet.LatestExecutionTrace),
                ["latest_action_validation"] = string.IsNullOrWhiteSpace(packet.LatestActionValidation.ResultId)
                    ? null
                    : SerializeDialogueActionValidationResult(packet.LatestActionValidation),
                ["latest_replication_trace"] = string.IsNullOrWhiteSpace(packet.LatestReplicationTrace.TraceId)
                    ? null
                    : SerializeDialogueReplicationTrace(packet.LatestReplicationTrace),
                ["recent_action_chains"] = packet.RecentActionChains != null
                    ? packet.RecentActionChains.Select(SerializeDiagnosticActionChainSummary).Cast<object>().ToList()
                    : new List<object>(),
                ["recommended_action_checks"] = packet.RecommendedActionChecks != null
                    ? packet.RecommendedActionChecks.Select(SerializeDiagnosticActionRecommendation).Cast<object>().ToList()
                    : new List<object>(),
                ["top_priorities"] = packet.TopPriorities != null
                    ? packet.TopPriorities.Select(SerializeBrainVariable).Cast<object>().ToList()
                    : new List<object>(),
                ["active_facts"] = packet.ActiveFacts != null
                    ? packet.ActiveFacts.Select(SerializeBrainVariable).Cast<object>().ToList()
                    : new List<object>(),
                ["active_suppressions"] = packet.ActiveSuppressions != null
                    ? packet.ActiveSuppressions.Select(SerializeBrainVariable).Cast<object>().ToList()
                    : new List<object>(),
            };
        }

        private static Dictionary<string, object> SerializeDialogueExecutionTrace(
            DialogueExecutionTrace trace
        )
        {
            return new Dictionary<string, object>
            {
                ["trace_id"] = trace.TraceId,
                ["action_id"] = trace.ActionId,
                ["run_id"] = trace.RunId,
                ["boot_id"] = trace.BootId,
                ["flow_id"] = trace.FlowId,
                ["request_id"] = trace.RequestId,
                ["client_request_id"] = trace.ClientRequestId,
                ["requesting_client_id"] = trace.RequestingClientId,
                ["speaker_network_id"] = trace.SpeakerNetworkId,
                ["listener_network_id"] = trace.ListenerNetworkId,
                ["conversation_key"] = trace.ConversationKey,
                ["stage"] = trace.Stage,
                ["stage_detail"] = trace.StageDetail,
                ["success"] = trace.Success,
                ["source"] = trace.Source,
                ["effect_type"] = trace.EffectType,
                ["effect_name"] = trace.EffectName,
                ["source_network_object_id"] = trace.SourceNetworkObjectId,
                ["target_network_object_id"] = trace.TargetNetworkObjectId,
                ["response_preview"] = trace.ResponsePreview,
                ["error"] = trace.Error,
                ["frame"] = trace.Frame,
                ["realtime_since_startup"] = trace.RealtimeSinceStartup,
                ["summary"] = trace.Summary,
            };
        }

        private static Dictionary<string, object> SerializeDialogueActionValidationResult(
            DialogueActionValidationResult result
        )
        {
            return new Dictionary<string, object>
            {
                ["result_id"] = result.ResultId,
                ["action_id"] = result.ActionId,
                ["run_id"] = result.RunId,
                ["boot_id"] = result.BootId,
                ["flow_id"] = result.FlowId,
                ["request_id"] = result.RequestId,
                ["client_request_id"] = result.ClientRequestId,
                ["requesting_client_id"] = result.RequestingClientId,
                ["speaker_network_id"] = result.SpeakerNetworkId,
                ["listener_network_id"] = result.ListenerNetworkId,
                ["conversation_key"] = result.ConversationKey,
                ["action_kind"] = result.ActionKind,
                ["action_name"] = result.ActionName,
                ["decision"] = result.Decision,
                ["success"] = result.Success,
                ["source"] = result.Source,
                ["reason"] = result.Reason,
                ["requested_target_hint"] = result.RequestedTargetHint,
                ["resolved_target_network_object_id"] = result.ResolvedTargetNetworkObjectId,
                ["requested_placement_hint"] = result.RequestedPlacementHint,
                ["resolved_spatial_type"] = result.ResolvedSpatialType,
                ["spatial_reason"] = result.SpatialReason,
                ["requested_scale"] = result.RequestedScale,
                ["applied_scale"] = result.AppliedScale,
                ["requested_duration"] = result.RequestedDuration,
                ["applied_duration"] = result.AppliedDuration,
                ["requested_damage_radius"] = result.RequestedDamageRadius,
                ["applied_damage_radius"] = result.AppliedDamageRadius,
                ["requested_damage_amount"] = result.RequestedDamageAmount,
                ["applied_damage_amount"] = result.AppliedDamageAmount,
                ["frame"] = result.Frame,
                ["realtime_since_startup"] = result.RealtimeSinceStartup,
                ["summary"] = result.Summary,
            };
        }

        private static Dictionary<string, object> SerializeDialogueReplicationTrace(
            DialogueReplicationTrace trace
        )
        {
            return new Dictionary<string, object>
            {
                ["trace_id"] = trace.TraceId,
                ["action_id"] = trace.ActionId,
                ["run_id"] = trace.RunId,
                ["boot_id"] = trace.BootId,
                ["flow_id"] = trace.FlowId,
                ["request_id"] = trace.RequestId,
                ["client_request_id"] = trace.ClientRequestId,
                ["requesting_client_id"] = trace.RequestingClientId,
                ["speaker_network_id"] = trace.SpeakerNetworkId,
                ["listener_network_id"] = trace.ListenerNetworkId,
                ["conversation_key"] = trace.ConversationKey,
                ["stage"] = trace.Stage,
                ["network_path"] = trace.NetworkPath,
                ["success"] = trace.Success,
                ["source"] = trace.Source,
                ["effect_type"] = trace.EffectType,
                ["effect_name"] = trace.EffectName,
                ["source_network_object_id"] = trace.SourceNetworkObjectId,
                ["target_network_object_id"] = trace.TargetNetworkObjectId,
                ["detail"] = trace.Detail,
                ["error"] = trace.Error,
                ["frame"] = trace.Frame,
                ["realtime_since_startup"] = trace.RealtimeSinceStartup,
                ["summary"] = trace.Summary,
            };
        }

        private static Dictionary<string, object> SerializeDiagnosticActionChainSummary(
            DiagnosticActionChainSummary summary
        )
        {
            return new Dictionary<string, object>
            {
                ["action_id"] = summary.ActionId,
                ["latest_stage"] = summary.LatestStage,
                ["has_client_visible"] = summary.HasClientVisible,
                ["has_failure"] = summary.HasFailure,
                ["request_id"] = summary.RequestId,
                ["client_request_id"] = summary.ClientRequestId,
                ["validation_count"] = summary.ValidationCount,
                ["execution_count"] = summary.ExecutionCount,
                ["replication_count"] = summary.ReplicationCount,
                ["latest_validation_decision"] = summary.LatestValidationDecision,
                ["latest_execution_stage"] = summary.LatestExecutionStage,
                ["latest_replication_stage"] = summary.LatestReplicationStage,
                ["latest_validation_summary"] = summary.LatestValidationSummary,
                ["latest_execution_summary"] = summary.LatestExecutionSummary,
                ["latest_replication_summary"] = summary.LatestReplicationSummary,
            };
        }

        private static Dictionary<string, object> SerializeDiagnosticActionRecommendation(
            DiagnosticActionRecommendation recommendation
        )
        {
            return new Dictionary<string, object>
            {
                ["action_id"] = recommendation.ActionId,
                ["stage"] = recommendation.Stage,
                ["priority"] = recommendation.Priority,
                ["summary"] = recommendation.Summary,
                ["recommended_breakpoint_anchor_id"] = recommendation.RecommendedBreakpointAnchorId,
                ["recommended_breakpoint_location"] = recommendation.RecommendedBreakpointLocation,
                ["recommended_mcp_query"] = recommendation.RecommendedMcpQuery,
            };
        }

        private static List<Dictionary<string, object>> SerializeLmStudioLogEntries(
            List<LMStudioLogEntry> entries
        )
        {
            var result = new List<Dictionary<string, object>>();
            if (entries == null)
            {
                return result;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                LMStudioLogEntry entry = entries[i];
                result.Add(
                    new Dictionary<string, object>
                    {
                        ["timestamp_ms"] = entry.TimestampMs,
                        ["mode"] = entry.Mode ?? string.Empty,
                        ["summary"] = entry.Summary ?? string.Empty,
                        ["detail"] = entry.Detail ?? string.Empty,
                    }
                );
            }

            return result;
        }

        /// <summary>
        /// Build the bundle folder path under the project output directory.
        /// </summary>
        private static string ResolveDiagnosticIncidentBundleDirectoryPath(
            string label,
            string sceneName,
            string phase
        )
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string root = Path.Combine(projectRoot, DiagnosticIncidentBundlesRelativeDirectory);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string suffix = SanitizePathSegment(
                string.Join(
                    "_",
                    new[]
                    {
                        string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                        string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim(),
                        string.IsNullOrWhiteSpace(phase) ? null : phase.Trim(),
                    }.Where(part => !string.IsNullOrWhiteSpace(part))
                )
            );
            if (string.IsNullOrWhiteSpace(suffix))
            {
                suffix = "incident";
            }

            string candidate = Path.Combine(root, $"{timestamp}_{suffix}");
            int suffixIndex = 1;
            while (Directory.Exists(candidate))
            {
                candidate = Path.Combine(root, $"{timestamp}_{suffix}_{suffixIndex}");
                suffixIndex++;
            }

            return candidate;
        }

        private static string BuildDiagnosticIncidentSummaryMarkdown(
            string label,
            DiagnosticBrainPacket packet,
            AuthoritySnapshot authority,
            DialogueInferenceEnvelope envelope,
            DialogueExecutionTrace trace,
            DialogueActionValidationResult actionValidation,
            DialogueReplicationTrace replicationTrace,
            UIBehaviorSnapshot uiBehavior,
            UIPerformanceSample uiPerformance,
            string conversationKey,
            int debugLogCount,
            int lmStudioLogCount
        )
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("# Diagnostic Incident Bundle");
            builder.AppendLine();
            builder.AppendLine($"- Generated UTC: {DateTime.UtcNow:o}");
            builder.AppendLine($"- Label: {Coalesce(label, "incident")}");
            builder.AppendLine($"- Scene: {Coalesce(packet.SceneName, authority.SceneName)}");
            builder.AppendLine($"- Phase: {Coalesce(packet.CurrentPhase, authority.CurrentPhase)}");
            builder.AppendLine($"- Run ID: {Coalesce(packet.RunId, authority.RunId)}");
            builder.AppendLine($"- Boot ID: {Coalesce(packet.BootId, authority.BootId)}");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine(Coalesce(packet.Summary, "No diagnostic summary available."));
            builder.AppendLine();
            builder.AppendLine("## Authority");
            builder.AppendLine($"- {Coalesce(authority.Summary, "No authority snapshot.")}");
            builder.AppendLine();
            builder.AppendLine("## Latest Inference Envelope");
            if (string.IsNullOrWhiteSpace(envelope.EnvelopeId))
            {
                builder.AppendLine("- none");
            }
            else
            {
                builder.AppendLine(
                    $"- request={envelope.RequestId} model={Coalesce(envelope.ModelName, "unknown")} backend={Coalesce(envelope.BackendName, "unknown")} maxTokens={envelope.MaxTokens} estTokens={envelope.PromptTokenEstimate}"
                );
            }

            builder.AppendLine();
            builder.AppendLine("## Latest Execution Trace");
            builder.AppendLine(
                string.IsNullOrWhiteSpace(trace.TraceId)
                    ? "- none"
                    : $"- {Coalesce(trace.Summary, "execution trace available")}"
            );

            builder.AppendLine();
            builder.AppendLine("## Latest Action Validation");
            builder.AppendLine(
                string.IsNullOrWhiteSpace(actionValidation.ResultId)
                    ? "- none"
                    : $"- {Coalesce(actionValidation.Summary, "action validation available")}"
            );

            builder.AppendLine();
            builder.AppendLine("## Latest Replication Trace");
            builder.AppendLine(
                string.IsNullOrWhiteSpace(replicationTrace.TraceId)
                    ? "- none"
                    : $"- {Coalesce(replicationTrace.Summary, "replication trace available")}"
            );

            builder.AppendLine();
            builder.AppendLine("## UI");
            builder.AppendLine(
                string.IsNullOrWhiteSpace(uiBehavior.UiId)
                    ? "- No UI behavior snapshot."
                    : $"- {Coalesce(uiBehavior.Summary, "ui behavior snapshot available")}"
            );
            if (!string.IsNullOrWhiteSpace(uiPerformance.UiId))
            {
                builder.AppendLine(
                    $"- Perf: {Coalesce(uiPerformance.UiKind, "ui")} {Coalesce(uiPerformance.SampleName, "sample")} {uiPerformance.DurationMs:0.00} ms"
                );
            }

            builder.AppendLine();
            builder.AppendLine("## Context");
            builder.AppendLine($"- Conversation key: {Coalesce(conversationKey, "none")}");
            builder.AppendLine($"- Debug log entries: {debugLogCount}");
            builder.AppendLine($"- LM Studio entries: {lmStudioLogCount}");

            if (packet.TopPriorities != null && packet.TopPriorities.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Top Priorities");
                for (int i = 0; i < packet.TopPriorities.Length; i++)
                {
                    DiagnosticBrainVariable variable = packet.TopPriorities[i];
                    builder.AppendLine(
                        $"- {variable.Severity} {Coalesce(variable.Key, "priority")}: {Coalesce(variable.Value, string.Empty)}"
                    );
                }
            }

            if (packet.RecommendedActionChecks != null && packet.RecommendedActionChecks.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Recommended Checks");
                for (int i = 0; i < packet.RecommendedActionChecks.Length; i++)
                {
                    DiagnosticActionRecommendation recommendation = packet.RecommendedActionChecks[i];
                    builder.Append("- ")
                        .Append(Coalesce(recommendation.Priority, "P2"))
                        .Append(' ')
                        .Append(Coalesce(recommendation.ActionId, "action"))
                        .Append(": ")
                        .AppendLine(Coalesce(recommendation.Summary, string.Empty));

                    if (!string.IsNullOrWhiteSpace(recommendation.RecommendedBreakpointAnchorId))
                    {
                        builder.AppendLine(
                            $"  anchor={recommendation.RecommendedBreakpointAnchorId} location={Coalesce(recommendation.RecommendedBreakpointLocation, string.Empty)}"
                        );
                    }

                    if (!string.IsNullOrWhiteSpace(recommendation.RecommendedMcpQuery))
                    {
                        builder.AppendLine($"  mcp={recommendation.RecommendedMcpQuery}");
                    }
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Files");
            builder.AppendLine("- `summary.md`");
            builder.AppendLine("- `snapshot.json`");
            builder.AppendLine("- `timeline.jsonl`");
            builder.AppendLine("- `recent_logs.txt`");
            builder.AppendLine("- `brain_prompt.txt`");
            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        private static string BuildDiagnosticIncidentTimelineJsonl(
            DialogueInferenceEnvelope envelope,
            DialogueExecutionTrace trace,
            DialogueActionValidationResult actionValidation,
            DialogueReplicationTrace replicationTrace,
            UIBehaviorSnapshot uiBehavior,
            UIPerformanceSample uiPerformance,
            List<Dictionary<string, object>> debugLog,
            List<Dictionary<string, object>> lmStudioLog
        )
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(envelope.EnvelopeId))
            {
                var record = SerializeInferenceEnvelope(envelope);
                record["record_type"] = "inference_envelope";
                lines.Add(SimpleJson(record));
            }

            if (!string.IsNullOrWhiteSpace(trace.TraceId))
            {
                var record = SerializeDialogueExecutionTrace(trace);
                record["record_type"] = "execution_trace";
                lines.Add(SimpleJson(record));
            }

            if (!string.IsNullOrWhiteSpace(actionValidation.ResultId))
            {
                var record = SerializeDialogueActionValidationResult(actionValidation);
                record["record_type"] = "action_validation";
                lines.Add(SimpleJson(record));
            }

            if (!string.IsNullOrWhiteSpace(replicationTrace.TraceId))
            {
                var record = SerializeDialogueReplicationTrace(replicationTrace);
                record["record_type"] = "replication_trace";
                lines.Add(SimpleJson(record));
            }

            if (!string.IsNullOrWhiteSpace(uiBehavior.UiId))
            {
                var record = SerializeUiBehaviorSnapshot(uiBehavior);
                record["record_type"] = "ui_behavior";
                lines.Add(SimpleJson(record));
            }

            if (!string.IsNullOrWhiteSpace(uiPerformance.UiId))
            {
                var record = SerializeUiPerformanceSample(uiPerformance);
                record["record_type"] = "ui_performance";
                lines.Add(SimpleJson(record));
            }

            if (debugLog != null)
            {
                for (int i = 0; i < debugLog.Count; i++)
                {
                    var record = new Dictionary<string, object>(debugLog[i]);
                    record["record_type"] = "debug_log";
                    lines.Add(SimpleJson(record));
                }
            }

            if (lmStudioLog != null)
            {
                for (int i = 0; i < lmStudioLog.Count; i++)
                {
                    var record = new Dictionary<string, object>(lmStudioLog[i]);
                    record["record_type"] = "lmstudio_log";
                    lines.Add(SimpleJson(record));
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildDiagnosticIncidentRecentLogsText(
            List<Dictionary<string, object>> debugLog,
            List<Dictionary<string, object>> lmStudioLog
        )
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("Debug Log");
            builder.AppendLine();

            if (debugLog == null || debugLog.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                for (int i = 0; i < debugLog.Count; i++)
                {
                    Dictionary<string, object> entry = debugLog[i];
                    builder.AppendLine(
                        string.Format(
                            "[{0}] request={1} server={2} status={3} error={4}",
                            entry.TryGetValue("timestamp_ms", out object timestamp) ? timestamp : 0,
                            entry.TryGetValue("request_id", out object requestId) ? requestId : 0,
                            entry.TryGetValue("server_request_id", out object serverRequestId) ? serverRequestId : 0,
                            entry.TryGetValue("status", out object status) ? status : string.Empty,
                            entry.TryGetValue("error", out object error) ? error : string.Empty
                        )
                    );
                    builder.AppendLine(
                        $"  prompt={Coalesce(entry.TryGetValue("prompt_snippet", out object prompt) ? prompt?.ToString() : string.Empty, string.Empty)}"
                    );
                    builder.AppendLine(
                        $"  response={Coalesce(entry.TryGetValue("response_snippet", out object response) ? response?.ToString() : string.Empty, string.Empty)}"
                    );
                }
            }

            builder.AppendLine();
            builder.AppendLine("LM Studio Log");
            builder.AppendLine();

            if (lmStudioLog == null || lmStudioLog.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                for (int i = 0; i < lmStudioLog.Count; i++)
                {
                    Dictionary<string, object> entry = lmStudioLog[i];
                    builder.AppendLine(
                        string.Format(
                            "[{0}] mode={1} summary={2}",
                            entry.TryGetValue("timestamp_ms", out object timestamp) ? timestamp : 0,
                            entry.TryGetValue("mode", out object mode) ? mode : string.Empty,
                            entry.TryGetValue("summary", out object summary) ? summary : string.Empty
                        )
                    );
                    if (entry.TryGetValue("detail", out object detail) && detail != null)
                    {
                        builder.AppendLine($"  detail={detail}");
                    }
                }
            }

            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            string trimmed = value.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (invalid.Contains(c) || char.IsControl(c))
                {
                    builder.Append('_');
                    continue;
                }

                builder.Append(char.IsWhiteSpace(c) ? '_' : c);
            }

            return builder.ToString().Trim('_');
        }

        private static string Coalesce(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        /// <summary>
        /// Simple JSON serialization for dictionaries
        /// </summary>
        private static string SimpleJson(object obj)
        {
            return ValueToJson(obj);
        }

        private static string ValueToJson(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string s)
            {
                return $"\"{EscapeJsonString(s)}\"";
            }

            if (value is char c)
            {
                return $"\"{EscapeJsonString(c.ToString())}\"";
            }

            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            if (
                value is byte
                || value is sbyte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal
            )
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (value is System.Collections.IDictionary nonGenericDictionary)
            {
                var parts = new List<string>();
                foreach (System.Collections.DictionaryEntry entry in nonGenericDictionary)
                {
                    string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                    parts.Add($"\"{EscapeJsonString(key)}\":{ValueToJson(entry.Value)}");
                }

                return "{" + string.Join(",", parts) + "}";
            }

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (object item in enumerable)
                {
                    items.Add(ValueToJson(item));
                }

                return "[" + string.Join(",", items) + "]";
            }

            return $"\"{EscapeJsonString(value.ToString())}\"";
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        internal static string SerializeForEditorDebug(object value)
        {
            return SimpleJson(value);
        }

        private static Dictionary<string, object> SerializeUiBehaviorSnapshot(UIBehaviorSnapshot snapshot)
        {
            return new Dictionary<string, object>
            {
                ["run_id"] = snapshot.RunId,
                ["boot_id"] = snapshot.BootId,
                ["ui_id"] = snapshot.UiId,
                ["ui_kind"] = snapshot.UiKind,
                ["scene_name"] = snapshot.SceneName,
                ["frame"] = snapshot.Frame,
                ["realtime_since_startup"] = snapshot.RealtimeSinceStartup,
                ["is_visible"] = snapshot.IsVisible,
                ["conversation_ready"] = snapshot.ConversationReady,
                ["gameplay_input_suppressed"] = snapshot.GameplayInputSuppressed,
                ["has_authenticated_player"] = snapshot.HasAuthenticatedPlayer,
                ["send_enabled"] = snapshot.SendEnabled,
                ["send_blocked_reason"] = snapshot.SendBlockedReason,
                ["input_focused"] = snapshot.InputFocused,
                ["has_pending_request"] = snapshot.HasPendingRequest,
                ["pending_request_id"] = snapshot.PendingRequestId,
                ["selected_npc_network_object_id"] = snapshot.SelectedNpcNetworkObjectId,
                ["selected_npc_name"] = snapshot.SelectedNpcName,
                ["npc_in_range"] = snapshot.NpcInRange,
                ["transcript_line_count"] = snapshot.TranscriptLineCount,
                ["transcript_character_count"] = snapshot.TranscriptCharacterCount,
                ["auto_scroll_enabled"] = snapshot.AutoScrollEnabled,
                ["summary"] = snapshot.Summary,
            };
        }

        private static Dictionary<string, object> SerializeUiPerformanceSample(UIPerformanceSample sample)
        {
            return new Dictionary<string, object>
            {
                ["run_id"] = sample.RunId,
                ["boot_id"] = sample.BootId,
                ["ui_id"] = sample.UiId,
                ["ui_kind"] = sample.UiKind,
                ["scene_name"] = sample.SceneName,
                ["sample_name"] = sample.SampleName,
                ["frame"] = sample.Frame,
                ["realtime_since_startup"] = sample.RealtimeSinceStartup,
                ["duration_ms"] = sample.DurationMs,
                ["transcript_line_count"] = sample.TranscriptLineCount,
                ["transcript_character_count"] = sample.TranscriptCharacterCount,
                ["visible_element_count"] = sample.VisibleElementCount,
                ["text_element_count"] = sample.TextElementCount,
                ["layout_pass_count"] = sample.LayoutPassCount,
                ["notes"] = sample.Notes,
            };
        }

        private static Dictionary<string, object> SerializeBrainVariable(DiagnosticBrainVariable variable)
        {
            return new Dictionary<string, object>
            {
                ["key"] = variable.Key,
                ["kind"] = variable.Kind.ToString(),
                ["severity"] = variable.Severity.ToString(),
                ["phase"] = variable.Phase,
                ["value"] = variable.Value,
                ["confidence"] = variable.Confidence,
                ["source"] = variable.Source,
                ["pinned"] = variable.Pinned,
                ["created_at"] = variable.CreatedAt,
                ["expires_at"] = variable.ExpiresAt,
            };
        }

        private static Dictionary<string, object> SerializeVector3(Vector3 value)
        {
            return new Dictionary<string, object>
            {
                ["x"] = value.x,
                ["y"] = value.y,
                ["z"] = value.z,
            };
        }

        /// <summary>
        /// Build a compact scene snapshot around the local player (or camera fallback).
        /// </summary>
        public static string BuildSceneSnapshotText(
            int maxCount = DefaultSceneElementCount,
            float maxDistance = DefaultSceneProbeDistance
        )
        {
            List<SceneElement> elements = CollectSceneElements(maxCount, maxDistance);
            if (elements.Count == 0)
            {
                return "Scene snapshot: no gameplay-relevant renderers found.";
            }

            var builder = new StringBuilder(512);
            builder.AppendLine("Scene snapshot:");
            for (int i = 0; i < elements.Count; i++)
            {
                SceneElement element = elements[i];
                builder.Append("- ");
                builder.Append(element.Name);
                builder.Append(" at [");
                builder.Append(element.Position.x.ToString("F1"));
                builder.Append(", ");
                builder.Append(element.Position.y.ToString("F1"));
                builder.Append(", ");
                builder.Append(element.Position.z.ToString("F1"));
                builder.Append("], size ");
                builder.Append(element.Size.x.ToString("F1"));
                builder.Append("x");
                builder.Append(element.Size.y.ToString("F1"));
                builder.Append("x");
                builder.Append(element.Size.z.ToString("F1"));
                builder.Append("m, distance ");
                builder.Append(element.Distance.ToString("F1"));
                builder.Append("m");
                if (!string.IsNullOrWhiteSpace(element.SemanticRole))
                {
                    builder.Append(", role=");
                    builder.Append(element.SemanticRole);
                }
                if (!string.IsNullOrWhiteSpace(element.SemanticId))
                {
                    builder.Append(", id=");
                    builder.Append(element.SemanticId);
                }
                if (element.SemanticAliases != null && element.SemanticAliases.Length > 0)
                {
                    builder.Append(", aliases=");
                    builder.Append(string.Join("/", element.SemanticAliases));
                }
                builder.AppendLine(".");
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Send a scene-aware gameplay probe request to nearby NPCs.
        /// </summary>
        public static Dictionary<string, object> SendGameplayProbeToNpcs(
            string directive = null,
            int maxNpcs = 3,
            ulong listenerNetworkObjectId = 0
        )
        {
            if (
                !TryResolveGameplayProbeContext(
                    out NetworkDialogueService service,
                    out ulong localClientId,
                    out ulong listenerNetworkId,
                    out NetworkObject localPlayer,
                    out string error,
                    listenerNetworkObjectId
                )
            )
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = error ?? "Unknown probe setup error.",
                };
            }

            NpcDialogueActor[] actors = FindNpcActors();
            if (actors == null || actors.Length == 0)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = "No NpcDialogueActor found in scene.",
                };
            }

            string sceneSnapshot = BuildSceneSnapshotText();
            string effectiveDirective = string.IsNullOrWhiteSpace(directive)
                ? "Choose one nearby scene element, respond in character, and trigger one visual power so we can validate effect logic."
                : directive.Trim();

            int targetNpcCount = Mathf.Clamp(maxNpcs, 1, actors.Length);
            Vector3 anchor =
                localPlayer != null
                    ? localPlayer.transform.position
                    : GetSceneProbeAnchorPosition();
            List<NpcDialogueActor> orderedActors = GetOrderedSpawnedNpcActors(
                actors,
                anchor,
                targetNpcCount
            );

            var requestIds = new List<int>(orderedActors.Count);
            var npcNames = new List<string>(orderedActors.Count);
            for (int i = 0; i < orderedActors.Count; i++)
            {
                NpcDialogueActor actor = orderedActors[i];
                int requestId = SendGameplayProbeRequest(
                    service,
                    localClientId,
                    listenerNetworkId,
                    actor,
                    effectiveDirective,
                    sceneSnapshot
                );
                requestIds.Add(requestId);
                npcNames.Add(GetNpcDisplayName(actor));
            }

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["sent_count"] = requestIds.Count,
                ["request_ids"] = requestIds.ToArray(),
                ["npc_names"] = npcNames.ToArray(),
                ["scene_snapshot"] = sceneSnapshot,
                ["directive"] = effectiveDirective,
                ["listener_network_id"] = listenerNetworkId,
                ["listener_missing"] = localPlayer == null,
            };
        }

        /// <summary>
        /// Send a single scene-aware gameplay probe request to a specific NPC network object.
        /// </summary>
        public static Dictionary<string, object> SendGameplayProbeToNpc(
            ulong speakerNetworkObjectId,
            string directive = null,
            ulong listenerNetworkObjectId = 0
        )
        {
            if (
                !TryResolveGameplayProbeContext(
                    out NetworkDialogueService service,
                    out ulong localClientId,
                    out ulong listenerNetworkId,
                    out _,
                    out string error,
                    listenerNetworkObjectId
                )
            )
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = error ?? "Unknown probe setup error.",
                };
            }

            NpcDialogueActor[] actors = FindNpcActors();
            if (actors == null || actors.Length == 0)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = "No NpcDialogueActor found in scene.",
                };
            }

            NpcDialogueActor targetActor = null;
            for (int i = 0; i < actors.Length; i++)
            {
                NpcDialogueActor candidate = actors[i];
                if (
                    candidate == null
                    || candidate.NetworkObject == null
                    || !candidate.NetworkObject.IsSpawned
                )
                {
                    continue;
                }

                if (candidate.NetworkObjectId == speakerNetworkObjectId)
                {
                    targetActor = candidate;
                    break;
                }
            }

            if (targetActor == null)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = $"NPC with NetworkObjectId={speakerNetworkObjectId} not found.",
                };
            }

            string sceneSnapshot = BuildSceneSnapshotText();
            string effectiveDirective = string.IsNullOrWhiteSpace(directive)
                ? "Choose one nearby scene element, respond in character, and trigger one visual power so we can validate effect logic."
                : directive.Trim();

            int requestId = SendGameplayProbeRequest(
                service,
                localClientId,
                listenerNetworkId,
                targetActor,
                effectiveDirective,
                sceneSnapshot
            );

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["sent_count"] = 1,
                ["request_id"] = requestId,
                ["speaker_network_id"] = speakerNetworkObjectId,
                ["npc_name"] = GetNpcDisplayName(targetActor),
                ["scene_snapshot"] = sceneSnapshot,
                ["directive"] = effectiveDirective,
                ["listener_network_id"] = listenerNetworkId,
            };
        }

        /// <summary>
        /// Send a deterministic animation validation probe to a specific NPC.
        /// This mirrors the effect validation flow but forces an [ANIM:] response.
        /// </summary>
        public static Dictionary<string, object> SendAnimationProbeToNpc(
            ulong speakerNetworkObjectId,
            string animationTag = "EmphasisReact",
            ulong listenerNetworkObjectId = 0
        )
        {
            if (
                !TryResolveGameplayProbeContext(
                    out NetworkDialogueService service,
                    out ulong localClientId,
                    out ulong listenerNetworkId,
                    out _,
                    out string error,
                    listenerNetworkObjectId
                )
            )
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = error ?? "Unknown probe setup error.",
                };
            }

            NpcDialogueActor[] actors = FindNpcActors();
            if (actors == null || actors.Length == 0)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = "No NpcDialogueActor found in scene.",
                };
            }

            NpcDialogueActor targetActor = null;
            for (int i = 0; i < actors.Length; i++)
            {
                NpcDialogueActor candidate = actors[i];
                if (
                    candidate == null
                    || candidate.NetworkObject == null
                    || !candidate.NetworkObject.IsSpawned
                )
                {
                    continue;
                }

                if (candidate.NetworkObjectId == speakerNetworkObjectId)
                {
                    targetActor = candidate;
                    break;
                }
            }

            if (targetActor == null)
            {
                return new Dictionary<string, object>
                {
                    ["ok"] = false,
                    ["error"] = $"NPC with NetworkObjectId={speakerNetworkObjectId} not found.",
                };
            }

            int requestId = ++s_NextGameplayCopilotRequestId;
            string key = service.ResolveConversationKey(
                targetActor.NetworkObjectId,
                listenerNetworkId,
                localClientId,
                null
            );

            string resolvedTag = string.IsNullOrWhiteSpace(animationTag)
                ? "EmphasisReact"
                : animationTag.Trim();
            string prompt = BuildAnimationProbePrompt(resolvedTag);
            service.RequestDialogue(
                new NetworkDialogueService.DialogueRequest
                {
                    Prompt = prompt,
                    ConversationKey = key,
                    SpeakerNetworkId = targetActor.NetworkObjectId,
                    ListenerNetworkId = listenerNetworkId,
                    RequestingClientId = localClientId,
                    Broadcast = true,
                    BroadcastDuration = 3f,
                    NotifyClient = true,
                    ClientRequestId = requestId,
                    IsUserInitiated = true,
                    BlockRepeatedPrompt = false,
                    MinRepeatDelaySeconds = 0f,
                    RequireUserReply = false,
                }
            );

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["sent_count"] = 1,
                ["request_id"] = requestId,
                ["speaker_network_id"] = speakerNetworkObjectId,
                ["npc_name"] = GetNpcDisplayName(targetActor),
                ["animation_tag"] = resolvedTag,
                ["listener_network_id"] = listenerNetworkId,
                ["prompt"] = prompt,
            };
        }

        /// <summary>
        /// Return nearby spawned NPC dialogue actors sorted by distance to local player/camera.
        /// </summary>
        public static List<Dictionary<string, object>> GetNearbyNpcActors(int maxNpcs = 6)
        {
            var result = new List<Dictionary<string, object>>();
            if (!Application.isPlaying)
            {
                return result;
            }

            NpcDialogueActor[] actors = FindNpcActors();
            if (actors == null || actors.Length == 0)
            {
                return result;
            }

            Vector3 anchor = GetSceneProbeAnchorPosition();
            int targetNpcCount = Mathf.Clamp(maxNpcs, 1, actors.Length);
            List<NpcDialogueActor> orderedActors = GetOrderedSpawnedNpcActors(
                actors,
                anchor,
                targetNpcCount
            );

            for (int i = 0; i < orderedActors.Count; i++)
            {
                NpcDialogueActor actor = orderedActors[i];
                float distance = Vector3.Distance(anchor, actor.transform.position);
                NpcDialogueProfile profile = actor.Profile;
                result.Add(
                    new Dictionary<string, object>
                    {
                        ["network_id"] = actor.NetworkObjectId,
                        ["name"] = GetNpcDisplayName(actor),
                        ["distance"] = distance,
                        ["profile_id"] = profile != null ? profile.ProfileId : string.Empty,
                        ["power_names"] = GetEnabledPowerNames(profile),
                    }
                );
            }

            return result;
        }

        private static string[] GetEnabledPowerNames(NpcDialogueProfile profile)
        {
            if (profile == null || profile.PrefabPowers == null || profile.PrefabPowers.Length == 0)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>(profile.PrefabPowers.Length);
            for (int i = 0; i < profile.PrefabPowers.Length; i++)
            {
                PrefabPowerEntry power = profile.PrefabPowers[i];
                if (power == null || !power.Enabled)
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(power.PowerName)
                    ? power.EffectPrefab != null
                        ? power.EffectPrefab.name
                        : string.Empty
                    : power.PowerName.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static bool TryResolveGameplayProbeContext(
            out NetworkDialogueService service,
            out ulong localClientId,
            out ulong listenerNetworkId,
            out NetworkObject localPlayer,
            out string error,
            ulong preferredListenerNetworkId = 0
        )
        {
            service = NetworkDialogueService.Instance;
            localClientId = 0;
            listenerNetworkId = 0;
            localPlayer = null;
            error = null;

            if (!Application.isPlaying)
            {
                error = "Enter Play Mode first.";
                return false;
            }

            if (service == null)
            {
                error = "NetworkDialogueService not found.";
                return false;
            }

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
            {
                error = "Local Netcode client not ready.";
                return false;
            }

            localClientId = NetworkManager.Singleton.LocalClientId;
            localPlayer = ResolveLocalPlayerObject(localClientId);
            if (preferredListenerNetworkId != 0)
            {
                NetworkObject explicitListener = ResolveProbeListenerObject(
                    preferredListenerNetworkId
                );
                if (explicitListener == null)
                {
                    error =
                        $"Listener player NetworkObjectId={preferredListenerNetworkId} is not a valid spawned player target.";
                    return false;
                }

                listenerNetworkId = explicitListener.NetworkObjectId;
                return true;
            }

            listenerNetworkId = localPlayer != null ? localPlayer.NetworkObjectId : 0;
            return true;
        }

        private static List<NpcDialogueActor> GetOrderedSpawnedNpcActors(
            NpcDialogueActor[] actors,
            Vector3 anchor,
            int maxCount
        )
        {
            int targetCount = Mathf.Max(1, maxCount);
            return actors
                .Where(actor =>
                    actor != null && actor.NetworkObject != null && actor.NetworkObject.IsSpawned
                )
                .OrderBy(actor => Vector3.Distance(actor.transform.position, anchor))
                .Take(targetCount)
                .ToList();
        }

        private static int SendGameplayProbeRequest(
            NetworkDialogueService service,
            ulong localClientId,
            ulong listenerNetworkId,
            NpcDialogueActor actor,
            string effectiveDirective,
            string sceneSnapshot
        )
        {
            int requestId = ++s_NextGameplayCopilotRequestId;
            string key = service.ResolveConversationKey(
                actor.NetworkObjectId,
                listenerNetworkId,
                localClientId,
                null
            );

            string prompt = BuildGameplayPrompt(actor, effectiveDirective, sceneSnapshot);
            service.RequestDialogue(
                new NetworkDialogueService.DialogueRequest
                {
                    Prompt = prompt,
                    ConversationKey = key,
                    SpeakerNetworkId = actor.NetworkObjectId,
                    ListenerNetworkId = listenerNetworkId,
                    RequestingClientId = localClientId,
                    Broadcast = true,
                    BroadcastDuration = 3f,
                    NotifyClient = true,
                    ClientRequestId = requestId,
                    IsUserInitiated = true,
                    BlockRepeatedPrompt = false,
                    MinRepeatDelaySeconds = 0f,
                    RequireUserReply = false,
                }
            );
            return requestId;
        }

        private static string BuildAnimationProbePrompt(string animationTag)
        {
            string tag = string.IsNullOrWhiteSpace(animationTag)
                ? "EmphasisReact"
                : animationTag.Trim();

            return string.Concat(
                    "Animation validation step. Respond in character with one short sentence, then append exactly one tag: [ANIM: ",
                    tag,
                    " | Target: Self].",
                    " Think in visible scene results only: play the animation on your own body and keep it on yourself.",
                    " Output only the final sentence and tag. No analysis, no extra lines.",
                    " Do not emit any [EFFECT:] tags for this step.",
                    " The animation tag name must match exactly and Target must stay Self."
                )
                .Trim();
        }

        private static List<SceneElement> CollectSceneElements(int maxCount, float maxDistance)
        {
            int targetCount = Mathf.Max(1, maxCount);
            Vector3 anchor = GetSceneProbeAnchorPosition();
            var elements = new List<SceneElement>(targetCount);
            var seenRoots = new HashSet<EntityId>();

            Renderer[] renderers = FindSceneRenderers();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                GameObject root =
                    renderer.transform.root != null
                        ? renderer.transform.root.gameObject
                        : renderer.gameObject;

                if (!seenRoots.Add(root.GetEntityId()) || IsIgnoredSceneRoot(root))
                {
                    continue;
                }

                DialogueSemanticTag semantic = ResolveSemanticTag(root);
                if (semantic != null && !semantic.IncludeInSceneSnapshots)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float distance = Vector3.Distance(anchor, bounds.center);
                if (maxDistance > 0f && distance > maxDistance)
                {
                    continue;
                }

                string semanticId = semantic != null ? semantic.SemanticId : string.Empty;
                string semanticRole = semantic != null ? semantic.RoleKey : string.Empty;
                string[] semanticAliases =
                    semantic != null ? semantic.GetCompactAliases() : Array.Empty<string>();
                elements.Add(
                    new SceneElement
                    {
                        Name = semantic != null ? semantic.ResolveDisplayName(root) : root.name,
                        Path = BuildGameObjectPath(root.transform),
                        Position = bounds.center,
                        Size = bounds.size,
                        Distance = distance,
                        SemanticId = semanticId,
                        SemanticRole = semanticRole,
                        SemanticAliases = semanticAliases,
                        SemanticPriority = semantic != null ? semantic.Priority : 0,
                    }
                );
            }

            return elements
                .OrderByDescending(element => element.SemanticPriority)
                .ThenBy(element => element.Distance)
                .ThenByDescending(element => element.Size.x * element.Size.y * element.Size.z)
                .Take(targetCount)
                .ToList();
        }

        private static DialogueSemanticTag ResolveSemanticTag(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            DialogueSemanticTag tag = root.GetComponent<DialogueSemanticTag>();
            if (tag != null)
            {
                return tag;
            }

            return root.GetComponentInChildren<DialogueSemanticTag>(true);
        }

        private static string BuildGameplayPrompt(
            NpcDialogueActor actor,
            string directive,
            string sceneSnapshot
        )
        {
            string npcName = GetNpcDisplayName(actor);
            var builder = new StringBuilder(640);
            builder.AppendLine($"Gameplay probe for {npcName}.");
            builder.AppendLine(directive);
            builder.AppendLine("Keep the response short (1-2 sentences).");
            builder.AppendLine(
                "When triggering a power, append exactly one [EFFECT: ...] tag at the end."
            );
            builder.AppendLine(sceneSnapshot);
            return builder.ToString().TrimEnd();
        }

        private static string GetNpcDisplayName(NpcDialogueActor actor)
        {
            if (actor == null)
            {
                return "NPC";
            }

            if (actor.Profile != null && !string.IsNullOrWhiteSpace(actor.Profile.DisplayName))
            {
                return actor.Profile.DisplayName.Trim();
            }

            return actor.name;
        }

        private static Vector3 GetSceneProbeAnchorPosition()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                NetworkObject localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localPlayer != null)
                {
                    return localPlayer.transform.position;
                }
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.position;
            }

            return Vector3.zero;
        }

        private static bool IsIgnoredSceneRoot(GameObject root)
        {
            if (root == null)
            {
                return true;
            }

            if (root.GetComponentInChildren<NpcDialogueActor>() != null)
            {
                return true;
            }

            if (root.GetComponentInChildren<Camera>() != null)
            {
                return true;
            }

            if (root.GetComponentInChildren<Canvas>() != null)
            {
                return true;
            }

            string name = root.name ?? string.Empty;
            return name.StartsWith("Network", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("EventSystem", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Dialogue", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("MainCamera", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("PlayerCinemachine", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Modern_HUD", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildGameObjectPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new List<string>(8);
            Transform cursor = transform;
            while (cursor != null)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static NpcDialogueActor[] FindNpcActors()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<NpcDialogueActor>(
                FindObjectsInactive.Exclude
            );
#else
            return UnityEngine.Object.FindObjectsOfType<NpcDialogueActor>();
#endif
        }

        /// <summary>Internal helper for same-assembly use (e.g. DialogueBatchTester).</summary>
        internal static NetworkObject ResolveLocalPlayerObjectPublic(ulong localClientId)
            => ResolveLocalPlayerObject(localClientId);

        private static NetworkObject ResolveLocalPlayerObject(ulong localClientId)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                NetworkObject direct = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (direct != null)
                {
                    return direct;
                }
            }

            NetworkObject[] objects = FindSceneNetworkObjects();
            for (int i = 0; i < objects.Length; i++)
            {
                NetworkObject candidate = objects[i];
                if (
                    candidate == null
                    || !candidate.IsSpawned
                    || candidate.OwnerClientId != localClientId
                )
                {
                    continue;
                }

                if (candidate.GetComponent<NpcDialogueActor>() != null)
                {
                    continue;
                }

                if (candidate.GetComponent<NetworkDialogueService>() != null)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsValidProbeListenerPlayer(NetworkObject candidate)
        {
            if (candidate == null || !candidate.IsSpawned)
            {
                return false;
            }

            if (candidate.GetComponent<NpcDialogueActor>() != null)
            {
                return false;
            }

            if (candidate.GetComponent<NetworkDialogueService>() != null)
            {
                return false;
            }

            return true;
        }

        private static NetworkObject ResolveProbeListenerObject(ulong listenerNetworkObjectId)
        {
            if (listenerNetworkObjectId == 0)
            {
                return null;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (
                manager != null
                && manager.IsListening
                && manager.SpawnManager != null
                && manager.SpawnManager.SpawnedObjects != null
                && manager.SpawnManager.SpawnedObjects.TryGetValue(
                    listenerNetworkObjectId,
                    out NetworkObject spawned
                )
                && IsValidProbeListenerPlayer(spawned)
            )
            {
                return spawned;
            }

            NetworkObject[] objects = FindSceneNetworkObjects();
            for (int i = 0; i < objects.Length; i++)
            {
                NetworkObject candidate = objects[i];
                if (
                    candidate != null
                    && candidate.NetworkObjectId == listenerNetworkObjectId
                    && IsValidProbeListenerPlayer(candidate)
                )
                {
                    return candidate;
                }
            }

            return null;
        }

        internal static List<(
            ulong networkId,
            ulong clientId,
            string label
        )> GetProbeListenerTargets()
        {
            var targets = new List<(ulong networkId, ulong clientId, string label)>();
            NetworkManager manager = NetworkManager.Singleton;
            if (
                manager == null
                || !manager.IsListening
                || manager.ConnectedClients == null
                || manager.ConnectedClients.Count == 0
            )
            {
                return targets;
            }

            foreach (var pair in manager.ConnectedClients)
            {
                ulong clientId = pair.Key;
                NetworkClient client = pair.Value;
                NetworkObject playerObject = client != null ? client.PlayerObject : null;
                if (!IsValidProbeListenerPlayer(playerObject))
                {
                    continue;
                }

                string label = $"client_{clientId}";
                NetworkDialogueService service = NetworkDialogueService.Instance;
                if (
                    service != null
                    && service.TryGetPlayerIdentityByClientId(clientId, out var snapshot)
                    && !string.IsNullOrWhiteSpace(snapshot.NameId)
                )
                {
                    label = snapshot.NameId.Trim();
                }

                targets.Add((playerObject.NetworkObjectId, clientId, label));
            }

            return targets
                .OrderBy(target => target.clientId)
                .ThenBy(target => target.networkId)
                .ToList();
        }

        private static NetworkObject[] FindSceneNetworkObjects()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude);
#else
            return UnityEngine.Object.FindObjectsOfType<NetworkObject>();
#endif
        }

        private static Renderer[] FindSceneRenderers()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
#else
            return UnityEngine.Object.FindObjectsOfType<Renderer>();
#endif
        }

        // ─── Debug Log Ring Buffer ────────────────────────────────────────────────

        private const int DebugLogCapacity = 100;

        /// <summary>Immutable record of one NPC dialogue exchange for MCP debug tooling.</summary>
        public struct DebugLogEntry
        {
            public long TimestampMs;
            public int RequestId;
            public int ServerRequestId;
            public ulong SpeakerNetworkId;
            public ulong ListenerNetworkId;
            public string ConversationKey;
            public string PromptSnippet;
            public string ResponseSnippet;
            public string[] EffectTagsParsed;
            public string[] AnimationTagsParsed;
            public string Status;
            public string Error;
            public int RetryCount;
            public float QueueLatencyMs;
            public float ModelLatencyMs;
            public float TotalLatencyMs;
            public bool IsUserInitiated;
        }

        /// <summary>LM Studio analysis result pushed back from the Python server.</summary>
        public struct LMStudioLogEntry
        {
            public long TimestampMs;
            public string Mode; // "mirror" | "critique"
            public string Summary;
            public string Detail;
        }

        private static readonly Queue<DebugLogEntry> s_DebugLog = new Queue<DebugLogEntry>(
            DebugLogCapacity
        );
        private static readonly Queue<LMStudioLogEntry> s_LMStudioLog = new Queue<LMStudioLogEntry>(
            20
        );
        private static readonly object s_LogLock = new object();

        private static readonly System.Text.RegularExpressions.Regex s_EffectTagRegex =
            new System.Text.RegularExpressions.Regex(
                @"\[EFFECT:\s*([^\]]+)\]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.Compiled
            );
        private static readonly System.Text.RegularExpressions.Regex s_AnimationTagRegex =
            new System.Text.RegularExpressions.Regex(
                @"\[ANIM:\s*([^\]]+)\]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.Compiled
            );

        /// <summary>
        /// Called by NetworkDialogueService after each completed response to populate the ring buffer.
        /// </summary>
        public static void LogDialogueDebugEntry(
            NetworkDialogueService.DialogueRequest request,
            string responseText,
            string status,
            string error = "",
            int retryCount = 0,
            float queueLatencyMs = 0f,
            float modelLatencyMs = 0f,
            float totalLatencyMs = 0f,
            int serverRequestId = 0
        )
        {
            string[] effectTags = ExtractEffectTags(responseText);
            string[] animationTags = ExtractAnimationTags(responseText);
            var entry = new DebugLogEntry
            {
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                RequestId = request.ClientRequestId,
                ServerRequestId = serverRequestId,
                SpeakerNetworkId = request.SpeakerNetworkId,
                ListenerNetworkId = request.ListenerNetworkId,
                ConversationKey = request.ConversationKey ?? string.Empty,
                PromptSnippet = Truncate(request.Prompt, 200),
                ResponseSnippet = Truncate(responseText, 300),
                EffectTagsParsed = effectTags,
                AnimationTagsParsed = animationTags,
                Status = status ?? "unknown",
                Error = error ?? string.Empty,
                RetryCount = retryCount,
                QueueLatencyMs = queueLatencyMs,
                ModelLatencyMs = modelLatencyMs,
                TotalLatencyMs = totalLatencyMs,
                IsUserInitiated = request.IsUserInitiated,
            };

            lock (s_LogLock)
            {
                if (s_DebugLog.Count >= DebugLogCapacity)
                    s_DebugLog.Dequeue();
                s_DebugLog.Enqueue(entry);
            }
        }

        /// <summary>
        /// Stores an LM Studio result entry for display in DialogueDebugPanel.
        /// </summary>
        public static void AddLMStudioLogEntry(LMStudioLogEntry entry)
        {
            lock (s_LogLock)
            {
                if (s_LMStudioLog.Count >= 20)
                    s_LMStudioLog.Dequeue();
                s_LMStudioLog.Enqueue(entry);
            }
        }

        /// <summary>
        /// Returns last N debug log entries as serialisable dictionaries for MCP.
        /// </summary>
        public static List<Dictionary<string, object>> GetDebugLog(int maxEntries = 30)
        {
            var result = new List<Dictionary<string, object>>();
            lock (s_LogLock)
            {
                var entries = s_DebugLog.ToArray();
                int start = Math.Max(0, entries.Length - maxEntries);
                for (int i = start; i < entries.Length; i++)
                {
                    var e = entries[i];
                    result.Add(
                        new Dictionary<string, object>
                        {
                            ["timestamp_ms"] = e.TimestampMs,
                            ["request_id"] = e.RequestId,
                            ["server_request_id"] = e.ServerRequestId,
                            ["speaker_network_id"] = e.SpeakerNetworkId.ToString(),
                            ["listener_network_id"] = e.ListenerNetworkId.ToString(),
                            ["conversation_key"] = e.ConversationKey,
                            ["prompt_snippet"] = e.PromptSnippet,
                            ["response_snippet"] = e.ResponseSnippet,
                            ["effect_tags_parsed"] = e.EffectTagsParsed,
                            ["animation_tags_parsed"] = e.AnimationTagsParsed,
                            ["status"] = e.Status,
                            ["error"] = e.Error,
                            ["retry_count"] = e.RetryCount,
                            ["queue_latency_ms"] = e.QueueLatencyMs,
                            ["model_latency_ms"] = e.ModelLatencyMs,
                            ["total_latency_ms"] = e.TotalLatencyMs,
                            ["is_user_initiated"] = e.IsUserInitiated,
                        }
                    );
                }
            }
            return result;
        }

        /// <summary>
        /// Returns LM Studio log entries for the debug panel.
        /// </summary>
        public static List<LMStudioLogEntry> GetLMStudioLog(int maxEntries = 10)
        {
            lock (s_LogLock)
            {
                var all = s_LMStudioLog.ToArray();
                int start = Math.Max(0, all.Length - maxEntries);
                var result = new List<LMStudioLogEntry>();
                for (int i = start; i < all.Length; i++)
                    result.Add(all[i]);
                return result;
            }
        }

        /// <summary>
        /// Scans the scene for active ParticleSystems tagged as dialogue-spawned.
        /// Returns name, position, remaining lifetime, and source NPC.
        /// </summary>
        public static List<Dictionary<string, object>> GetActiveVfxState()
        {
            var result = new List<Dictionary<string, object>>();
            if (!Application.isPlaying)
                return result;

#if UNITY_2023_1_OR_NEWER
            var systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Exclude
            );
#else
            var systems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
#endif
            foreach (var ps in systems)
            {
                if (ps == null || !ps.isPlaying)
                    continue;

                // Only include systems that carry the dialogue-spawned marker
                if (ps.GetComponent<DialogueSpawnedMarker>() == null)
                    continue;

                var marker = ps.GetComponent<DialogueSpawnedMarker>();
                Vector3 pos = ps.transform.position;
                var main = ps.main;
                var emission = ps.emission;
                var shape = ps.shape;
                Vector3 worldScale = ps.transform.lossyScale;

                result.Add(
                    new Dictionary<string, object>
                    {
                        ["name"] = ps.gameObject.name,
                        ["position"] = new[] { pos.x, pos.y, pos.z },
                        ["duration_remaining"] = Mathf.Max(0f, main.duration - ps.time),
                        ["source_npc"] = marker.SourceNpcProfileId ?? string.Empty,
                        ["effect_tag"] = marker.EffectTag ?? string.Empty,
                        ["source_network_id"] = marker.SourceNetworkObjectId.ToString(),
                        ["target_network_id"] = marker.TargetNetworkObjectId.ToString(),
                        ["configured_scale"] = marker.ConfiguredScale,
                        ["configured_duration"] = marker.ConfiguredDurationSeconds,
                        ["attach_to_target"] = marker.AttachToTarget,
                        ["fit_to_target_mesh"] = marker.FitToTargetMesh,
                        ["seed"] = marker.EffectSeed.ToString(),
                        ["world_scale"] = new[] { worldScale.x, worldScale.y, worldScale.z },
                        ["main_duration"] = main.duration,
                        ["simulation_speed"] = main.simulationSpeed,
                        ["max_particles"] = main.maxParticles,
                        ["start_size"] = ResolveCurveMid(main.startSize),
                        ["start_speed"] = ResolveCurveMid(main.startSpeed),
                        ["start_lifetime"] = ResolveCurveMid(main.startLifetime),
                        ["emission_rate_over_time"] = ResolveCurveMid(emission.rateOverTime),
                        ["emission_rate_over_distance"] = ResolveCurveMid(
                            emission.rateOverDistance
                        ),
                        ["shape_radius"] = shape.enabled ? shape.radius : 0f,
                        ["shape_angle"] = shape.enabled ? shape.angle : 0f,
                        ["is_attached"] = ps.transform.parent != null,
                    }
                );
            }
            return result;
        }

        private static float ResolveCurveMid(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return (curve.constantMin + curve.constantMax) * 0.5f;
                case ParticleSystemCurveMode.Curve:
                    return curve.curve != null
                        ? curve.curve.Evaluate(0.5f) * curve.curveMultiplier
                        : 0f;
                case ParticleSystemCurveMode.TwoCurves:
                    float min = curve.curveMin != null ? curve.curveMin.Evaluate(0.5f) : 0f;
                    float max = curve.curveMax != null ? curve.curveMax.Evaluate(0.5f) : 0f;
                    return (min + max) * 0.5f * curve.curveMultiplier;
                default:
                    return curve.constant;
            }
        }

        private static string[] ExtractEffectTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();
            var matches = s_EffectTagRegex.Matches(text);
            if (matches.Count == 0)
                return Array.Empty<string>();
            var tags = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                tags[i] = matches[i].Groups[1].Value.Trim();
            return tags;
        }

        private static string[] ExtractAnimationTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();
            var matches = s_AnimationTagRegex.Matches(text);
            if (matches.Count == 0)
                return Array.Empty<string>();
            var tags = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                tags[i] = matches[i].Groups[1].Value.Trim();
            return tags;
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor menu to expose dialogue MCP commands
    /// </summary>
    public static class DialogueMCPMenu
    {
        private const string SceneScanMenuPath = "Network Game/MCP/Gameplay Copilot/Scan Scene";
        private const string ProbeNearbyNpcsMenuPath =
            "Network Game/MCP/Gameplay Copilot/Probe Nearby NPCs";
        private const string RunAutomatedProbesMenuPath =
            "Network Game/MCP/Gameplay Copilot/Run Automated Probes (Feedback-Gated)";
        private const string RunAutomatedTrainingProbesMenuPath =
            "Network Game/MCP/Gameplay Copilot/Run Automated Probes (Training Mode, Skip Narrative)";
        private const string StopAutomatedProbesMenuPath =
            "Network Game/MCP/Gameplay Copilot/Stop Automated Probes";
        private const string ClearAutomationPlacementMemoryMenuPath =
            "Network Game/MCP/Gameplay Copilot/Clear Placement Memory";
        private const string RunBatchTestMenuPath =
            "Network Game/MCP/Batch Test/Run Animation+Effect Batch Test";
        private const string GetBatchResultsMenuPath =
            "Network Game/MCP/Batch Test/Get Batch Results";
        private const string DumpSystemPromptMenuPath =
            "Network Game/MCP/Debug/Dump NPC System Prompt";
        private const string ExportIncidentBundleMenuPath =
            "Network Game/MCP/Debug/Export Diagnostic Incident Bundle";
        private const string TestDirectDispatchMenuPath =
            "Network Game/MCP/Debug/Test All Animations Direct";
        private const string AutomationPlacementMemoryRelativePath =
            "output/automation_probe_placement_memory.json";
        private const int MaxAutomatedNpcCount = 4;
        private const int MaxAutomatedEffectsPerRun = 24;
        private static readonly string[] s_AutomationSpecialEffectTags = { "dissolve", "respawn" };
        private static readonly string[] s_AutomationFallbackEffectTags =
        {
            "FireBall",
            "Lightning Storm",
            "Ice Lance",
            "Ground Fog",
            "Forge Sparks",
            "Guide Fireflies",
        };
        private static readonly HashSet<int> s_GameplayProbeRequestIds = new HashSet<int>();
        private static readonly Dictionary<
            string,
            HashSet<string>
        > s_AutomationDisfavoredPlacementsByTag = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase
        );
        private static bool s_AutomationPlacementMemoryLoaded;
        private static bool s_GameplayProbeSubscribed;
        private static GameplayProbeAutomationRunner s_AutomationRunner;

        [Serializable]
        private sealed class AutomationPlacementMemoryEntry
        {
            public string effect_tag = string.Empty;
            public List<string> disfavored_placements = new List<string>();
        }

        [Serializable]
        private sealed class AutomationPlacementMemoryFile
        {
            public int schema_version = 1;
            public string updated_utc = string.Empty;
            public List<AutomationPlacementMemoryEntry> entries =
                new List<AutomationPlacementMemoryEntry>();
        }

        /// <summary>
        /// Get dialogue stats - use via menu "Network Game/MCP/Dialogue Stats"
        /// </summary>
        [UnityEditor.MenuItem("Network Game/MCP/Dialogue Stats")]
        public static void PrintStats()
        {
            var stats = DialogueMCPBridge.GetStats();
            if (stats == null)
            {
                Debug.LogWarning("[DialogueMCP] NetworkDialogueService not found in scene.");
                return;
            }

            var json = DialogueMCPBridge.SerializeForEditorDebug(stats);
            UnityEditor.EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"[DialogueMCP] Stats: {json}");
        }

        /// <summary>
        /// Get LLM status - use via menu "Network Game/MCP/LLM Status"
        /// </summary>
        [UnityEditor.MenuItem("Network Game/MCP/LLM Status")]
        public static void PrintLLMStatus()
        {
            var status = DialogueMCPBridge.GetLLMStatus();
            if (status == null)
            {
                Debug.LogWarning("[DialogueMCP] NetworkDialogueService not found in scene.");
                return;
            }

            var json = DialogueMCPBridge.SerializeForEditorDebug(status);
            UnityEditor.EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"[DialogueMCP] LLM Status: {json}");
        }

        /// <summary>
        /// List all profiles - use via menu "Network Game/MCP/List Profiles"
        /// </summary>
        [UnityEditor.MenuItem("Network Game/MCP/List Profiles")]
        public static void ListProfiles()
        {
            var profiles = DialogueMCPBridge.GetProfiles();
            var json = DialogueMCPBridge.SerializeForEditorDebug(profiles);
            UnityEditor.EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"[DialogueMCP] Profiles ({profiles.Count}): {json}");
        }

        /// <summary>
        /// List effect types - use via menu "Network Game/MCP/List Effects"
        /// </summary>
        [UnityEditor.MenuItem("Network Game/MCP/List Effects")]
        public static void ListEffects()
        {
            var effects = DialogueMCPBridge.GetEffectTypes();
            var json = DialogueMCPBridge.SerializeForEditorDebug(effects);
            UnityEditor.EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"[DialogueMCP] Effects: {json}");
        }

        /// <summary>
        /// Get queue status - use via menu "Network Game/MCP/Queue Status"
        /// </summary>
        [UnityEditor.MenuItem("Network Game/MCP/Queue Status")]
        public static void QueueStatus()
        {
            var queue = DialogueMCPBridge.GetQueueStatus();
            if (queue == null)
            {
                Debug.LogWarning("[DialogueMCP] NetworkDialogueService not found.");
                return;
            }

            var json = DialogueMCPBridge.SerializeForEditorDebug(queue);
            UnityEditor.EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"[DialogueMCP] Queue: {json}");
        }

        [UnityEditor.MenuItem(SceneScanMenuPath)]
        public static void ScanGameplayScene()
        {
            string snapshot = DialogueMCPBridge.BuildSceneSnapshotText();
            UnityEditor.EditorGUIUtility.systemCopyBuffer = snapshot;
            Debug.Log($"[DialogueMCP] Scene scan:\n{snapshot}");
        }

        [UnityEditor.MenuItem(ProbeNearbyNpcsMenuPath)]
        public static void ProbeNearbyNpcs()
        {
            EnsureGameplayProbeSubscription();
            Dictionary<string, object> result = DialogueMCPBridge.SendGameplayProbeToNpcs();
            if (result == null)
            {
                Debug.LogWarning("[DialogueMCP] Gameplay probe failed: null result.");
                return;
            }

            bool ok =
                result.TryGetValue("ok", out object okValue) && okValue is bool boolOk && boolOk;

            if (!ok)
            {
                string error = result.TryGetValue("error", out object errorValue)
                    ? errorValue?.ToString()
                    : "unknown error";
                Debug.LogWarning($"[DialogueMCP] Gameplay probe failed: {error}");
                return;
            }

            if (
                result.TryGetValue("request_ids", out object requestIdsValue)
                && requestIdsValue is int[] requestIds
            )
            {
                for (int i = 0; i < requestIds.Length; i++)
                {
                    s_GameplayProbeRequestIds.Add(requestIds[i]);
                }
            }

            int sentCount =
                result.TryGetValue("sent_count", out object sentValue) && sentValue is int sent
                    ? sent
                    : 0;

            string snapshot = result.TryGetValue("scene_snapshot", out object snapshotValue)
                ? snapshotValue?.ToString() ?? string.Empty
                : string.Empty;

            Debug.Log(
                $"[DialogueMCP] Gameplay probe sent to {sentCount} NPC(s). Waiting for responses...\n{snapshot}"
            );
        }

        [UnityEditor.MenuItem(RunAutomatedProbesMenuPath)]
        public static void RunAutomatedProbes()
        {
            RunAutomatedProbesInternal(includeNarrativePrelude: true);
        }

        [UnityEditor.MenuItem(RunAutomatedTrainingProbesMenuPath)]
        public static void RunAutomatedTrainingProbes()
        {
            RunAutomatedProbesInternal(includeNarrativePrelude: false);
        }

        private static void RunAutomatedProbesInternal(bool includeNarrativePrelude)
        {
            EnsureGameplayProbeSubscription();
            EnsureAutomationPlacementMemoryLoaded();

            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Automated Probes",
                    "Enter Play Mode first, then click Run Automated Probes.",
                    "OK"
                );
                return;
            }

            bool netcodeReady =
                NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.LocalClient != null;

            if (!netcodeReady)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Automated Probes",
                    "NetworkManager is not started.\n\nPress 'Start Host' in the NetworkManager inspector, then try again.",
                    "OK"
                );
                return;
            }

            if (s_AutomationRunner != null && s_AutomationRunner.IsRunning)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Automated Probes",
                    "Probes are already running.\n\nUse  Network Game > MCP > Gameplay Copilot > Stop Automated Probes  first.",
                    "OK"
                );
                return;
            }

            List<Dictionary<string, object>> nearbyNpcs = DialogueMCPBridge.GetNearbyNpcActors(
                MaxAutomatedNpcCount
            );
            if (nearbyNpcs.Count == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Automated Probes",
                    "No spawned NPCs found.\n\nMake sure at least one NpcDialogueActor with a NetworkObject is spawned in the scene.",
                    "OK"
                );
                return;
            }

            var npcTargets = new List<(ulong id, string name)>(nearbyNpcs.Count);
            var allEffectTags = new List<string>(48);
            var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < nearbyNpcs.Count; i++)
            {
                Dictionary<string, object> npc = nearbyNpcs[i];
                if (!TryReadNpcFromDictionary(npc, out ulong npcId, out string npcName))
                {
                    continue;
                }

                npcTargets.Add((npcId, npcName));

                if (npc.TryGetValue("power_names", out object powerObj))
                {
                    AppendTagsFromObject(powerObj, allEffectTags, seenTags);
                }
            }

            AppendCatalogEffectTags(allEffectTags, seenTags);
            for (int i = 0; i < s_AutomationSpecialEffectTags.Length; i++)
            {
                AddTagIfUnique(s_AutomationSpecialEffectTags[i], allEffectTags, seenTags);
            }

            if (allEffectTags.Count == 0)
            {
                for (int i = 0; i < s_AutomationFallbackEffectTags.Length; i++)
                {
                    AddTagIfUnique(s_AutomationFallbackEffectTags[i], allEffectTags, seenTags);
                }
            }

            if (npcTargets.Count == 0 || allEffectTags.Count == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Automated Probes",
                    $"Could not build probe steps (npcs={npcTargets.Count}, effectTags={allEffectTags.Count}).\n\n"
                        + "Check that NPCs have NpcDialogueProfile with PrefabPowers enabled, or that EffectCatalog has entries.",
                    "OK"
                );
                return;
            }

            List<(ulong networkId, ulong clientId, string label)> listenerTargets =
                DialogueMCPBridge.GetProbeListenerTargets();
            if (listenerTargets.Count == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Automated Probes",
                    "No spawned player listener targets found.\n\nMake sure host/client player objects are spawned before running probes.",
                    "OK"
                );
                return;
            }

            int narrativeStepCount = 0;
            if (includeNarrativePrelude)
            {
                // ── Seed player data for narrative hint testing ────────────────────
                LocalPlayerAuthService auth = LocalPlayerAuthService.Instance;
                if (auth != null)
                {
                    string testNpcId = npcTargets[0]
                        .name.Trim()
                        .ToLowerInvariant()
                        .Replace(" ", "_");
                    auth.SetPlayerClass("berserker");
                    auth.SetReputation(testNpcId, 90);
                    auth.AddInventoryTag("cursed_blade");
                    auth.AddInventoryTag("ancient_tome");
                    auth.SetQuestFlag("met_elder");
                    auth.SetQuestFlag("defeated_dragon");
                    auth.SetLastAction("defeated_boss_unscathed");
                    Debug.Log(
                        $"[DialogueMCP] Seeded test player data: class=berserker, reputation[{testNpcId}]=90, "
                            + "inventory=[cursed_blade,ancient_tome], flags=[met_elder,defeated_dragon], "
                            + "last_action=defeated_boss_unscathed"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[DialogueMCP] LocalPlayerAuthService not found — narrative hints will not be seeded."
                    );
                }
            }
            else
            {
                Debug.Log(
                    "[DialogueMCP] Training-mode automated probes: skipping narrative prelude and player-data seeding."
                );
            }

            List<string> plannedTags = BuildDiverseAutomationPlan(allEffectTags);
            if (plannedTags.Count > MaxAutomatedEffectsPerRun)
            {
                plannedTags = plannedTags.Take(MaxAutomatedEffectsPerRun).ToList();
            }

            var steps = new List<GameplayProbeAutomationRunner.ProbeStep>(
                plannedTags.Count + (includeNarrativePrelude ? 5 : 0)
            );

            if (includeNarrativePrelude)
            {
                // ── Prepend 5 narrative probe steps (one per new player-data field) ─
                (ulong narrativeListenerNetworkId, ulong _, string narrativeListenerLabel) =
                    listenerTargets[0];
                List<GameplayProbeAutomationRunner.ProbeStep> narrativeSteps =
                    BuildNarrativeProbeSteps(
                        npcTargets[0],
                        narrativeListenerNetworkId,
                        narrativeListenerLabel
                    );
                steps.AddRange(narrativeSteps);
                narrativeStepCount = narrativeSteps.Count;
            }

            // ── Effect validation steps ───────────────────────────────────────────
            for (int i = 0; i < plannedTags.Count; i++)
            {
                (ulong npcId, string npcName) = npcTargets[i % npcTargets.Count];
                (ulong listenerNetworkId, ulong __, string listenerLabel) = listenerTargets[
                    i % listenerTargets.Count
                ];
                string effectTag = plannedTags[i];
                string placementHint = SelectAutomationPlacementHint(effectTag, null);
                if (string.IsNullOrWhiteSpace(placementHint))
                {
                    continue;
                }

                steps.Add(
                    new GameplayProbeAutomationRunner.ProbeStep
                    {
                        NpcNetworkId = npcId,
                        NpcName = npcName,
                        EffectTag = effectTag,
                        PlacementHint = placementHint,
                        AttemptIndex = 0,
                        ListenerNetworkId = listenerNetworkId,
                        ListenerLabel = listenerLabel,
                        Directive = BuildAutomationDirectiveForEffect(
                            effectTag,
                            placementHint,
                            listenerLabel
                        ),
                    }
                );
            }

            s_AutomationRunner = new GameplayProbeAutomationRunner(steps);
            s_AutomationRunner.Start();
            string listenerSummary = string.Join(
                ", ",
                listenerTargets.Select(target =>
                    $"{target.label}(client={target.clientId}, net={target.networkId})"
                )
            );
            Debug.Log(
                $"[DialogueMCP] Built automated plan: {narrativeStepCount} narrative check(s) + {plannedTags.Count} effect tag(s) across {npcTargets.Count} NPC(s) and {listenerTargets.Count} listener target(s): {listenerSummary}"
            );
        }

        [UnityEditor.MenuItem(StopAutomatedProbesMenuPath)]
        public static void StopAutomatedProbes()
        {
            if (s_AutomationRunner == null || !s_AutomationRunner.IsRunning)
            {
                Debug.Log("[DialogueMCP] No automated probe run is active.");
                return;
            }

            s_AutomationRunner.Stop("stopped_by_user");
        }

        [UnityEditor.MenuItem(ClearAutomationPlacementMemoryMenuPath)]
        public static void ClearAutomationPlacementMemory()
        {
            s_AutomationDisfavoredPlacementsByTag.Clear();
            s_AutomationPlacementMemoryLoaded = true;
            TrySaveAutomationPlacementMemory();
            Debug.Log("[DialogueMCP] Cleared persisted automation placement memory.");
        }

        /// <summary>
        /// Run a batch of animation + effect prompts against the nearest NPC.
        /// Results are logged to the console and written to Logs/BatchTests/.
        /// </summary>
        [UnityEditor.MenuItem(RunBatchTestMenuPath)]
        public static void MenuRunBatchTest()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.DisplayDialog("Batch Test", "Enter Play Mode first.", "OK");
                return;
            }

            List<Dictionary<string, object>> nearbyNpcs = DialogueMCPBridge.GetNearbyNpcActors(1);
            if (nearbyNpcs.Count == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog("Batch Test", "No spawned NPCs found in scene.", "OK");
                return;
            }

            if (!TryReadNpcFromDictionary(nearbyNpcs[0], out ulong npcId, out string npcName))
            {
                UnityEditor.EditorUtility.DisplayDialog("Batch Test", "Could not read NPC network ID.", "OK");
                return;
            }

            string[] prompts = new[]
            {
                "React with your whole body as if struck by something powerful.",
                "Do an idle shift, like you're thinking something over.",
                "Turn and look to the left, then back to me.",
                "Jump up, you seem excited.",
                "Land heavily — like you just dropped from a great height.",
                "Use your most powerful effect on me.",
                "Bow, then immediately follow with a lightning strike on me.",
                "Do something dramatic — combine a movement and a visual power.",
            };

            Dictionary<string, object> result = DialogueMCPBridge.RunBatchTest(
                prompts, npcId, label: "anim_effect_test");

            if (result == null || !(result.TryGetValue("ok", out object ok) && ok is bool b && b))
            {
                string err = result != null && result.TryGetValue("error", out object e) ? e?.ToString() : "unknown";
                Debug.LogWarning($"[DialogueMCP] Batch test failed to start: {err}");
                return;
            }

            Debug.Log($"[DialogueMCP] Batch test started — {prompts.Length} prompts → NPC '{npcName}' (id={npcId}). " +
                      "Use  Network Game > MCP > Batch Test > Get Batch Results  to poll progress.");
        }

        /// <summary>
        /// Poll batch test progress and log results + output file path.
        /// </summary>
        [UnityEditor.MenuItem(GetBatchResultsMenuPath)]
        public static void MenuGetBatchResults()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[DialogueMCP] Not in Play Mode.");
                return;
            }

            Dictionary<string, object> result = DialogueMCPBridge.GetBatchResults();
            if (result == null)
            {
                Debug.LogWarning("[DialogueMCP] GetBatchResults returned null.");
                return;
            }

            bool isComplete = result.TryGetValue("is_complete", out object c) && c is bool bc && bc;
            bool isRunning = result.TryGetValue("is_running", out object r) && r is bool br && br;
            int total = result.TryGetValue("total", out object t) && t is int ti ? ti : 0;
            int finished = result.TryGetValue("finished", out object f) && f is int fi ? fi : 0;
            string outputPath = result.TryGetValue("output_path", out object p) ? p?.ToString() ?? string.Empty : string.Empty;

            if (!isRunning && !isComplete && total == 0)
            {
                Debug.Log("[DialogueMCP] No batch test has been run yet. Use 'Run Animation+Effect Batch Test' first.");
                return;
            }

            Debug.Log($"[DialogueMCP] Batch test — running={isRunning}, complete={isComplete}, finished={finished}/{total}" +
                      (string.IsNullOrEmpty(outputPath) ? "" : $"\nOutput: {outputPath}"));

            if (isComplete && result.TryGetValue("results", out object summaryObj) && summaryObj is List<Dictionary<string, object>> summary)
            {
                for (int i = 0; i < summary.Count; i++)
                {
                    var entry = summary[i];
                    string prompt = entry.TryGetValue("prompt", out object pr) ? pr?.ToString() : "?";
                    string status = entry.TryGetValue("status", out object st) ? st?.ToString() : "?";
                    string speech = entry.TryGetValue("speech", out object sp) ? sp?.ToString() : string.Empty;
                    int actionCount = entry.TryGetValue("action_count", out object ac) && ac is int aci ? aci : 0;
                    float modelMs = entry.TryGetValue("model_ms", out object mm) && mm is double mmd ? (float)mmd : 0f;
                    string err = entry.TryGetValue("error", out object er) ? er?.ToString() : string.Empty;

                    string line = $"  [{i}] {status} | actions={actionCount} | {modelMs:F0}ms | prompt='{prompt}' | speech='{speech}'";
                    if (!string.IsNullOrEmpty(err)) line += $" | error={err}";
                    Debug.Log($"[DialogueMCP] {line}");
                }
            }
        }

        private static bool TryReadNpcFromDictionary(
            Dictionary<string, object> data,
            out ulong npcId,
            out string npcName
        )
        {
            npcId = 0;
            npcName = "NPC";
            if (data == null)
            {
                return false;
            }

            if (!data.TryGetValue("network_id", out object idObj) || idObj == null)
            {
                return false;
            }

            try
            {
                npcId = Convert.ToUInt64(idObj);
            }
            catch
            {
                return false;
            }

            if (data.TryGetValue("name", out object nameObj) && nameObj != null)
            {
                string raw = nameObj.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    npcName = raw.Trim();
                }
            }

            return npcId != 0;
        }

        private static void AppendCatalogEffectTags(List<string> tags, HashSet<string> seen)
        {
            try
            {
                Effects.EffectCatalog catalog = Effects.EffectCatalog.Load();
                if (catalog == null || catalog.allEffects == null)
                {
                    return;
                }

                for (int i = 0; i < catalog.allEffects.Count; i++)
                {
                    Effects.EffectDefinition effect = catalog.allEffects[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    AddTagIfUnique(effect.effectTag, tags, seen);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[DialogueMCP] Could not read EffectCatalog for automation: {ex.Message}"
                );
            }
        }

        private static void AppendTagsFromObject(
            object source,
            List<string> tags,
            HashSet<string> seen
        )
        {
            if (source == null)
            {
                return;
            }

            if (source is string[] arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    AddTagIfUnique(arr[i], tags, seen);
                }
                return;
            }

            if (source is IEnumerable<string> enumerable)
            {
                foreach (string item in enumerable)
                {
                    AddTagIfUnique(item, tags, seen);
                }
                return;
            }

            if (source is IEnumerable<object> objEnumerable)
            {
                foreach (object item in objEnumerable)
                {
                    AddTagIfUnique(item?.ToString(), tags, seen);
                }
                return;
            }

            AddTagIfUnique(source.ToString(), tags, seen);
        }

        private static void AddTagIfUnique(string rawTag, List<string> tags, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return;
            }

            string tag = rawTag.Trim();
            string dedupKey = CanonicalizeAutomationTag(tag);
            if (string.IsNullOrWhiteSpace(dedupKey))
            {
                return;
            }

            if (seen.Add(dedupKey))
            {
                tags.Add(tag);
            }
        }

        private static string CanonicalizeAutomationTag(string rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return string.Empty;
            }

            string tag = rawTag.Trim();
            string lower = tag.ToLowerInvariant();

            if (lower.Contains("dissolve"))
            {
                return "dissolve";
            }

            if (lower.Contains("respawn") || lower.Contains("revive") || lower.Contains("reappear"))
            {
                return "respawn";
            }

            var compact = new StringBuilder(tag.Length);
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                if (char.IsLetterOrDigit(c))
                {
                    compact.Append(c);
                }
            }

            return compact.Length > 0 ? compact.ToString() : lower;
        }

        private enum AutomationEffectCategory
        {
            Projectile = 0,
            Fire = 1,
            Weather = 2,
            Area = 3,
            Ambient = 4,
            Utility = 5,
            Special = 6,
        }

        private static List<string> BuildDiverseAutomationPlan(List<string> tags)
        {
            var buckets = new Dictionary<AutomationEffectCategory, Queue<string>>();
            foreach (
                AutomationEffectCategory category in Enum.GetValues(
                    typeof(AutomationEffectCategory)
                )
            )
            {
                buckets[category] = new Queue<string>();
            }

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                AutomationEffectCategory category = ClassifyAutomationCategory(tag);
                buckets[category].Enqueue(tag);
            }

            var ordered = new List<string>(tags.Count);
            AutomationEffectCategory[] passOrder =
            {
                AutomationEffectCategory.Projectile,
                AutomationEffectCategory.Fire,
                AutomationEffectCategory.Weather,
                AutomationEffectCategory.Area,
                AutomationEffectCategory.Ambient,
                AutomationEffectCategory.Utility,
                AutomationEffectCategory.Special,
            };

            bool addedAny;
            do
            {
                addedAny = false;
                for (int i = 0; i < passOrder.Length; i++)
                {
                    Queue<string> bucket = buckets[passOrder[i]];
                    if (bucket.Count == 0)
                    {
                        continue;
                    }

                    ordered.Add(bucket.Dequeue());
                    addedAny = true;
                }
            } while (addedAny);

            return ordered;
        }

        private static AutomationEffectCategory ClassifyAutomationCategory(string effectTag)
        {
            string lower = (effectTag ?? string.Empty).ToLowerInvariant();
            if (lower == "dissolve" || lower == "respawn")
            {
                return AutomationEffectCategory.Special;
            }

            if (
                lower.Contains("projectile")
                || lower.Contains("missile")
                || lower.Contains("rocket")
                || lower.Contains("arrow")
                || lower.Contains("bolt")
                || lower.Contains("lance")
                || lower.Contains("fireball")
            )
            {
                return AutomationEffectCategory.Projectile;
            }

            if (
                lower.Contains("fire")
                || lower.Contains("flame")
                || lower.Contains("burn")
                || lower.Contains("heat")
                || lower.Contains("wildfire")
            )
            {
                return AutomationEffectCategory.Fire;
            }

            if (
                lower.Contains("ice")
                || lower.Contains("frost")
                || lower.Contains("freeze")
                || lower.Contains("storm")
                || lower.Contains("rain")
                || lower.Contains("water")
                || lower.Contains("thunder")
                || lower.Contains("lightning")
            )
            {
                return AutomationEffectCategory.Weather;
            }

            if (
                lower.Contains("explosion")
                || lower.Contains("blast")
                || lower.Contains("shockwave")
                || lower.Contains("impact")
                || lower.Contains("shatter")
                || lower.Contains("splash")
            )
            {
                return AutomationEffectCategory.Area;
            }

            if (
                lower.Contains("fog")
                || lower.Contains("smoke")
                || lower.Contains("steam")
                || lower.Contains("dust")
                || lower.Contains("motes")
                || lower.Contains("sand")
                || lower.Contains("flies")
            )
            {
                return AutomationEffectCategory.Ambient;
            }

            return AutomationEffectCategory.Utility;
        }

        private static string BuildAutomationDirectiveForEffect(
            string effectTag,
            string placementHint,
            string listenerLabel = null
        )
        {
            string tag = string.IsNullOrWhiteSpace(effectTag) ? "FireBall" : effectTag.Trim();
            bool special =
                tag.Equals("dissolve", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("respawn", StringComparison.OrdinalIgnoreCase);
            string placement = string.IsNullOrWhiteSpace(placementHint)
                ? ResolveAutomationPlacementHint(tag, special)
                : placementHint;
            string targetHint = ResolveAutomationTargetHint(placement, special);
            string extras = placement switch
            {
                "attached" => "Collision: allow_overlap | GroundSnap: false",
                "projectile" => "Collision: strict | RequireLineOfSight: true",
                "area" => "Collision: strict | GroundSnap: true",
                _ => "Collision: relaxed | GroundSnap: true",
            };

            string caution = special
                ? string.Empty
                : "Use only the requested effect tag for this step. Do NOT use dissolve or respawn unless explicitly requested.";
            string listenerInstruction = string.IsNullOrWhiteSpace(listenerLabel)
                ? string.Empty
                : $" Address the current listener player ({listenerLabel}) directly.";
            string spatialGuide = placement switch
            {
                "attached" =>
                    " Think in visible scene results only: place the effect on the target body so it follows the target. Match the target's visible body size.",
                "area" =>
                    " Think in visible scene results only: place the effect on the ground close to the target.",
                "projectile" =>
                    " Think in visible scene results only: start the effect away from the target and have it travel toward the target.",
                _ =>
                    " Think in visible scene results only: keep the effect in nearby world space around the target.",
            };

            return string.Concat(
                    "Effect validation step. Respond in character with one short sentence, then append exactly one tag: [EFFECT: ",
                    tag,
                    " | Target: ",
                    targetHint,
                    " | Placement: ",
                    placement,
                    " | Scale: 1.0 | Duration: 3.5 | ",
                    extras,
                    "].",
                    spatialGuide,
                    listenerInstruction,
                    " Output only the final sentence and tag. No analysis, no extra lines.",
                    " Do not reason about Unity internals such as GameObjects, meshes, materials, shaders, or animations.",
                    " The effect tag name must match exactly and you must not emit any additional [EFFECT:] tags. ",
                    caution
                )
                .Trim();
        }

        private static List<GameplayProbeAutomationRunner.ProbeStep> BuildNarrativeProbeSteps(
            (ulong id, string name) npcTarget,
            ulong listenerNetworkId,
            string listenerLabel
        )
        {
            // Each tuple: (playerDataField, acceptedKeywordsInResponse, openDialoguePrompt)
            // Keywords are deliberately broad so a paraphrase still counts as a PASS.
            var configs = new (string field, string[] keywords, string prompt)[]
            {
                (
                    "class",
                    new[] { "berserker", "warrior", "fighter" },
                    "Greet me, warrior. What do you think of someone with the fighting spirit of a berserker?"
                ),
                (
                    "reputation",
                    new[] { "champion", "hero", "renowned" },
                    "Tell me honestly — do you know who I am and how much I've done for you?"
                ),
                (
                    "inventory_tags",
                    new[] { "cursed", "blade", "tome" },
                    "I carry something unusual on my person. Do you notice what I have in my possession?"
                ),
                (
                    "quest_flags",
                    new[] { "elder", "keep", "journey" },
                    "I recently met the elder of the northern keep. Have you heard of my journey?"
                ),
                (
                    "last_action",
                    new[] { "boss", "victory", "accomplishment", "defeated" },
                    "Word travels fast in these lands. Have you heard about my recent accomplishment in the keep?"
                ),
            };

            var steps = new List<GameplayProbeAutomationRunner.ProbeStep>(configs.Length);
            foreach (var (field, keywords, prompt) in configs)
            {
                steps.Add(
                    new GameplayProbeAutomationRunner.ProbeStep
                    {
                        NpcNetworkId = npcTarget.id,
                        NpcName = npcTarget.name,
                        ListenerNetworkId = listenerNetworkId,
                        ListenerLabel = listenerLabel,
                        EffectTag = string.Empty,
                        PlacementHint = string.Empty,
                        AttemptIndex = 0,
                        Directive = prompt,
                        IsNarrativeCheck = true,
                        NarrativeField = field,
                        NarrativeKeywords = keywords,
                    }
                );
            }

            return steps;
        }

        private static string ResolveAutomationTargetHint(string placementHint, bool special)
        {
            if (special)
            {
                return "Player";
            }

            return placementHint switch
            {
                "projectile" => "Player",
                "attached" => "Player",
                "area" => "Ground",
                "ambient" => "World",
                _ => "Player",
            };
        }

        private static string SelectAutomationPlacementHint(
            string effectTag,
            string excludePlacement
        )
        {
            EnsureAutomationPlacementMemoryLoaded();
            bool special =
                effectTag.Equals("dissolve", StringComparison.OrdinalIgnoreCase)
                || effectTag.Equals("respawn", StringComparison.OrdinalIgnoreCase);
            string defaultPlacement = ResolveAutomationPlacementHint(effectTag, special);
            var candidates = new List<string>(5)
            {
                defaultPlacement,
                "attached",
                "area",
                "projectile",
                "ambient",
            };

            string canonicalTag = CanonicalizeAutomationTag(effectTag);
            s_AutomationDisfavoredPlacementsByTag.TryGetValue(
                canonicalTag,
                out HashSet<string> disfavored
            );
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (
                    !string.IsNullOrWhiteSpace(excludePlacement)
                    && candidate.Equals(excludePlacement, StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                if (disfavored != null && disfavored.Contains(candidate))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsNegativePlacementOutcome(string outcome)
        {
            if (string.IsNullOrWhiteSpace(outcome))
            {
                return false;
            }

            string normalized = outcome.Trim().ToLowerInvariant();
            return normalized == "wrong_target"
                || normalized == "wrong_placement"
                || normalized == "wrong_mesh_fit"
                || normalized == "not_visible";
        }

        private static void RegisterAutomationPlacementFeedback(
            string effectTag,
            string placementHint,
            string outcome
        )
        {
            if (string.IsNullOrWhiteSpace(effectTag) || string.IsNullOrWhiteSpace(placementHint))
            {
                return;
            }

            EnsureAutomationPlacementMemoryLoaded();
            string canonicalTag = CanonicalizeAutomationTag(effectTag);
            if (
                !s_AutomationDisfavoredPlacementsByTag.TryGetValue(
                    canonicalTag,
                    out HashSet<string> set
                )
            )
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                s_AutomationDisfavoredPlacementsByTag[canonicalTag] = set;
            }

            if (IsNegativePlacementOutcome(outcome))
            {
                if (set.Add(placementHint.Trim()))
                {
                    TrySaveAutomationPlacementMemory();
                }
                return;
            }

            if (
                !string.IsNullOrWhiteSpace(outcome)
                && outcome.Trim().Equals("looks_correct", StringComparison.OrdinalIgnoreCase)
            )
            {
                if (set.Remove(placementHint.Trim()))
                {
                    TrySaveAutomationPlacementMemory();
                }
            }
        }

        private static void EnsureAutomationPlacementMemoryLoaded()
        {
            if (s_AutomationPlacementMemoryLoaded)
            {
                return;
            }

            s_AutomationPlacementMemoryLoaded = true;
            string path = ResolveAutomationPlacementMemoryPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var data = JsonUtility.FromJson<AutomationPlacementMemoryFile>(json);
                if (data?.entries == null)
                {
                    return;
                }

                s_AutomationDisfavoredPlacementsByTag.Clear();
                for (int i = 0; i < data.entries.Count; i++)
                {
                    AutomationPlacementMemoryEntry entry = data.entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.effect_tag))
                    {
                        continue;
                    }

                    string canonicalTag = CanonicalizeAutomationTag(entry.effect_tag);
                    if (string.IsNullOrWhiteSpace(canonicalTag))
                    {
                        continue;
                    }

                    if (
                        !s_AutomationDisfavoredPlacementsByTag.TryGetValue(
                            canonicalTag,
                            out HashSet<string> set
                        )
                    )
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        s_AutomationDisfavoredPlacementsByTag[canonicalTag] = set;
                    }

                    if (entry.disfavored_placements == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < entry.disfavored_placements.Count; j++)
                    {
                        string placement = entry.disfavored_placements[j];
                        if (string.IsNullOrWhiteSpace(placement))
                        {
                            continue;
                        }

                        set.Add(placement.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[DialogueMCP] Failed to load automation placement memory: {ex.Message}"
                );
            }
        }

        private static void TrySaveAutomationPlacementMemory()
        {
            try
            {
                string path = ResolveAutomationPlacementMemoryPath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = new AutomationPlacementMemoryFile
                {
                    schema_version = 1,
                    updated_utc = DateTime.UtcNow.ToString("o"),
                };

                foreach (var kvp in s_AutomationDisfavoredPlacementsByTag)
                {
                    if (
                        string.IsNullOrWhiteSpace(kvp.Key)
                        || kvp.Value == null
                        || kvp.Value.Count == 0
                    )
                    {
                        continue;
                    }

                    var entry = new AutomationPlacementMemoryEntry { effect_tag = kvp.Key };
                    foreach (string placement in kvp.Value)
                    {
                        if (string.IsNullOrWhiteSpace(placement))
                        {
                            continue;
                        }

                        entry.disfavored_placements.Add(placement.Trim());
                    }

                    if (entry.disfavored_placements.Count > 0)
                    {
                        data.entries.Add(entry);
                    }
                }

                data.entries.Sort(
                    (a, b) =>
                        string.Compare(
                            a.effect_tag,
                            b.effect_tag,
                            StringComparison.OrdinalIgnoreCase
                        )
                );
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[DialogueMCP] Failed to save automation placement memory: {ex.Message}"
                );
            }
        }

        private static string ResolveAutomationPlacementMemoryPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(
                Path.Combine(projectRoot, AutomationPlacementMemoryRelativePath)
            );
        }

        private static bool TagsMatch(string expectedTag, string appliedTag)
        {
            string a = CanonicalizeAutomationTag(expectedTag);
            string b = CanonicalizeAutomationTag(appliedTag);
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveAutomationPlacementHint(string effectTag, bool special)
        {
            if (special)
            {
                return "attached";
            }

            string lower = (effectTag ?? string.Empty).ToLowerInvariant();
            if (
                lower.Contains("projectile")
                || lower.Contains("lance")
                || lower.Contains("bolt")
                || lower.Contains("arrow")
                || lower.Contains("fireball")
                || lower.Contains("missile")
            )
            {
                return "projectile";
            }

            if (
                lower.Contains("explosion")
                || lower.Contains("blast")
                || lower.Contains("shockwave")
                || lower.Contains("storm")
                || lower.Contains("rain")
                || lower.Contains("nova")
            )
            {
                return "area";
            }

            if (
                lower.Contains("shield")
                || lower.Contains("aura")
                || lower.Contains("dissolve")
                || lower.Contains("respawn")
            )
            {
                return "attached";
            }

            return "attached";
        }

        private static void EnsureGameplayProbeSubscription()
        {
            if (s_GameplayProbeSubscribed)
            {
                return;
            }

            NetworkDialogueService.OnDialogueResponse += HandleGameplayProbeResponse;
            s_GameplayProbeSubscribed = true;
        }

        private static void HandleGameplayProbeResponse(
            NetworkDialogueService.DialogueResponse response
        )
        {
            int clientRequestId = response.Request.ClientRequestId;
            if (s_AutomationRunner != null && s_AutomationRunner.HandleResponse(response))
            {
                return;
            }

            if (!s_GameplayProbeRequestIds.Remove(clientRequestId))
            {
                return;
            }

            string speaker = response.Request.SpeakerNetworkId.ToString();
            string status = response.Status.ToString();
            string text = string.IsNullOrWhiteSpace(response.ResponseText)
                ? "(empty response)"
                : response.ResponseText.Trim();
            string error = string.IsNullOrWhiteSpace(response.Error)
                ? string.Empty
                : response.Error.Trim();

            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(
                    $"[DialogueMCP] Probe response request={clientRequestId} speaker={speaker} status={status} error={error}\n{text}"
                );
                return;
            }

            Debug.Log(
                $"[DialogueMCP] Probe response request={clientRequestId} speaker={speaker} status={status}\n{text}"
            );
        }

        private sealed class GameplayProbeAutomationRunner
        {
            internal struct ProbeStep
            {
                public ulong NpcNetworkId;
                public string NpcName;
                public ulong ListenerNetworkId;
                public string ListenerLabel;
                public string EffectTag;
                public string PlacementHint;
                public int AttemptIndex;
                public string Directive;

                /// <summary>When true this step validates a narrative hint field, not an effect placement.</summary>
                public bool IsNarrativeCheck;

                /// <summary>Which player-data field is under test: "class" | "reputation" | "inventory_tags" | "quest_flags" | "last_action"</summary>
                public string NarrativeField;

                /// <summary>Accepted substrings anywhere in the LLM response (case-insensitive) for a PASS verdict.</summary>
                public string[] NarrativeKeywords;
            }

            private const double LocalStepResponseTimeoutSeconds = 55d;
            private const double RemoteStepResponseTimeoutSeconds = 480d;
            private const double ResponseSettleWithoutPromptSeconds = 2.5d;
            private const double InterStepDelaySeconds = 0.35d;
            private const double TransientRetryDelaySeconds = 4.0d;
            private const double CancelDrainGraceSeconds = 5.0d;
            private const double SendBusyRetryDelaySeconds = 2.0d;

            private sealed class NarrativeCheckResult
            {
                public string Field;
                public string Keywords;
                public bool Passed;
                public string Excerpt;
            }

            private readonly List<ProbeStep> m_Steps;
            private readonly List<NarrativeCheckResult> m_NarrativeResults =
                new List<NarrativeCheckResult>();
            private int m_StepIndex;
            private int m_ActiveRequestId;
            private bool m_WaitingForResponse;
            private bool m_WaitingForFeedback;
            private bool m_SawBlockingPrompt;
            private bool m_WaitingForCancelDrain;
            private double m_RequestSentAt;
            private double m_ResponseAt;
            private double m_NextStepAt;
            private double m_CancelIssuedAt;
            private string m_LastFeedbackOutcome;

            public bool IsRunning { get; private set; }

            public GameplayProbeAutomationRunner(List<ProbeStep> steps)
            {
                m_Steps = steps ?? new List<ProbeStep>();
                m_StepIndex = 0;
                m_ActiveRequestId = -1;
                m_LastFeedbackOutcome = string.Empty;
            }

            public void Start()
            {
                if (IsRunning)
                {
                    return;
                }

                DialogueEffectFeedbackPrompt prompt =
                    DialogueEffectFeedbackPrompt.EnsureForAutomation();
                if (prompt != null)
                {
                    prompt.ForceEnablePrompt(true);
                }
                else
                {
                    Debug.LogWarning(
                        "[DialogueMCP] Feedback prompt instance could not be created; probes may auto-advance with no_feedback_prompt."
                    );
                }

                IsRunning = true;
                m_NextStepAt = UnityEditor.EditorApplication.timeSinceStartup;
                UnityEditor.EditorApplication.update += Tick;
                DialogueEffectFeedbackPrompt.OnFeedbackSubmitted += HandleFeedbackSubmitted;
                Debug.Log(
                    $"[DialogueMCP] Automated probe run started with {m_Steps.Count} step(s). Submit the feedback popup each step to advance."
                );
            }

            public void Stop(string reason)
            {
                if (!IsRunning)
                {
                    return;
                }

                IsRunning = false;
                m_WaitingForResponse = false;
                m_WaitingForFeedback = false;
                m_WaitingForCancelDrain = false;
                m_ActiveRequestId = -1;
                UnityEditor.EditorApplication.update -= Tick;
                DialogueEffectFeedbackPrompt.OnFeedbackSubmitted -= HandleFeedbackSubmitted;
                Debug.Log($"[DialogueMCP] Automated probe run stopped ({reason}).");

                if (m_NarrativeResults.Count > 0)
                {
                    int narrativePass = m_NarrativeResults.Count(r => r.Passed);
                    int narrativeFail = m_NarrativeResults.Count - narrativePass;
                    Debug.Log(
                        $"[DialogueMCP] ── Narrative hint summary: {narrativePass} PASS / {narrativeFail} FAIL ──"
                    );
                    foreach (NarrativeCheckResult r in m_NarrativeResults)
                    {
                        string marker = r.Passed ? "✅" : "❌";
                        Debug.Log(
                            $"  {marker} [{r.Field}] keywords='{r.Keywords}'\n     {r.Excerpt}"
                        );
                    }
                }
            }

            public bool HandleResponse(NetworkDialogueService.DialogueResponse response)
            {
                if (!IsRunning || !m_WaitingForResponse)
                {
                    return false;
                }

                int clientRequestId = response.Request.ClientRequestId;
                if (clientRequestId != m_ActiveRequestId)
                {
                    return false;
                }

                m_WaitingForResponse = false;

                // ── Transient rejection (NPC busy) → retry the same step ──────────
                string transientError = string.IsNullOrWhiteSpace(response.Error)
                    ? string.Empty
                    : response.Error.Trim();
                bool isTransientRejection =
                    response.Status == NetworkDialogueService.DialogueStatus.Failed
                    && (
                        transientError == "conversation_in_flight"
                        || transientError == "repeat_delay"
                        || transientError == "request_rejected"
                    );
                if (isTransientRejection)
                {
                    m_WaitingForFeedback = false;
                    m_NextStepAt =
                        UnityEditor.EditorApplication.timeSinceStartup + TransientRetryDelaySeconds;
                    Debug.Log(
                        $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} transient rejection"
                            + $" ({transientError}) for {m_Steps[m_StepIndex].NpcName} - retrying in {TransientRetryDelaySeconds:F1}s."
                    );
                    return true;
                }

                m_WaitingForFeedback = true;
                m_ResponseAt = UnityEditor.EditorApplication.timeSinceStartup;
                m_SawBlockingPrompt = DialogueEffectFeedbackPrompt.IsBlockingPromptActive;
                m_LastFeedbackOutcome = string.Empty;

                ProbeStep currentStep = m_Steps[m_StepIndex];
                string status = response.Status.ToString();
                string error = string.IsNullOrWhiteSpace(response.Error)
                    ? string.Empty
                    : response.Error.Trim();
                string text = string.IsNullOrWhiteSpace(response.ResponseText)
                    ? "(empty response)"
                    : response.ResponseText.Trim();

                if (string.IsNullOrWhiteSpace(error))
                {
                    Debug.Log(
                        $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} response request={clientRequestId} status={status}\n{text}"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} response request={clientRequestId} status={status} error={error}\n{text}"
                    );
                }

                // Narrative check: validate that at least one accepted keyword appears in the response
                if (
                    currentStep.IsNarrativeCheck
                    && currentStep.NarrativeKeywords != null
                    && currentStep.NarrativeKeywords.Length > 0
                )
                {
                    string keywordSummary = string.Join("|", currentStep.NarrativeKeywords);
                    bool passed = false;
                    for (int i = 0; i < currentStep.NarrativeKeywords.Length; i++)
                    {
                        string narrativeKeyword = currentStep.NarrativeKeywords[i];
                        if (
                            !string.IsNullOrWhiteSpace(narrativeKeyword)
                            && text.IndexOf(narrativeKeyword, StringComparison.OrdinalIgnoreCase)
                                >= 0
                        )
                        {
                            passed = true;
                            break;
                        }
                    }

                    string excerpt = text.Length > 140 ? text.Substring(0, 140) + "…" : text;
                    m_NarrativeResults.Add(
                        new NarrativeCheckResult
                        {
                            Field = currentStep.NarrativeField,
                            Keywords = keywordSummary,
                            Passed = passed,
                            Excerpt = excerpt,
                        }
                    );
                    string verdict = passed ? "✅ PASS" : "❌ FAIL (keywords not found in response)";
                    Debug.Log(
                        $"[DialogueMCP] Narrative [{currentStep.NarrativeField}] keywords='{keywordSummary}' → {verdict}"
                    );
                }

                return true;
            }

            private void Tick()
            {
                if (!IsRunning)
                {
                    return;
                }

                if (!Application.isPlaying)
                {
                    Stop("play_mode_exited");
                    return;
                }

                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (m_WaitingForResponse)
                {
                    if (TryRecoverMissedResponseViaPolling())
                    {
                        return;
                    }

                    if (now - m_RequestSentAt > GetCurrentStepResponseTimeoutSeconds())
                    {
                        CancelTimedOutRequestAndWaitForDrain(now);
                    }
                    return;
                }

                if (m_WaitingForCancelDrain)
                {
                    TickCancelDrainWait(now);
                    return;
                }

                if (m_WaitingForFeedback)
                {
                    TickFeedbackWait(now);
                    return;
                }

                if (now < m_NextStepAt)
                {
                    return;
                }

                if (DialogueEffectFeedbackPrompt.IsBlockingPromptActive)
                {
                    return;
                }

                if (m_StepIndex >= m_Steps.Count)
                {
                    Stop("completed");
                    return;
                }

                SendCurrentStep();
            }

            private void TickFeedbackWait(double now)
            {
                bool isBlocking = DialogueEffectFeedbackPrompt.IsBlockingPromptActive;
                if (isBlocking)
                {
                    m_SawBlockingPrompt = true;
                    return;
                }

                if (m_SawBlockingPrompt)
                {
                    string outcome = string.IsNullOrWhiteSpace(m_LastFeedbackOutcome)
                        ? "feedback_submitted"
                        : "feedback_" + m_LastFeedbackOutcome;
                    AdvanceStep(outcome);
                    return;
                }

                if (now - m_ResponseAt > ResponseSettleWithoutPromptSeconds)
                {
                    AdvanceStep("no_feedback_prompt");
                    return;
                }
            }

            private void SendCurrentStep()
            {
                ProbeStep step = m_Steps[m_StepIndex];
                Dictionary<string, object> result = DialogueMCPBridge.SendGameplayProbeToNpc(
                    step.NpcNetworkId,
                    step.Directive,
                    step.ListenerNetworkId
                );

                bool ok =
                    result != null
                    && result.TryGetValue("ok", out object okValue)
                    && okValue is bool boolOk
                    && boolOk;
                if (!ok)
                {
                    string error =
                        result != null && result.TryGetValue("error", out object errorValue)
                            ? errorValue?.ToString() ?? "unknown error"
                            : "unknown error";
                    bool transientBusySendError =
                        string.Equals(
                            error,
                            "conversation_in_flight",
                            StringComparison.OrdinalIgnoreCase
                        )
                        || string.Equals(error, "repeat_delay", StringComparison.OrdinalIgnoreCase);
                    if (transientBusySendError)
                    {
                        m_NextStepAt =
                            UnityEditor.EditorApplication.timeSinceStartup
                            + SendBusyRetryDelaySeconds;
                        Debug.LogWarning(
                            $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} send transient busy for {step.NpcName}: {error}. Retrying in {SendBusyRetryDelaySeconds:F1}s."
                        );
                        return;
                    }

                    Debug.LogWarning(
                        $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} send failed for {step.NpcName}: {error}"
                    );
                    AdvanceStep("send_failed");
                    return;
                }

                if (
                    !result.TryGetValue("request_id", out object requestObj)
                    || !TryConvertToInt(requestObj, out int requestId)
                )
                {
                    Debug.LogWarning(
                        $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} missing request_id for {step.NpcName}."
                    );
                    AdvanceStep("missing_request_id");
                    return;
                }

                m_ActiveRequestId = requestId;
                m_WaitingForResponse = true;
                m_RequestSentAt = UnityEditor.EditorApplication.timeSinceStartup;
                Debug.Log(
                    $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} -> request={requestId} npc={step.NpcName} listener={step.ListenerLabel}({step.ListenerNetworkId}) tag={step.EffectTag} placement={step.PlacementHint} attempt={step.AttemptIndex}\nDirective: {step.Directive}"
                );
            }

            private void HandleFeedbackSubmitted(
                DialogueEffectFeedbackPrompt.FeedbackSubmission submission
            )
            {
                if (!IsRunning || !m_WaitingForFeedback)
                {
                    return;
                }

                ProbeStep step = m_Steps[m_StepIndex];
                // Narrative steps have no placement logic — feedback popup is not expected for them
                if (step.IsNarrativeCheck)
                {
                    return;
                }

                m_SawBlockingPrompt = true;
                m_LastFeedbackOutcome = submission.Outcome ?? string.Empty;

                RegisterAutomationPlacementFeedback(
                    step.EffectTag,
                    step.PlacementHint,
                    m_LastFeedbackOutcome
                );

                bool effectMismatch = !TagsMatch(step.EffectTag, submission.Effect.EffectName);
                bool shouldRetry =
                    step.AttemptIndex < 1
                    && (effectMismatch || IsNegativePlacementOutcome(m_LastFeedbackOutcome));
                if (!shouldRetry)
                {
                    return;
                }

                string retryPlacement = SelectAutomationPlacementHint(
                    step.EffectTag,
                    step.PlacementHint
                );
                if (
                    string.IsNullOrWhiteSpace(retryPlacement)
                    || retryPlacement.Equals(step.PlacementHint, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return;
                }

                ProbeStep retryStep = new ProbeStep
                {
                    NpcNetworkId = step.NpcNetworkId,
                    NpcName = step.NpcName,
                    ListenerNetworkId = step.ListenerNetworkId,
                    ListenerLabel = step.ListenerLabel,
                    EffectTag = step.EffectTag,
                    PlacementHint = retryPlacement,
                    AttemptIndex = step.AttemptIndex + 1,
                    Directive = BuildAutomationDirectiveForEffect(
                        step.EffectTag,
                        retryPlacement,
                        step.ListenerLabel
                    ),
                };
                m_Steps.Add(retryStep);
                Debug.Log(
                    $"[DialogueMCP] Queued retry for tag={step.EffectTag} with alternative placement={retryPlacement} (outcome={m_LastFeedbackOutcome}, mismatch={effectMismatch})."
                );
            }

            private void AdvanceStep(string reason)
            {
                ProbeStep step = m_Steps[m_StepIndex];
                Debug.Log(
                    $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} finished for npc={step.NpcName} listener={step.ListenerLabel}({step.ListenerNetworkId}) tag={step.EffectTag} placement={step.PlacementHint} attempt={step.AttemptIndex} ({reason})."
                );

                m_StepIndex++;
                m_ActiveRequestId = -1;
                m_WaitingForResponse = false;
                m_WaitingForFeedback = false;
                m_WaitingForCancelDrain = false;
                m_SawBlockingPrompt = false;
                m_LastFeedbackOutcome = string.Empty;
                m_NextStepAt =
                    UnityEditor.EditorApplication.timeSinceStartup + InterStepDelaySeconds;

                if (m_StepIndex >= m_Steps.Count)
                {
                    Stop("completed");
                }
            }

            private double GetCurrentStepResponseTimeoutSeconds()
            {
                NetworkDialogueService service = NetworkDialogueService.Instance;
                if (service == null)
                {
                    return LocalStepResponseTimeoutSeconds;
                }

                bool isRemote = service.UsesRemoteInference;
                return isRemote
                    ? RemoteStepResponseTimeoutSeconds
                    : LocalStepResponseTimeoutSeconds;
            }

            private void CancelTimedOutRequestAndWaitForDrain(double now)
            {
                if (m_ActiveRequestId <= 0)
                {
                    AdvanceStep("response_timeout");
                    return;
                }

                NetworkDialogueService service = NetworkDialogueService.Instance;
                if (service != null)
                {
                    service.CancelRequest(m_ActiveRequestId);
                }

                m_WaitingForResponse = false;
                m_WaitingForFeedback = false;
                m_WaitingForCancelDrain = true;
                m_CancelIssuedAt = now;

                Debug.LogWarning(
                    $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} request={m_ActiveRequestId} exceeded timeout ({GetCurrentStepResponseTimeoutSeconds():F0}s). Cancelling and waiting for dialogue service to drain."
                );
            }

            private void TickCancelDrainWait(double now)
            {
                NetworkDialogueService service = NetworkDialogueService.Instance;
                bool canAdvance = service == null || service.ActiveRequestCount <= 0;
                bool drainTimedOut = now - m_CancelIssuedAt >= CancelDrainGraceSeconds;
                if (!canAdvance && !drainTimedOut)
                {
                    return;
                }

                if (!canAdvance && service != null)
                {
                    Debug.LogWarning(
                        $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} cancel drain grace expired after {CancelDrainGraceSeconds:F1}s (active={service.ActiveRequestCount}, pending={service.PendingRequestCount}). Advancing anyway."
                    );
                }

                AdvanceStep(
                    drainTimedOut
                        ? "response_timeout_cancelled_drain_timeout"
                        : "response_timeout_cancelled"
                );
            }

            private bool TryRecoverMissedResponseViaPolling()
            {
                if (m_ActiveRequestId <= 0)
                {
                    return false;
                }

                NetworkDialogueService service = NetworkDialogueService.Instance;
                if (service == null)
                {
                    return false;
                }

                if (
                    service.IsClientRequestInFlight(
                        m_ActiveRequestId,
                        NetworkManager.Singleton != null
                            ? NetworkManager.Singleton.LocalClientId
                            : ulong.MaxValue
                    )
                )
                {
                    return false;
                }

                if (
                    !service.TryGetTerminalResponseByClientRequestId(
                        m_ActiveRequestId,
                        out var response,
                        NetworkManager.Singleton != null
                            ? NetworkManager.Singleton.LocalClientId
                            : ulong.MaxValue
                    )
                )
                {
                    return false;
                }

                Debug.LogWarning(
                    $"[DialogueMCP] Auto step {m_StepIndex + 1}/{m_Steps.Count} recovered missed response callback for clientRequest={m_ActiveRequestId} via polling."
                );
                HandleResponse(response);
                return true;
            }

            private static bool TryConvertToInt(object value, out int result)
            {
                result = 0;
                if (value == null)
                {
                    return false;
                }

                if (value is int intValue)
                {
                    result = intValue;
                    return true;
                }

                if (value is long longValue)
                {
                    result = (int)longValue;
                    return true;
                }

                if (value is short shortValue)
                {
                    result = shortValue;
                    return true;
                }

                if (value is uint uintValue && uintValue <= int.MaxValue)
                {
                    result = (int)uintValue;
                    return true;
                }

                if (value is ulong ulongValue && ulongValue <= int.MaxValue)
                {
                    result = (int)ulongValue;
                    return true;
                }

                return int.TryParse(value.ToString(), out result);
            }
        }

        // ─── Debug: Dump System Prompt ─────────────────────────────────────────────

        /// <summary>
        /// Dumps the full NPC system prompt (persona + capabilities guide) to the
        /// console without making an LLM call. Use this to verify catalog entries and
        /// effect lists are visible within the token budget.
        /// Works in both Play Mode and Edit Mode.
        /// </summary>
        [UnityEditor.MenuItem(DumpSystemPromptMenuPath)]
        public static void MenuDumpSystemPrompt()
        {
            List<Dictionary<string, object>> nearbyNpcs = DialogueMCPBridge.GetNearbyNpcActors(1);
            if (nearbyNpcs.Count == 0)
            {
                Debug.LogWarning("[DialogueMCP] No NPC found. Make sure an NpcDialogueActor exists in the scene.");
                return;
            }

            if (!TryReadNpcFromDictionary(nearbyNpcs[0], out ulong npcNetId, out string npcName))
            {
                Debug.LogWarning("[DialogueMCP] Could not read NPC network ID.");
                return;
            }

            // Resolve actor component from the spawned NetworkObject
            NpcDialogueActor actor = null;
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(npcNetId, out NetworkObject npcNetObj))
            {
                actor = npcNetObj.GetComponent<NpcDialogueActor>();
            }

            if (actor == null)
            {
                // Fallback: find first actor in scene (Edit Mode or unspawned)
#if UNITY_2023_1_OR_NEWER
                actor = UnityEngine.Object.FindObjectsByType<NpcDialogueActor>(FindObjectsInactive.Exclude)
                    is { Length: > 0 } arr ? arr[0] : null;
#else
                actor = UnityEngine.Object.FindObjectOfType<NpcDialogueActor>();
#endif
            }

            if (actor == null)
            {
                Debug.LogWarning("[DialogueMCP] Could not resolve NpcDialogueActor component.");
                return;
            }

            // Resolve listener for spatial context
            GameObject listenerObj = null;
            string listenerName = "TestPlayer";
            if (Application.isPlaying && NetworkManager.Singleton != null)
            {
                NetworkObject lp = DialogueMCPBridge.ResolveLocalPlayerObjectPublic(
                    NetworkManager.Singleton.LocalClientId);
                if (lp != null) { listenerObj = lp.gameObject; listenerName = lp.gameObject.name; }
            }

            string prompt = actor.BuildSystemPrompt(string.Empty, string.Empty, listenerObj, listenerName);
            // Use the service's actual budget if available; otherwise show raw size.
            var svc = NetworkDialogueService.Instance;
            int budget = svc != null ? svc.RemoteSystemPromptCharBudget : 0;
            bool fits = budget <= 0 || prompt.Length <= budget;

            Debug.Log(
                $"[DialogueMCP] SystemPrompt dump — NPC={actor.gameObject.name} " +
                $"chars={prompt.Length} budget={budget} fits={fits}\n\n{prompt}"
            );
        }

        [UnityEditor.MenuItem(ExportIncidentBundleMenuPath)]
        public static void MenuExportDiagnosticIncidentBundle()
        {
            Dictionary<string, object> result = DialogueMCPBridge.ExportDiagnosticIncidentBundle();
            if (result == null)
            {
                Debug.LogWarning("[DialogueMCP] Incident bundle export returned no result.");
                return;
            }

            bool ok =
                result.TryGetValue("ok", out object okValue)
                && okValue is bool okBool
                && okBool;
            if (!ok)
            {
                string error = result.TryGetValue("error", out object errorValue)
                    ? errorValue?.ToString() ?? "unknown error"
                    : "unknown error";
                Debug.LogWarning($"[DialogueMCP] Incident bundle export failed: {error}");
                return;
            }

            string directory = result.TryGetValue("bundle_directory", out object directoryValue)
                ? directoryValue?.ToString() ?? string.Empty
                : string.Empty;
            Debug.Log($"[DialogueMCP] Incident bundle exported: {directory}");
        }

        // ─── Debug: Direct Dispatch Test ──────────────────────────────────────────

        /// <summary>
        /// Fires all 8 animations and the first available effect directly through the
        /// dispatch pipeline — no LLM call, no prompt, instant feedback.
        /// Each action is staggered 2 s apart so you can observe each one in sequence.
        /// Requires Play Mode with NPCs spawned.
        /// </summary>
        [UnityEditor.MenuItem(TestDirectDispatchMenuPath)]
        public static void MenuTestDirectDispatch()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.DisplayDialog("Direct Dispatch Test", "Enter Play Mode first.", "OK");
                return;
            }

            var service = NetworkDialogueService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[DialogueMCP] NetworkDialogueService not found.");
                return;
            }

            List<Dictionary<string, object>> nearbyNpcs = DialogueMCPBridge.GetNearbyNpcActors(1);
            if (nearbyNpcs.Count == 0 ||
                !TryReadNpcFromDictionary(nearbyNpcs[0], out ulong npcNetId, out string npcName))
            {
                Debug.LogWarning("[DialogueMCP] No spawned NPC found.");
                return;
            }

            ulong listenerNetId = 0;
            string listenerName = "Self";
            NetworkObject localPlayer = DialogueMCPBridge.ResolveLocalPlayerObjectPublic(
                NetworkManager.Singleton.LocalClientId);
            if (localPlayer != null)
            {
                listenerNetId = localPlayer.NetworkObjectId;
                listenerName  = localPlayer.gameObject.name;
            }

            // One action per animation with 2 s stagger, then one EFFECT at the end.
            var actions = new List<DialogueAction>
            {
                new DialogueAction { Type = "ANIM", Tag = "EmphasisReact", Target = "Self", Delay = 0f  },
                new DialogueAction { Type = "ANIM", Tag = "IdleVariant",   Target = "Self", Delay = 2f  },
                new DialogueAction { Type = "ANIM", Tag = "TurnLeft",      Target = "Self", Delay = 4f  },
                new DialogueAction { Type = "ANIM", Tag = "TurnRight",     Target = "Self", Delay = 6f  },
                new DialogueAction { Type = "ANIM", Tag = "HardLand",      Target = "Self", Delay = 8f  },
                new DialogueAction { Type = "ANIM", Tag = "FreeFall",      Target = "Self", Delay = 10f },
                new DialogueAction { Type = "ANIM", Tag = "Land",          Target = "Self", Delay = 12f },
                new DialogueAction { Type = "ANIM", Tag = "Jump",          Target = "Self", Delay = 14f },
            };

            // Append the first available effect from the catalog
            try
            {
                var effectCatalog = Effects.EffectCatalog.Instance ?? Effects.EffectCatalog.Load();
                if (effectCatalog != null)
                {
                    foreach (var fx in effectCatalog.GetAllRegisteredEffects())
                    {
                        if (fx != null && !string.IsNullOrWhiteSpace(fx.effectTag))
                        {
                            actions.Add(new DialogueAction
                            {
                                Type   = "EFFECT",
                                Tag    = fx.effectTag,
                                Target = listenerName,
                                Delay  = 16f,
                            });
                            break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DialogueMCP] Could not resolve EffectCatalog: {ex.Message}");
            }

            service.InjectTestActions(npcNetId, listenerNetId, actions);

            Debug.Log(
                $"[DialogueMCP] Direct dispatch test → NPC '{npcName}' (id={npcNetId}), " +
                $"{actions.Count} actions (8 anims + effect, 2 s stagger). Watch the NPC."
            );
        }
    }
#endif
}
