using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Play-mode batch simulation runner.
    ///
    /// Fires a configurable list of prompts at a target NPC one at a time, waits for
    /// each LLM response, captures which effects were actually spawned, then writes
    /// a full JSON report to:
    ///   Application.persistentDataPath/DialogueLogs/sim_batch_YYYYMMDD_HHmmss.json
    ///
    /// Usage:
    ///   1. Add this component to any GameObject in the scene.
    ///   2. Assign Target Npc in the Inspector.
    ///   3. Enter Play Mode as Host (File > Build Settings > Host, or start as host in-game).
    ///   4. With Run On Start enabled the batch fires automatically once the
    ///      NetworkManager is ready; otherwise use the Context Menu "Run Sim Batch".
    ///
    /// The JSON report shows per-prompt: response text, effects triggered, latency,
    /// and success/failure — everything you need to iterate on the NPC prompt.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network Game/Dialogue/Dialogue Sim Batch Runner")]
    public sealed class DialogueSimBatchRunner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Target")]
        [SerializeField]
        [Tooltip("NPC to talk to. Must have NetworkObject + NpcDialogueActor.")]
        private NpcDialogueActor m_TargetNpc;

        [Header("Config")]
        [SerializeField]
        [Tooltip("Automatically run the batch when the NetworkManager becomes host/server.")]
        private bool m_RunOnStart = true;

        [SerializeField]
        [Range(5f, 120f)]
        [Tooltip("Max seconds to wait for a single LLM response before marking it timed-out.")]
        private float m_RequestTimeoutSeconds = 30f;

        [SerializeField]
        [Range(0f, 15f)]
        [Tooltip("Pause between consecutive requests (seconds).")]
        private float m_DelayBetweenRequests = 2f;

        [SerializeField]
        [Range(0.5f, 5f)]
        [Tooltip("Extra window after the response arrives to collect spawned effects.")]
        private float m_EffectCollectionWindowSeconds = 2f;

        [Header("Test Prompts")]
        [SerializeField]
        [TextArea(2, 5)]
        private string[] m_TestPrompts = new string[]
        {
            "Hello! What can you do?",
            "Show me your most powerful attack!",
            "Cast a lightning storm on me!",
            "Summon fire around us.",
            "Can you heal me?",
            "Make it rain!",
            "Hit me with your best spell at maximum power!",
            "Do something totally epic right now!",
            "Use all of your abilities at once!",
            "What effects do you have available?",
            "Defend yourself!",
            "I challenge you to a duel!",
            "Show me something I have never seen before.",
            "Destroy everything around us!",
            "Peace! Let us resolve this without violence.",
        };

        // ── Internal record ───────────────────────────────────────────────────────

        private sealed class RequestRecord
        {
            public int    Index;
            public string Prompt;
            public string ResponseText;
            public float  LatencyMs;
            public bool   Succeeded;
            public string Error;
            /// <summary>Actions the LLM requested: "TYPE:Tag" strings.</summary>
            public readonly List<string> ActionsRequested = new List<string>();
            /// <summary>Effects that actually spawned: "Tag@scale×dur" strings.</summary>
            public readonly List<string> EffectsSpawned   = new List<string>();
            public readonly List<string> AnimationsPlayed = new List<string>();
        }

        // ── Runtime state ─────────────────────────────────────────────────────────

        private const int ClientRequestIdBase = 9900;

        private readonly List<RequestRecord> m_Records = new List<RequestRecord>();

        // Written by event handlers on the main thread; read by the coroutine.
        private int    m_InflightIndex       = -1;
        private bool   m_WaitingForResponse  = false;
        private string m_InflightResponseText;
        private bool   m_InflightSucceeded;
        private string m_InflightError;
        private float  m_InflightStartTime;
        private string m_ConvKey;

        // Actions/effects/animations collected while a request is in-flight or in collection window.
        private readonly List<string> m_InflightActions    = new List<string>();
        private readonly List<string> m_InflightEffects    = new List<string>();
        private readonly List<string> m_InflightAnimations = new List<string>();

        private bool m_Running = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            NetworkDialogueService.OnDialogueResponse             += HandleResponse;
            NetworkDialogueService.OnActionsDispatched            += HandleActionsDispatched;
            DialogueSceneEffectsController.OnEffectApplied        += HandleEffectApplied;
            NpcDialogueAnimationController.OnAnimationPlayed      += HandleAnimationPlayed;
        }

        private void OnDisable()
        {
            NetworkDialogueService.OnDialogueResponse             -= HandleResponse;
            NetworkDialogueService.OnActionsDispatched            -= HandleActionsDispatched;
            DialogueSceneEffectsController.OnEffectApplied        -= HandleEffectApplied;
            NpcDialogueAnimationController.OnAnimationPlayed      -= HandleAnimationPlayed;
        }

        private void Start()
        {
            if (m_RunOnStart)
                StartCoroutine(RunBatchWhenReady());
        }

        // ── Public / context menu ─────────────────────────────────────────────────

        /// <summary>
        /// Starts the batch immediately (in Play Mode).
        /// Safe to call from scripts or the Inspector context menu.
        /// </summary>
        [ContextMenu("Run Sim Batch")]
        public void RunSimBatch()
        {
            if (!Application.isPlaying)
            {
                NGLog.Warn("DialogueSim", "Enter Play Mode first.");
                return;
            }
            if (m_Running)
            {
                NGLog.Warn("DialogueSim", "A sim batch is already running.");
                return;
            }
            if (NetworkManager.Singleton == null ||
                (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer))
            {
                NGLog.Warn("DialogueSim", "[SimBatch] NetworkManager is not host/server yet. " +
                    "Start the game as Host first, then run the batch.");
                return;
            }
            StartCoroutine(RunBatch());
        }

        // ── Coroutines ────────────────────────────────────────────────────────────

        private IEnumerator RunBatchWhenReady()
        {
            // Wait until NetworkManager is up as host or server.
            yield return new WaitUntil(() =>
                NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer));

            // Brief settle time so NPC actors finish spawning.
            yield return new WaitForSeconds(2f);
            yield return RunBatch();
        }

        private IEnumerator RunBatch()
        {
            // ── Pre-flight checks ────────────────────────────────────────────────

            if (m_TargetNpc == null)
            {
                NGLog.Warn("DialogueSim", "[SimBatch] TargetNpc not assigned — aborting.");
                yield break;
            }

            NetworkDialogueService service = NetworkDialogueService.Instance;
            if (service == null || !service.IsServer)
            {
                NGLog.Warn("DialogueSim", "[SimBatch] NetworkDialogueService not found or not server — aborting.");
                yield break;
            }

            NetworkObject playerObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (playerObj == null)
            {
                NGLog.Warn("DialogueSim", "[SimBatch] Local player object not available — aborting.");
                yield break;
            }

            NetworkObject npcNetObj = m_TargetNpc.NetworkObject
                ?? m_TargetNpc.GetComponent<NetworkObject>();
            if (npcNetObj == null || !npcNetObj.IsSpawned)
            {
                NGLog.Warn("DialogueSim", "[SimBatch] NPC NetworkObject is not spawned yet — aborting. " +
                    "Ensure the NPC is spawned before running the batch.");
                yield break;
            }

            m_Running = true;
            m_Records.Clear();

            ulong  npcId    = npcNetObj.NetworkObjectId;
            ulong  playerId = playerObj.NetworkObjectId;
            string convKey  = service.ResolveConversationKey(npcId, playerId, playerObj.OwnerClientId, null);
            m_ConvKey = convKey;

            NGLog.Info("DialogueSim",
                $"[SimBatch] Starting {m_TestPrompts.Length} prompts -> NPC '{m_TargetNpc.name}' | key={convKey}");

            // ── Main loop ────────────────────────────────────────────────────────

            for (int i = 0; i < m_TestPrompts.Length; i++)
            {
                string prompt = m_TestPrompts[i];
                if (string.IsNullOrWhiteSpace(prompt))
                    continue;

                // Reset in-flight state.
                m_InflightIndex       = i;
                m_InflightResponseText = null;
                m_InflightSucceeded    = false;
                m_InflightError        = null;
                m_InflightStartTime    = Time.realtimeSinceStartup;
                m_InflightActions.Clear();
                m_InflightEffects.Clear();
                m_InflightAnimations.Clear();
                m_WaitingForResponse   = true;

                NGLog.Info("DialogueSim",
                    $"[SimBatch {i + 1}/{m_TestPrompts.Length}] Sending: \"{prompt}\"");

                service.TryEnqueueRequest(
                    new NetworkDialogueService.DialogueRequest
                    {
                        Prompt                = prompt,
                        ConversationKey       = convKey,
                        SpeakerNetworkId      = npcId,
                        ListenerNetworkId     = playerId,
                        RequestingClientId    = playerObj.OwnerClientId,
                        Broadcast             = true,
                        BroadcastDuration     = 4f,
                        NotifyClient          = true,
                        ClientRequestId       = ClientRequestIdBase + i,
                        IsUserInitiated       = true,
                        BlockRepeatedPrompt   = false,
                        MinRepeatDelaySeconds = 0f,
                        RequireUserReply      = false,
                    },
                    out _,
                    out _
                );

                // Wait for LLM response (or timeout).
                float deadline = Time.realtimeSinceStartup + m_RequestTimeoutSeconds;
                yield return new WaitUntil(() =>
                    !m_WaitingForResponse || Time.realtimeSinceStartup >= deadline);

                bool timedOut        = m_WaitingForResponse;
                m_WaitingForResponse = false;

                // Brief extra window to catch effects that spawn right after the response.
                yield return new WaitForSeconds(m_EffectCollectionWindowSeconds);

                float rawLatency = Time.realtimeSinceStartup - m_InflightStartTime
                                   - m_EffectCollectionWindowSeconds;

                // Build record.
                var record = new RequestRecord
                {
                    Index        = i,
                    Prompt       = prompt,
                    ResponseText = timedOut ? null : m_InflightResponseText,
                    Succeeded    = !timedOut && m_InflightSucceeded,
                    Error        = timedOut ? "timeout" : m_InflightError,
                    LatencyMs    = Mathf.Max(0f, rawLatency * 1000f),
                };
                record.ActionsRequested.AddRange(m_InflightActions);
                record.EffectsSpawned.AddRange(m_InflightEffects);
                record.AnimationsPlayed.AddRange(m_InflightAnimations);
                m_Records.Add(record);

                int reqEffects = record.ActionsRequested.FindAll(a => a.StartsWith("EFFECT:", StringComparison.OrdinalIgnoreCase)).Count;
                int reqAnims   = record.ActionsRequested.FindAll(a => a.StartsWith("ANIM:",   StringComparison.OrdinalIgnoreCase)).Count;

                // Summary line per request.
                string actList  = record.ActionsRequested.Count > 0
                    ? string.Join(", ", record.ActionsRequested) : "none";
                string fxList   = record.EffectsSpawned.Count > 0
                    ? string.Join(", ", record.EffectsSpawned) : "none";
                string animList = record.AnimationsPlayed.Count > 0
                    ? string.Join(", ", record.AnimationsPlayed) : "none";
                string status   = record.Succeeded ? "OK" : $"FAIL({record.Error})";
                NGLog.Info("DialogueSim",
                    $"[SimBatch {i + 1}] {status} | {record.LatencyMs:F0}ms" +
                    $" | requested=[{actList}]" +
                    $" | effects=[{fxList}] ({record.EffectsSpawned.Count}/{reqEffects} spawned)" +
                    $" | anims=[{animList}] ({record.AnimationsPlayed.Count}/{reqAnims} played)" +
                    $" | \"{Truncate(record.ResponseText, 80)}\"");

                if (i < m_TestPrompts.Length - 1)
                    yield return new WaitForSeconds(m_DelayBetweenRequests);
            }

            m_Running = false;
            WriteReport();
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void HandleResponse(NetworkDialogueService.DialogueResponse response)
        {
            if (!m_WaitingForResponse)
                return;

            // Only match requests we submitted in this batch.
            if (response.Request.ClientRequestId != ClientRequestIdBase + m_InflightIndex)
                return;

            m_InflightResponseText = response.ResponseText;
            m_InflightSucceeded    = response.Status == NetworkDialogueService.DialogueStatus.Completed;
            m_InflightError        = response.Error;
            m_WaitingForResponse   = false;
        }

        private void HandleEffectApplied(DialogueSceneEffectsController.AppliedEffectInfo info)
        {
            // Collect effects from our NPC during both the wait and the collection window.
            if (m_InflightIndex < 0)
                return;

            NetworkObject npcObj = m_TargetNpc?.NetworkObject
                ?? m_TargetNpc?.GetComponent<NetworkObject>();
            if (npcObj != null && info.SourceNetworkObjectId != npcObj.NetworkObjectId)
                return;

            string tag = !string.IsNullOrWhiteSpace(info.EffectTag) ? info.EffectTag : info.EffectName;
            if (!string.IsNullOrWhiteSpace(tag))
                m_InflightEffects.Add($"{tag}@{info.Scale:F1}x{info.DurationSeconds:F1}s");
        }

        private void HandleAnimationPlayed(ulong sourceNetworkObjectId, string animName)
        {
            if (m_InflightIndex < 0 || string.IsNullOrWhiteSpace(animName))
                return;

            NetworkObject npcObj = m_TargetNpc?.NetworkObject
                ?? m_TargetNpc?.GetComponent<NetworkObject>();
            if (npcObj != null && sourceNetworkObjectId != npcObj.NetworkObjectId)
                return;

            m_InflightAnimations.Add(animName);
        }

        private void HandleActionsDispatched(string convKey, IReadOnlyList<DialogueAction> actions)
        {
            if (m_InflightIndex < 0 || actions == null) return;
            if (!string.IsNullOrEmpty(m_ConvKey) && convKey != m_ConvKey) return;

            foreach (DialogueAction a in actions)
            {
                if (a == null || string.IsNullOrWhiteSpace(a.Type)) continue;
                m_InflightActions.Add($"{a.Type}:{a.Tag ?? "?"}");
            }
        }

        // ── Report ────────────────────────────────────────────────────────────────

        private void WriteReport()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "DialogueLogs");
                Directory.CreateDirectory(dir);

                string ts   = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(dir, $"sim_batch_{ts}.json");

                int   ok           = 0;
                int   failed       = 0;
                int   withFx       = 0;
                int   totalRequested = 0;
                int   totalSpawned   = 0;
                float totalMs      = 0f;

                foreach (var r in m_Records)
                {
                    if (r.Succeeded) ok++; else failed++;
                    if (r.EffectsSpawned.Count > 0) withFx++;
                    totalMs += r.LatencyMs;
                    totalRequested += r.ActionsRequested.FindAll(a => a.StartsWith("EFFECT:", StringComparison.OrdinalIgnoreCase)).Count;
                    totalSpawned   += r.EffectsSpawned.Count;
                }

                float avgMs = m_Records.Count > 0 ? totalMs / m_Records.Count : 0f;

                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"timestamp\": \"{DateTime.UtcNow:o}\",");
                sb.AppendLine($"  \"npc\": \"{EscapeJson(m_TargetNpc?.name ?? "unknown")}\",");
                sb.AppendLine($"  \"totalRequests\": {m_Records.Count},");
                sb.AppendLine($"  \"succeeded\": {ok},");
                sb.AppendLine($"  \"failed\": {failed},");
                sb.AppendLine($"  \"withEffects\": {withFx},");
                sb.AppendLine($"  \"totalEffectsRequested\": {totalRequested},");
                sb.AppendLine($"  \"totalEffectsSpawned\": {totalSpawned},");
                sb.AppendLine($"  \"effectSpawnRate\": \"{(totalRequested > 0 ? (float)totalSpawned / totalRequested * 100f : 0f):F0}%\",");
                sb.AppendLine($"  \"avgLatencyMs\": {avgMs:F1},");
                sb.AppendLine("  \"requests\": [");

                for (int i = 0; i < m_Records.Count; i++)
                {
                    var r = m_Records[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"index\": {r.Index},");
                    sb.AppendLine($"      \"prompt\": \"{EscapeJson(r.Prompt)}\",");
                    sb.AppendLine($"      \"succeeded\": {(r.Succeeded ? "true" : "false")},");
                    sb.AppendLine($"      \"latencyMs\": {r.LatencyMs:F1},");
                    sb.AppendLine($"      \"error\": {(r.Error != null ? $"\"{EscapeJson(r.Error)}\"" : "null")},");
                    sb.AppendLine($"      \"response\": \"{EscapeJson(r.ResponseText ?? string.Empty)}\",");
                    sb.AppendLine($"      \"actionsRequested\": [{BuildJsonStringArray(r.ActionsRequested)}],");
                    sb.AppendLine($"      \"effectsSpawned\": [{BuildJsonStringArray(r.EffectsSpawned)}],");
                    sb.AppendLine($"      \"animationsPlayed\": [{BuildJsonStringArray(r.AnimationsPlayed)}]");
                    sb.Append("    }");
                    if (i < m_Records.Count - 1) sb.Append(',');
                    sb.AppendLine();
                }

                sb.AppendLine("  ]");
                sb.Append('}');

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                NGLog.Info("DialogueSim", $"[SimBatch] Report -> {path}");
                NGLog.Info("DialogueSim",
                    $"[SimBatch] Summary: {ok}/{m_Records.Count} OK | " +
                    $"{withFx} with effects | effects {totalSpawned}/{totalRequested} spawned ({(totalRequested > 0 ? (float)totalSpawned / totalRequested * 100f : 0f):F0}%) | avg {avgMs:F0}ms");
            }
            catch (Exception ex)
            {
                NGLog.Warn("DialogueSim", $"[SimBatch] Failed to write report: {ex.Message}");
            }
        }

        // ── Static helpers ────────────────────────────────────────────────────────

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string BuildJsonStringArray(List<string> list)
        {
            if (list == null || list.Count == 0)
                return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append('"');
                sb.Append(EscapeJson(list[i]));
                sb.Append('"');
                if (i < list.Count - 1)
                    sb.Append(", ");
            }
            return sb.ToString();
        }
    }
}
