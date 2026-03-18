using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DialogueExecutionTrace
    {
        public string TraceId;
        public string RunId;
        public string BootId;
        public string FlowId;
        public int RequestId;
        public int ClientRequestId;
        public ulong RequestingClientId;
        public ulong SpeakerNetworkId;
        public ulong ListenerNetworkId;
        public string ConversationKey;

        public string Stage;
        public string StageDetail;
        public bool Success;
        public string Source;

        public string EffectType;
        public string EffectName;
        public ulong SourceNetworkObjectId;
        public ulong TargetNetworkObjectId;

        public string ResponsePreview;
        public string Error;
        public int Frame;
        public float RealtimeSinceStartup;
        public string Summary;

        public void RefreshSummary()
        {
            string stage = string.IsNullOrWhiteSpace(Stage) ? "trace" : Stage;
            string detail = string.IsNullOrWhiteSpace(StageDetail) ? string.Empty : $"[{StageDetail}] ";
            string request = RequestId > 0
                ? $"request={RequestId}"
                : ClientRequestId > 0
                    ? $"clientRequest={ClientRequestId}"
                    : "request=none";
            string effect = string.IsNullOrWhiteSpace(EffectName)
                ? string.IsNullOrWhiteSpace(EffectType)
                    ? string.Empty
                    : $" effect={EffectType}"
                : $" effect={EffectName}";
            string target = TargetNetworkObjectId != 0UL ? $" target={TargetNetworkObjectId}" : string.Empty;
            string status = Success ? "ok" : "failed";
            string error = string.IsNullOrWhiteSpace(Error) ? string.Empty : $" error={Error}";
            Summary = $"{stage} {detail}{request}{effect}{target} status={status}{error}".Trim();
        }
    }
}
