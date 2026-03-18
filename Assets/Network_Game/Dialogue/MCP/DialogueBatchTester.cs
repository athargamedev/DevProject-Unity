using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Network_Game.Diagnostics;
using Newtonsoft.Json;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue.MCP
{
    /// <summary>
    /// Fires a list of dialogue prompts sequentially and records LLM responses,
    /// parsed actions, and latency data. Results are written to a JSONL file in
    /// <see cref="OutputDirectory"/> when the run finishes.
    ///
    /// Trigger via <see cref="DialogueMCPBridge.RunBatchTest"/> from MCP, or call
    /// <see cref="StartBatch"/> directly from editor scripts.
    /// </summary>
    public class DialogueBatchTester : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────────

        private static DialogueBatchTester s_Instance;

        /// <summary>
        /// Returns the live instance, creating a DontDestroyOnLoad GameObject if needed.
        /// </summary>
        public static DialogueBatchTester GetOrCreate()
        {
            if (s_Instance != null)
                return s_Instance;

            var go = new GameObject("[DialogueBatchTester]");
            DontDestroyOnLoad(go);
            return go.AddComponent<DialogueBatchTester>();
        }

        // ─── Public state ─────────────────────────────────────────────────────────

        /// <summary>True while a batch run is in progress.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>True once the most recent batch has finished (pass or timeout).</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Accumulated results from the current or most recent batch run.</summary>
        public List<BatchTestResult> Results { get; } = new List<BatchTestResult>();

        /// <summary>Absolute path to the JSONL file written after the last run.</summary>
        public string LastOutputPath { get; private set; }

        // ─── Configuration ────────────────────────────────────────────────────────

        [Header("Timing")]
        [Tooltip("Seconds to wait between prompts to avoid queue congestion.")]
        [SerializeField] private float _interPromptDelay = 3f;

        [Tooltip("Max seconds to wait for a single response before moving on.")]
        [SerializeField] private float _responseTimeoutSeconds = 120f;

        [Header("Output")]
        [Tooltip("Directory relative to the project root for JSONL output files.")]
        [SerializeField] private string _outputDirectory = "Logs/BatchTests";

        // ─── Internal ─────────────────────────────────────────────────────────────

        private readonly Dictionary<int, BatchTestResult> _pending =
            new Dictionary<int, BatchTestResult>();

        private static int s_NextClientRequestId = 98000;

        // ─── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (s_Instance == this)
                s_Instance = null;
        }

        // ─── Entry point ──────────────────────────────────────────────────────────

        /// <summary>
        /// Starts a batch test run. If a run is already active the call is ignored.
        /// </summary>
        /// <param name="cases">Test prompts to send.</param>
        /// <param name="speakerNetworkId">NetworkObjectId of the NPC speaking.</param>
        /// <param name="listenerNetworkId">NetworkObjectId of the player listener (0 = local player).</param>
        /// <param name="label">Optional label used in the output filename.</param>
        public void StartBatch(
            IReadOnlyList<BatchTestCase> cases,
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            string label = null)
        {
            if (IsRunning)
            {
                NGLog.Warn("BatchTest", "Batch already running — ignoring new request.");
                return;
            }

            Results.Clear();
            _pending.Clear();
            IsComplete = false;
            IsRunning = true;

            SubscribeEvents();
            StartCoroutine(RunBatchCoroutine(cases, speakerNetworkId, listenerNetworkId, label));
        }

        // ─── Coroutine ────────────────────────────────────────────────────────────

        private IEnumerator RunBatchCoroutine(
            IReadOnlyList<BatchTestCase> cases,
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            string label)
        {
            var service = NetworkDialogueService.Instance;
            if (service == null)
            {
                NGLog.Error("BatchTest", "NetworkDialogueService not found — aborting batch.");
                FinishBatch(label);
                yield break;
            }

            if (NetworkManager.Singleton == null)
            {
                NGLog.Error("BatchTest", "NetworkManager not ready — aborting batch.");
                FinishBatch(label);
                yield break;
            }

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            // Resolve listener to local player when not specified
            ulong resolvedListenerNetworkId = listenerNetworkId;
            if (resolvedListenerNetworkId == 0)
            {
                NetworkObject localPlayer = DialogueMCPBridge.ResolveLocalPlayerObjectPublic(localClientId);
                resolvedListenerNetworkId = localPlayer != null ? localPlayer.NetworkObjectId : 0;
            }

            string conversationKey = service.ResolveConversationKey(
                speakerNetworkId,
                resolvedListenerNetworkId,
                localClientId,
                null);

            for (int i = 0; i < cases.Count; i++)
            {
                BatchTestCase testCase = cases[i];
                int clientRequestId = ++s_NextClientRequestId;

                var result = new BatchTestResult
                {
                    Index = i,
                    ClientRequestId = clientRequestId,
                    Prompt = testCase.Prompt,
                    Description = testCase.Description,
                    SentAtRealtime = Time.realtimeSinceStartup,
                };

                _pending[clientRequestId] = result;
                Results.Add(result);

                NGLog.Debug("BatchTest", NGLog.Format("Sending prompt",
                    ("index", i),
                    ("clientRequestId", clientRequestId),
                    ("prompt", testCase.Prompt)));

                service.RequestDialogue(new NetworkDialogueService.DialogueRequest
                {
                    Prompt = testCase.Prompt,
                    ConversationKey = conversationKey,
                    SpeakerNetworkId = speakerNetworkId,
                    ListenerNetworkId = resolvedListenerNetworkId,
                    RequestingClientId = localClientId,
                    Broadcast = false,
                    NotifyClient = false,
                    ClientRequestId = clientRequestId,
                    IsUserInitiated = true,
                    BlockRepeatedPrompt = false,
                    MinRepeatDelaySeconds = 0f,
                    RequireUserReply = false,
                });

                // Wait for response or timeout
                float deadline = Time.realtimeSinceStartup + _responseTimeoutSeconds;
                yield return new WaitUntil(
                    () => result.IsFinished || Time.realtimeSinceStartup >= deadline);

                if (!result.IsFinished)
                {
                    result.Error = "timeout";
                    result.Status = "Timeout";
                    result.IsFinished = true;
                    NGLog.Warn("BatchTest", NGLog.Format("Prompt timed out",
                        ("index", i), ("clientRequestId", clientRequestId)));
                }

                if (i < cases.Count - 1)
                    yield return new WaitForSeconds(_interPromptDelay);
            }

            FinishBatch(label);
        }

        private void FinishBatch(string label)
        {
            UnsubscribeEvents();
            IsRunning = false;
            IsComplete = true;
            WriteResults(label);

            NGLog.Debug("BatchTest", NGLog.Format("Batch complete",
                ("count", Results.Count),
                ("output", LastOutputPath ?? "none")));
        }

        // ─── Event handlers ───────────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            NetworkDialogueService.OnDialogueActionResponse += HandleActionResponse;
            NetworkDialogueService.OnDialogueResponseTelemetry += HandleTelemetry;
        }

        private void UnsubscribeEvents()
        {
            NetworkDialogueService.OnDialogueActionResponse -= HandleActionResponse;
            NetworkDialogueService.OnDialogueResponseTelemetry -= HandleTelemetry;
        }

        private void HandleActionResponse(
            int clientRequestId,
            NetworkDialogueService.DialogueRequest request,
            DialogueActionResponse actionResponse)
        {
            if (!_pending.TryGetValue(clientRequestId, out BatchTestResult result))
                return;

            if (actionResponse != null)
            {
                result.Speech = actionResponse.Speech;
                result.ParsedActions = actionResponse.Actions;
                result.ActionCount = actionResponse.Actions?.Count ?? 0;
            }
        }

        private void HandleTelemetry(NetworkDialogueService.DialogueResponseTelemetry telemetry)
        {
            if (!_pending.TryGetValue(telemetry.Request.ClientRequestId, out BatchTestResult result))
                return;

            result.Status = telemetry.Status.ToString();
            result.QueueLatencyMs = telemetry.QueueLatencyMs;
            result.ModelLatencyMs = telemetry.ModelLatencyMs;
            result.TotalLatencyMs = telemetry.TotalLatencyMs;
            result.IsFinished = true;

            _pending.Remove(telemetry.Request.ClientRequestId);
        }

        // ─── JSONL output ─────────────────────────────────────────────────────────

        private void WriteResults(string label)
        {
            try
            {
                string projectRoot = Path.Combine(Application.dataPath, "..");
                string dir = Path.Combine(projectRoot, _outputDirectory);
                Directory.CreateDirectory(dir);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string filename = string.IsNullOrWhiteSpace(label)
                    ? $"batch_{timestamp}.jsonl"
                    : $"batch_{label.Replace(" ", "_")}_{timestamp}.jsonl";

                string path = Path.GetFullPath(Path.Combine(dir, filename));

                var sb = new StringBuilder();
                foreach (BatchTestResult r in Results)
                    sb.AppendLine(JsonConvert.SerializeObject(r));

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                LastOutputPath = path;
            }
            catch (Exception ex)
            {
                NGLog.Error("BatchTest", $"Failed to write results: {ex.Message}");
            }
        }

        // ─── Data types ───────────────────────────────────────────────────────────

        /// <summary>One prompt to send during a batch run.</summary>
        public class BatchTestCase
        {
            /// <summary>The user-side dialogue prompt to send to the NPC.</summary>
            public string Prompt;

            /// <summary>Optional human-readable label for this test case.</summary>
            public string Description;
        }

        /// <summary>Recorded outcome for one prompt in a batch run.</summary>
        public class BatchTestResult
        {
            public int Index;
            public int ClientRequestId;
            public string Prompt;
            public string Description;
            public string Status;
            public string Speech;
            public List<DialogueAction> ParsedActions;
            public int ActionCount;
            public string Error;
            public float QueueLatencyMs;
            public float ModelLatencyMs;
            public float TotalLatencyMs;

            // Runtime-only — excluded from JSON output
            [JsonIgnore] public float SentAtRealtime;
            [JsonIgnore] public bool IsFinished;
        }
    }
}
