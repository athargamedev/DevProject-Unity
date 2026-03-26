using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Project-owned runtime config for remote dialogue inference.
    /// Stores LM Studio / Qwen-specific transport and sampling settings for the
    /// server-authoritative remote dialogue backend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueBackendConfig : MonoBehaviour
    {
        private static readonly string[] s_DefaultStopSequences =
        {
            "<|eot_id|>",
            "<|end_of_text|>",
            "<|start_header_id|>"
        };

        [Header("Transport")]
        private string m_Host = "127.0.0.1";

        [SerializeField]
        [Min(1)]
        private int m_Port = 7002;

        [SerializeField]
        [Tooltip("Model identifier sent to LM Studio. Leave empty to use the service fallback.")]
        private string m_Model = "qwen3-8b";

        [SerializeField]
        [Tooltip("Optional API key override. Leave empty to use LM Studio file/env fallback.")]
        private string m_ApiKey = string.Empty;

        [SerializeField]
        [Tooltip("Stop sequences injected into every remote request to avoid chat-template bleed.")]
        private string[] m_StopSequences = (string[])s_DefaultStopSequences.Clone();

        [Header("Sampling")]
        [SerializeField]
        [Range(0f, 2f)]
        private float m_Temperature = 0.3f;

        [SerializeField]
        [Min(-1)]
        private int m_MaxTokens = 2048;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_TopP = 0.9f;

        [SerializeField]
        [Range(0, 100)]
        private int m_TopK = 40;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_MinP = 0.05f;

        [SerializeField]
        [Range(0f, 2f)]
        private float m_RepeatPenalty = 1.1f;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_PresencePenalty = 0f;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_FrequencyPenalty = 0f;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_TypicalP = 1f;

        [SerializeField]
        [Range(0, 2048)]
        private int m_RepeatLastN = 64;

        [SerializeField]
        [Range(0, 2)]
        private int m_Mirostat = 0;

        [SerializeField]
        [Range(0f, 10f)]
        private float m_MirostatTau = 5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_MirostatEta = 0.1f;

        [SerializeField]
        [Range(0, 10)]
        private int m_NProbs = 0;

        [SerializeField]
        private bool m_IgnoreEos;

        [SerializeField]
        private bool m_CachePrompt = true;

        [SerializeField]
        [Min(0)]
        private int m_Seed = 0;

        [SerializeField]
        [TextArea(1, 8)]
        private string m_Grammar = string.Empty;

        [Header("Qwen3 Extended Thinking")]
        [SerializeField]
        [Tooltip(
            "Thinking budget in tokens for Qwen3 models (0 = disabled). "
            + "When > 0 the model reasons in a <think> block before emitting the JSON output. "
            + "Improves effect/animation decision quality at the cost of extra latency. "
            + "Ignored by non-Qwen3 backends."
        )]
        [Min(-1)]
        private int m_ThinkingBudgetTokens = 512;

        [Header("WebGL / Hosted Backend")]
        [SerializeField]
        [Tooltip(
            "Override the inference host when running as a WebGL build. "
            + "Set to your hosted OpenAI-compatible endpoint (e.g. api.openrouter.ai). "
            + "Leave empty to use the Host field above on all platforms."
        )]
        private string m_WebGlHostOverride = string.Empty;

        [SerializeField]
        [Tooltip("Port used with WebGlHostOverride. Use 443 for HTTPS endpoints.")]
        [Min(1)]
#pragma warning disable CS0414 // value read only in UNITY_WEBGL builds
        private int m_WebGlPortOverride = 443;
#pragma warning restore CS0414

        [SerializeField]
        [Tooltip("API key used when WebGlHostOverride is set (e.g. OpenRouter key).")]
        private string m_WebGlApiKeyOverride = string.Empty;

        [Header("WebGL — Supabase Edge Function LLM Proxy")]
        [SerializeField]
        [Tooltip(
            "Full URL of the llm-proxy Supabase Edge Function.\n"
            + "Example: https://<project>.supabase.co/functions/v1/llm-proxy\n"
            + "When set, WebGL builds route LLM requests through this proxy instead\n"
            + "of calling the LLM backend directly, keeping the API key server-side.\n"
            + "Leave empty to use WebGlHostOverride (direct LLM call with exposed key)."
        )]
        private string m_WebGlEdgeFunctionUrl = string.Empty;

        [SerializeField]
        [Tooltip(
            "Supabase anon/public key for the Edge Function.\n"
            + "This key is safe to ship in the WebGL bundle — it only grants access\n"
            + "to the llm-proxy function, not to the database service-role APIs."
        )]
        private string m_WebGlAnonKey = string.Empty;

        [Header("Prompting")]
        [SerializeField]
        [TextArea(4, 16)]
        [Tooltip("System prompt used by the remote LM Studio / Qwen backend.")]
        private string m_SystemPrompt = string.Empty;

        public string Host => string.IsNullOrWhiteSpace(m_Host) ? "127.0.0.1" : m_Host.Trim();
        public int Port => Mathf.Clamp(m_Port, 1, 65535);
        public string Model => NormalizeOptionalValue(m_Model);
        public string ApiKey => NormalizeOptionalValue(m_ApiKey);
        public float Temperature => m_Temperature;
        public int MaxTokens => m_MaxTokens;
        public float TopP => m_TopP;
        public int TopK => m_TopK;
        public float MinP => m_MinP;
        public float RepeatPenalty => m_RepeatPenalty;
        public float PresencePenalty => m_PresencePenalty;
        public float FrequencyPenalty => m_FrequencyPenalty;
        public float TypicalP => m_TypicalP;
        public int RepeatLastN => m_RepeatLastN;
        public int Mirostat => m_Mirostat;
        public float MirostatTau => m_MirostatTau;
        public float MirostatEta => m_MirostatEta;
        public int NProbs => m_NProbs;
        public bool IgnoreEos => m_IgnoreEos;
        public bool CachePrompt => m_CachePrompt;
        public int Seed => m_Seed;
        public string Grammar => NormalizeOptionalValue(m_Grammar);
        public int ThinkingBudgetTokens => m_ThinkingBudgetTokens;

        /// <summary>
        /// Active LLM host — returns <see cref="m_WebGlHostOverride"/> on WebGL builds
        /// when it is configured, otherwise falls back to the standard Host.
        /// </summary>
        public string EffectiveHost
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!string.IsNullOrWhiteSpace(m_WebGlHostOverride))
                    return m_WebGlHostOverride.Trim();
