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
                m_WarmupTask = Task.Run(async delegate
                {
                    using CancellationTokenSource warmupCts = new CancellationTokenSource(
                        TimeSpan.FromSeconds(remoteProbeTimeoutSeconds)
                    );
                    if (!await inferenceClient.CheckConnectionAsync(warmupCts.Token))
                    {
                        throw new Exception(
                            "OpenAI-compatible warmup probe failed at " + GetRemoteEndpointLabel()
                        );
                    }
                });
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
            EnsureOpenAIChatClient();
            return m_OpenAIChatClient;
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
                    Host = backendConfig.Host,
                    Port = backendConfig.Port,
                    ApiKey = ResolveRemoteApiKey(),
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
                    StopSequences = ResolveConfiguredRemoteStopSequences(),
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
            apiKey = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = string.Empty;
                return false;
            }

            apiKey = apiKey.Trim();
            return true;
        }

        private static bool TryGetApiKeyFromLmStudioFile(out string apiKey)
        {
            apiKey = string.Empty;
            try
            {
                string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userProfilePath))
                {
                    return false;
                }

                string path = Path.Combine(userProfilePath, ".lmstudio", "lms-key");
                if (!File.Exists(path))
                {
                    return false;
                }

                string value = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                apiKey = value.Trim();
                return true;
            }
            catch
            {
                return false;
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
