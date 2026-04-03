using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Network_Game.Diagnostics;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Inference-client selection, warmup, and remote backend readiness.
        private async Task<bool> EnsureWarmup()
        {
            if (m_WarmupTask != null && (m_WarmupTask.IsFaulted || m_WarmupTask.IsCanceled))
            {
                string staleReason = ExtractWarmupFailureReason(m_WarmupTask);
                m_WarmupTask = null;
                m_LastWarmupFailureReason = staleReason;
                if (m_LogDebug)
                {
                    NGLog.Warn("Dialogue", NGLog.Format("Cleared stale warmup task", ("reason", staleReason)));
                }
            }

            float now = Time.realtimeSinceStartup;
            if (m_WarmupTask == null && m_NextWarmupRetryAt > now)
            {
                float retryIn = m_NextWarmupRetryAt - now;
                if (m_WarmupDegradedMode)
                {
                    m_LastWarmupFailureReason = $"Warmup degraded mode active; retry in {retryIn:0.0}s.";
                }

                return false;
            }

            if (m_WarmupTask == null)
            {
                if (m_LogDebug)
                {
                    NGLog.Debug("Dialogue", "Starting LLM warmup.");
                }

                IDialogueInferenceClient inferenceClient = ResolveInferenceClient();
                if (inferenceClient == null)
                {
                    throw new Exception("Remote inference client unavailable.");
                }

                float remoteProbeTimeoutSeconds = Mathf.Clamp(
                    m_WarmupTimeoutSeconds > 0f ? m_WarmupTimeoutSeconds : 10f,
                    5f,
                    20f
                );
                // Task.Run uses a thread-pool thread which is unavailable on WebGL.
                // Calling the async method directly produces a Task that runs on the
                // Unity synchronisation context — correct on all platforms.
                m_WarmupTask = PerformWarmupProbeAsync(inferenceClient, remoteProbeTimeoutSeconds);
            }

            Task activeTask = m_WarmupTask;
            float effectiveWarmupTimeoutSeconds =
                m_WarmupTimeoutSeconds > 0f ? Mathf.Min(m_WarmupTimeoutSeconds, 20f) : 20f;
            if (
                effectiveWarmupTimeoutSeconds > 0f
                && await Task.WhenAny(
                        activeTask,
                        Task.Delay(TimeSpan.FromSeconds(effectiveWarmupTimeoutSeconds))
                    ) != activeTask
            )
            {
                return HandleWarmupFailure(
                    $"Warmup timed out after {effectiveWarmupTimeoutSeconds:0.0}s."
                );
            }

            try
            {
                await activeTask;
                m_WarmupTask = activeTask;
                m_WarmupConsecutiveFailures = 0;
                m_WarmupDegradedMode = false;
                m_NextWarmupRetryAt = 0f;
                m_LastWarmupFailureReason = string.Empty;
                return true;
            }
            catch (Exception)
            {
                return HandleWarmupFailure(ExtractWarmupFailureReason(activeTask));
            }
        }

        private bool HandleWarmupFailure(string reason)
        {
            m_WarmupTask = null;
            m_WarmupConsecutiveFailures++;
            m_LastWarmupFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Warmup failed with unknown reason."
                : reason;
            float retryDelaySeconds = Mathf.Max(0f, m_WarmupRetryCooldownSeconds);
            m_NextWarmupRetryAt = Time.realtimeSinceStartup + retryDelaySeconds;
            if (m_WarmupConsecutiveFailures >= Mathf.Max(1, m_DegradedWarmupFailureThreshold))
            {
                m_WarmupDegradedMode = true;
                NGLog.Error(
                    "Dialogue",
                    NGLog.Format(
                        "Warmup degraded mode enabled",
                        ("failures", m_WarmupConsecutiveFailures),
                        ("reason", m_LastWarmupFailureReason),
                        ("retryIn", retryDelaySeconds)
                    )
                );
            }
            else
            {
                NGLog.Warn(
                    "Dialogue",
                    NGLog.Format(
                        "Warmup failed",
                        ("failures", m_WarmupConsecutiveFailures),
                        ("reason", m_LastWarmupFailureReason),
                        ("retryIn", retryDelaySeconds)
                    )
                );
            }

            return false;
        }

        private static string ExtractWarmupFailureReason(Task task)
        {
            if (task == null)
            {
                return "Warmup task missing.";
            }

            if (task.IsCanceled)
            {
                return "Warmup task canceled.";
            }

            if (!task.IsFaulted)
            {
                return "Warmup task failed.";
            }

            Exception ex = task.Exception?.GetBaseException();
            return string.IsNullOrWhiteSpace(ex?.Message)
                ? "Warmup task faulted."
                : "Warmup task faulted: " + ex.Message;
        }

        private void EnsureOpenAIChatClient()
        {
            if (m_OpenAIChatClient != null)
            {
                SyncOpenAIChatClientParams();
                return;
            }

            m_OpenAIChatClient = new OpenAIChatClient();
            SyncOpenAIChatClientParams();
            NGLog.Info(
                "Dialogue",
                NGLog.Format(
                    "OpenAI chat client initialized",
                    ("host", m_OpenAIChatClient.Host),
                    ("port", m_OpenAIChatClient.Port),
                    (
                        "model",
                        string.IsNullOrEmpty(ResolveConfiguredRemoteModelName())
                            ? "(auto)"
                            : ResolveConfiguredRemoteModelName()
                    )
                )
            );
        }

        private IDialogueInferenceClient ResolveInferenceClient()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // On WebGL prefer the Edge Function proxy when configured — keeps the
            // LLM API key server-side and avoids localhost-unreachable issues.
            DialogueBackendConfig cfg = GetDialogueBackendConfig();
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.WebGlEdgeFunctionUrl))
            {
                EnsureEdgeFunctionClient(cfg);
                return m_EdgeFunctionClient;
            }
