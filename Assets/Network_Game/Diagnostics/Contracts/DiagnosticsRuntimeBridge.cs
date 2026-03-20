namespace Network_Game.Diagnostics
{
    public interface IDiagnosticsRuntimeBridge
    {
        bool TryGetAuthoritySnapshot(out AuthoritySnapshot snapshot);
        bool TryGetSceneSnapshot(out AuthoritativeSceneSnapshot snapshot);
        AuthoritativeSceneSnapshot BuildSceneSnapshot(int maxObjects, float maxDistance);
        bool TryGetLatestInferenceEnvelope(out DialogueInferenceEnvelope envelope);
        void RecordInferenceEnvelope(DialogueInferenceEnvelope envelope);
        bool TryGetLatestDialogueExecutionTrace(out DialogueExecutionTrace trace);
        void RecordDialogueExecutionTrace(DialogueExecutionTrace trace);
        DialogueExecutionTrace[] GetRecentDialogueExecutionTraces();
        bool TryGetLatestDialogueActionValidationResult(out DialogueActionValidationResult result);
        void RecordDialogueActionValidationResult(DialogueActionValidationResult result);
        DialogueActionValidationResult[] GetRecentDialogueActionValidationResults();
        bool TryGetLatestDialogueReplicationTrace(out DialogueReplicationTrace trace);
        void RecordDialogueReplicationTrace(DialogueReplicationTrace trace);
        DialogueReplicationTrace[] GetRecentDialogueReplicationTraces();
        bool TryGetLatestUiBehaviorSnapshot(string uiId, out UIBehaviorSnapshot snapshot);
        void RecordUiBehaviorSnapshot(UIBehaviorSnapshot snapshot);
        bool TryGetLatestUiPerformanceSample(string uiId, out UIPerformanceSample sample);
        void RecordUiPerformanceSample(UIPerformanceSample sample);
        bool TryGetDiagnosticBrainPacket(out DiagnosticBrainPacket packet);
        string BuildDiagnosticBrainPrompt();
        void UpsertBrainVariable(DiagnosticBrainVariable variable);
    }

    public static class DiagnosticsRuntimeBridgeRegistry
    {
        public static IDiagnosticsRuntimeBridge Current { get; private set; }

        public static void Register(IDiagnosticsRuntimeBridge bridge)
        {
            Current = bridge;
        }

        public static void Unregister(IDiagnosticsRuntimeBridge bridge)
        {
            if (ReferenceEquals(Current, bridge))
            {
                Current = null;
            }
        }
    }
}
