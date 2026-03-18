using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DialogueReplicationTrace
    {
        public string TraceId;
        public string ActionId;
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
        public string NetworkPath;
        public bool Success;
        public string Source;
        public string EffectType;
        public string EffectName;
        public ulong SourceNetworkObjectId;
        public ulong TargetNetworkObjectId;
        public string Detail;
        public string Error;
        public int Frame;
        public float RealtimeSinceStartup;
        public string Summary;

        public void RefreshSummary()
        {
            string stage = string.IsNullOrWhiteSpace(Stage) ? "replication" : Stage;
            string path = string.IsNullOrWhiteSpace(NetworkPath) ? string.Empty : $"[{NetworkPath}] ";
            string actionId = string.IsNullOrWhiteSpace(ActionId) ? string.Empty : $" action={ActionId}";
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
            string detail = string.IsNullOrWhiteSpace(Detail) ? string.Empty : $" detail={Detail}";
            string error = string.IsNullOrWhiteSpace(Error) ? string.Empty : $" error={Error}";
            string status = Success ? "ok" : "failed";
            Summary = $"{stage} {path}{request}{actionId}{effect}{target} status={status}{detail}{error}".Trim();
        }
    }
}