#endif
            EnsureOpenAIChatClient();
            return m_OpenAIChatClient;
        }

        private void EnsureEdgeFunctionClient(DialogueBackendConfig cfg)
        {
            if (m_EdgeFunctionClient == null)
            {
                m_EdgeFunctionClient = new EdgeFunctionInferenceClient();
                NGLog.Info(
                    "Dialogue",
                    NGLog.Format(
                        "Edge Function inference client initialized",
                        ("url", cfg.WebGlEdgeFunctionUrl)
                    )
                );
            }

            // Re-apply config every call so live Inspector changes are picked up.
            var runtimeConfig = BuildInferenceRuntimeConfig();
            // Redirect host/port to the Edge Function URL for this client.
            runtimeConfig.Host   = cfg.WebGlEdgeFunctionUrl;
            runtimeConfig.Port   = 443;
            runtimeConfig.ApiKey = !string.IsNullOrWhiteSpace(cfg.WebGlAnonKey)
                ? cfg.WebGlAnonKey
                : runtimeConfig.ApiKey;
            m_EdgeFunctionClient.ApplyConfig(runtimeConfig);
        }

        public OpenAIChatClient CreateConfiguredOpenAIChatClient()
        {
            OpenAIChatClient client = new OpenAIChatClient();
            client.ApplyConfig(BuildInferenceRuntimeConfig());
            return client;
        }

        public DialogueInferenceRuntimeConfig CreateInferenceRuntimeConfigSnapshot()
        {
            return BuildInferenceRuntimeConfig();
        }

        private void SyncOpenAIChatClientParams()
        {
            if (m_OpenAIChatClient != null)
            {
                m_OpenAIChatClient.ApplyConfig(BuildInferenceRuntimeConfig());
            }
        }

        private DialogueInferenceRuntimeConfig BuildInferenceRuntimeConfig()
        {
            DialogueBackendConfig backendConfig = GetDialogueBackendConfig();
            if (backendConfig != null)
            {
                return new DialogueInferenceRuntimeConfig
                {
                    Host   = backendConfig.EffectiveHost,
                    Port   = backendConfig.EffectivePort,
                    ApiKey = !string.IsNullOrWhiteSpace(backendConfig.EffectiveApiKey)
                        ? backendConfig.EffectiveApiKey
                        : ResolveRemoteApiKey(),
                    Model = ResolveConfiguredRemoteModelName(),
                    Temperature = backendConfig.Temperature,
                    MaxTokens = backendConfig.MaxTokens,
                    TopP = backendConfig.TopP,
                    FrequencyPenalty = backendConfig.FrequencyPenalty,
                    PresencePenalty = backendConfig.PresencePenalty,
                    Seed = backendConfig.Seed,
                    TopK = backendConfig.TopK,
                    RepeatPenalty = backendConfig.RepeatPenalty,
                    MinP = backendConfig.MinP,
                    TypicalP = backendConfig.TypicalP,
                    RepeatLastN = backendConfig.RepeatLastN,
                    Mirostat = backendConfig.Mirostat,
                    MirostatTau = backendConfig.MirostatTau,
                    MirostatEta = backendConfig.MirostatEta,
                    NProbs = backendConfig.NProbs,
                    IgnoreEos = backendConfig.IgnoreEos,
                    CachePrompt = backendConfig.CachePrompt,
                    Grammar = string.IsNullOrWhiteSpace(backendConfig.Grammar)
                        ? null
                        : backendConfig.Grammar,
                    StopSequences        = ResolveConfiguredRemoteStopSequences(),
                    ThinkingBudgetTokens = backendConfig.ThinkingBudgetTokens,
                };
            }

            return new DialogueInferenceRuntimeConfig
            {
                Host = DefaultRemoteHost,
                Port = DefaultRemotePort,
                ApiKey = ResolveRemoteApiKey(),
                Model = ResolveConfiguredRemoteModelName(),
                StopSequences = ResolveConfiguredRemoteStopSequences(),
            };
        }

        private string ResolveConfiguredRemoteModelName()
        {
            DialogueBackendConfig backendConfig = GetDialogueBackendConfig();
            if (backendConfig != null)
            {
                string model = backendConfig.Model;
                if (!string.IsNullOrWhiteSpace(model))
                {
                    if (
                        string.Equals(model, "auto", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(model, "(auto)", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return string.Empty;
                    }

                    return model;
                }
            }

            string configured = m_RemoteModelName == null ? string.Empty : m_RemoteModelName.Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                return string.Empty;
            }

            if (
                string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configured, "(auto)", StringComparison.OrdinalIgnoreCase)
            )
            {
                return string.Empty;
            }

            return configured;
        }

        private string ResolveRemoteApiKey()
        {
            if (TryGetApiKeyFromLmStudioFile(out string apiKey))
            {
                return apiKey;
            }

            if (TryGetApiKeyFromEnvironment("LMSTUDIO_API_KEY", out apiKey))
            {
                return apiKey;
            }

            if (TryGetApiKeyFromEnvironment("OPENAI_API_KEY", out apiKey))
            {
                return apiKey;
            }

            if (GetDialogueBackendConfig() != null && !string.IsNullOrWhiteSpace(m_DialogueBackendConfig.ApiKey))
            {
                return m_DialogueBackendConfig.ApiKey;
            }

            return string.Empty;
        }

        private string[] ResolveConfiguredRemoteStopSequences()
        {
            DialogueBackendConfig backendConfig = GetDialogueBackendConfig();
            if (backendConfig != null)
            {
                string[] stopSequences = backendConfig.GetStopSequences();
                if (stopSequences != null && stopSequences.Length != 0)
                {
                    return stopSequences;
                }
            }

            return m_RemoteStopSequences != null && m_RemoteStopSequences.Length != 0
                ? m_RemoteStopSequences
                : null;
        }

        private static bool TryGetApiKeyFromEnvironment(string varName, out string apiKey)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Environment.GetEnvironmentVariable is not available in browser sandboxes.
            apiKey = string.Empty;
            return false;