#endif
                return Host;
            }
        }

        public int EffectivePort
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!string.IsNullOrWhiteSpace(m_WebGlHostOverride))
                    return UnityEngine.Mathf.Clamp(m_WebGlPortOverride, 1, 65535);
#endif
                return Port;
            }
        }

        public string EffectiveApiKey
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!string.IsNullOrWhiteSpace(m_WebGlHostOverride)
                    && !string.IsNullOrWhiteSpace(m_WebGlApiKeyOverride))
                    return m_WebGlApiKeyOverride.Trim();
#endif
                return ApiKey;
            }
        }

        /// <summary>
        /// Full URL of the Supabase llm-proxy Edge Function for WebGL builds.
        /// When non-empty, <see cref="NetworkDialogueService"/> will use
        /// <see cref="EdgeFunctionInferenceClient"/> instead of <see cref="OpenAIChatClient"/>.
        /// </summary>
        public string WebGlEdgeFunctionUrl => NormalizeOptionalValue(m_WebGlEdgeFunctionUrl);

        /// <summary>
        /// Supabase anon/public key for the Edge Function — safe to ship in WebGL builds.
        /// </summary>
        public string WebGlAnonKey => NormalizeOptionalValue(m_WebGlAnonKey);

        public string SystemPrompt => m_SystemPrompt ?? string.Empty;

        public void SetSystemPrompt(string value)
        {
            m_SystemPrompt = value ?? string.Empty;
        }

        public string[] GetStopSequences()
        {
            if (m_StopSequences == null || m_StopSequences.Length == 0)
            {
                return null;
            }

            return CloneFilteredArray(m_StopSequences);
        }

        private void OnValidate()
        {
            m_Port = Mathf.Clamp(m_Port, 1, 65535);
            m_MaxTokens = Mathf.Max(-1, m_MaxTokens);
            m_RepeatLastN = Mathf.Clamp(m_RepeatLastN, 0, 2048);
            m_NProbs = Mathf.Clamp(m_NProbs, 0, 10);
            m_Seed = Mathf.Max(0, m_Seed);
        }

        private static string NormalizeOptionalValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string[] CloneFilteredArray(string[] values)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return null;
            }

            var filtered = new string[count];
            int index = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    filtered[index++] = values[i].Trim();
                }
            }

            return filtered;
        }
    }
}
