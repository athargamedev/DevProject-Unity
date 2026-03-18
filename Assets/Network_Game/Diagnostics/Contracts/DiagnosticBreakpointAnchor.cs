using System;

namespace Network_Game.Diagnostics
{
    [Serializable]
    public struct DiagnosticBreakpointAnchor
    {
        public string Id;
        public string DisplayName;
        public string Location;
        public string Summary;
        public string RelativeFilePath;
        public string SearchHint;
    }

    public static class DiagnosticBreakpointAnchors
    {
        public const string DialogueValidationEffectParser = "dialogue.validation.effect_parser";
        public const string DialogueValidationSpecialEffect = "dialogue.validation.special_effect";
        public const string DialogueDispatchEffectRpc = "dialogue.dispatch.effect_rpc";
        public const string DialogueRpcReceiveEffect = "dialogue.rpc.receive_effect";
        public const string DialogueSceneEffectApply = "dialogue.scene_effect.apply";
        public const string DialogueSceneEffectVisible = "dialogue.scene_effect.visible";
        public const string DialogueFailureGeneric = "dialogue.failure.generic";

        private static readonly DiagnosticBreakpointAnchor[] s_All =
        {
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueValidationEffectParser,
                DisplayName = "Effect Parser Validation",
                Location = "NetworkDialogueService.ApplyEffectParserIntents",
                Summary = "Server-side validation and spatial resolution for catalog-driven prefab powers.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/NetworkDialogueService.cs",
                SearchHint = "private void ApplyEffectParserIntents(",
            },
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueValidationSpecialEffect,
                DisplayName = "Special Effect Validation",
                Location = "NetworkDialogueService.ApplyPlayerSpecialEffects",
                Summary = "Server-side validation for dissolve, floor dissolve, and respawn effects.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/NetworkDialogueService.cs",
                SearchHint = "private bool ApplyPlayerSpecialEffects(",
            },
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueDispatchEffectRpc,
                DisplayName = "Effect RPC Dispatch",
                Location = "NetworkDialogueService.Apply*ClientRpc call sites",
                Summary = "Server dispatch point where validated effects are sent to clients over NGO RPC.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/NetworkDialogueService.cs",
                SearchHint = "ApplyPrefabPowerEffectClientRpc(",
            },
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueRpcReceiveEffect,
                DisplayName = "Effect RPC Receive",
                Location = "NetworkDialogueService.Apply*ClientRpc handlers",
                Summary = "Client RPC handler entry where effect delivery is observed before scene application.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/NetworkDialogueService.cs",
                SearchHint = "private void ApplyDissolveEffectClientRpc(",
            },
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueSceneEffectApply,
                DisplayName = "Scene Effect Apply",
                Location = "DialogueSceneEffectsController.Apply*Effect methods",
                Summary = "Effect controller application path for prefab powers, dissolve, respawn, and surface material changes.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/Effects/DialogueSceneEffectsController.cs",
                SearchHint = "public void ApplyPrefabPower(",
            },
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueSceneEffectVisible,
                DisplayName = "Scene Effect Visible",
                Location = "DialogueSceneEffectsController.EmitEffectApplied",
                Summary = "Client-visible effect emission point used to confirm the final visible result.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/Effects/DialogueSceneEffectsController.cs",
                SearchHint = "private static void EmitEffectApplied(AppliedEffectInfo info)",
            },
            new DiagnosticBreakpointAnchor
            {
                Id = DialogueFailureGeneric,
                DisplayName = "Dialogue Failure Fallback",
                Location = "NetworkDialogueService / DialogueSceneEffectsController failing stage",
                Summary = "Fallback anchor when the failure stage is known but no more specific anchor fits.",
                RelativeFilePath = "Assets/Network_Game/Dialogue/NetworkDialogueService.cs",
                SearchHint = "RecordExecutionTrace(",
            },
        };

        public static DiagnosticBreakpointAnchor[] GetAll()
        {
            DiagnosticBreakpointAnchor[] copy = new DiagnosticBreakpointAnchor[s_All.Length];
            Array.Copy(s_All, copy, s_All.Length);
            return copy;
        }

        public static bool TryGet(string id, out DiagnosticBreakpointAnchor anchor)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                anchor = default;
                return false;
            }

            for (int i = 0; i < s_All.Length; i++)
            {
                if (string.Equals(s_All[i].Id, id, StringComparison.Ordinal))
                {
                    anchor = s_All[i];
                    return true;
                }
            }

            anchor = default;
            return false;
        }
    }
}