#else
            apiKey = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = string.Empty;
                return false;
            }

            apiKey = apiKey.Trim();
            return true;
#endif
        }

        private static bool TryGetApiKeyFromLmStudioFile(out string apiKey)
        {
            apiKey = string.Empty;
#if UNITY_WEBGL && !UNITY_EDITOR
            // File I/O is unavailable in browser sandboxes.
            return false;
#else
            try
            {
                string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userProfilePath))
                    return false;

                string path = Path.Combine(userProfilePath, ".lmstudio", "lms-key");
                if (!File.Exists(path))
                    return false;

                string value = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(value))
                    return false;

                apiKey = value.Trim();
                return true;
            }
            catch
            {
                return false;
            }
#endif
        }

        private static async Task PerformWarmupProbeAsync(
            IDialogueInferenceClient client,
            float timeoutSeconds
        )
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            if (!await client.CheckConnectionAsync(cts.Token).ConfigureAwait(false))
            {
                throw new Exception(
                    "OpenAI-compatible warmup probe failed — check the LLM backend URL in DialogueBackendConfig."
                );
            }
        }

        private string BuildWarmupStateLabel()
        {
            if (m_WarmupTask != null && !m_WarmupTask.IsCompleted)
            {
                return "InProgress";
            }

            if (m_WarmupDegradedMode)
            {
                return "Degraded";
            }

            if (m_WarmupConsecutiveFailures > 0)
            {
                return "RetryCooldown";
            }

            if (
                m_WarmupTask != null
                && m_WarmupTask.IsCompleted
                && !m_WarmupTask.IsFaulted
                && !m_WarmupTask.IsCanceled
            )
            {
                return "Ready";
            }

            return "Idle";
        }
    }
}
