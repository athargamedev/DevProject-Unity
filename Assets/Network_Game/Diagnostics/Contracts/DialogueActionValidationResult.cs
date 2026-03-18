using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DialogueActionValidationResult
    {
        public string ResultId;
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

        public string ActionKind;
        public string ActionName;
        public string Decision;
        public bool Success;
        public string Source;
        public string Reason;
        public string RequestedTargetHint;
        public ulong ResolvedTargetNetworkObjectId;
        public string RequestedPlacementHint;
        public string ResolvedSpatialType;
        public string SpatialReason;

        public float RequestedScale;
        public float AppliedScale;
        public float RequestedDuration;
        public float AppliedDuration;
        public float RequestedDamageRadius;
        public float AppliedDamageRadius;
        public float RequestedDamageAmount;
        public float AppliedDamageAmount;

        public int Frame;
        public float RealtimeSinceStartup;
        public string Summary;

        public void RefreshSummary()
        {
            string action = string.IsNullOrWhiteSpace(ActionName)
                ? string.IsNullOrWhiteSpace(ActionKind) ? "action" : ActionKind
                : ActionName;
            string actionId = string.IsNullOrWhiteSpace(ActionId) ? string.Empty : $" action={ActionId}";
            string decision = string.IsNullOrWhiteSpace(Decision) ? "recorded" : Decision;
            string request = RequestId > 0
                ? $"request={RequestId}"
                : ClientRequestId > 0
                    ? $"clientRequest={ClientRequestId}"
                    : "request=none";
            string target = ResolvedTargetNetworkObjectId != 0UL
                ? $" target={ResolvedTargetNetworkObjectId}"
                : string.IsNullOrWhiteSpace(RequestedTargetHint)
                    ? string.Empty
                    : $" targetHint={RequestedTargetHint}";
            string reason = string.IsNullOrWhiteSpace(Reason) ? string.Empty : $" reason={Reason}";
            string spatial = string.IsNullOrWhiteSpace(SpatialReason)
                ? string.Empty
                : $" spatial={SpatialReason}";
            string status = Success ? "ok" : "failed";
            Summary = $"{action} decision={decision} {request}{actionId}{target} status={status}{reason}{spatial}".Trim();
        }
    }
}
