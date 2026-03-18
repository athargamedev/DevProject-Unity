using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DialogueInferenceEnvelope
    {
        public string EnvelopeId;
        public string FlowId;
        public int RequestId;
        public int ClientRequestId;
        public ulong RequestingClientId;
        public ulong SpeakerNetworkId;
        public ulong ListenerNetworkId;
        public string ConversationKey;

        public string BackendName;
        public string EndpointLabel;
        public string ModelName;

        public string SystemPromptId;
        public string PromptTemplateId;
        public string PromptTemplateVersion;

        public string SceneSnapshotId;
        public string SceneSnapshotHash;

        public float Temperature;
        public float TopP;
        public int TopK;
        public float FrequencyPenalty;
        public float PresencePenalty;
        public float RepeatPenalty;
        public int MaxTokens;
        public string[] StopSequences;

        public int PromptCharCount;
        public int SceneSnapshotCharCount;
        public int PromptTokenEstimate;

        public float EnqueuedAt;
        public float StartedAt;
        public int RetryCount;

        public string PromptPreview;
    }
}
