using System;
using System.Collections.Generic;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    [CreateAssetMenu(
        fileName = "NpcDialogueProfile",
        menuName = "Network Game/Dialogue/NPC Dialogue Profile"
     )]
    public class NpcDialogueProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string m_ProfileId = "npc.default";

        [SerializeField]
        private string m_DisplayName = "NPC";

        [Header("Personality")]
        [SerializeField]
        [TextArea(5, 12)]
        private string m_SystemPrompt =
            "You are {npc_name}. Keep responses concise, in-character, and useful to the player.";

        [Header("Lore")]
        [SerializeField]
        [TextArea(10, 20)]
        [Tooltip(
            "Comprehensive NPC background and world-building data injected into the system prompt."
         )]
        private string m_Lore = "";

        [Header("Effects")]
        [SerializeField]
        [Tooltip("Effect definitions this NPC can use. Drag EffectDefinition assets from the project.")]
        private EffectDefinition[] m_Effects = new EffectDefinition[0];

        [Header("Dynamic Effect Parameters")]
        [SerializeField]
        [Tooltip("Allow dialogue wording to modulate effect parameters at runtime.")]
        private bool m_EnableDynamicEffectParameters = true;

        [SerializeField]
        [Range(0.25f, 1f)]
        [Tooltip("Lower clamp for dynamic multipliers extracted from dialogue.")]
        private float m_DynamicEffectMinMultiplier = 0.6f;

        [SerializeField]
        [Range(1f, 10f)]
        [Tooltip("Upper clamp for dynamic multipliers extracted from dialogue.")]
        private float m_DynamicEffectMaxMultiplier = 2f;

        public string ProfileId => m_ProfileId;
        public string DisplayName => m_DisplayName;
        public string SystemPrompt => m_SystemPrompt;
        public string Lore => m_Lore;
        public EffectDefinition[] Effects => m_Effects;
        public bool EnableDynamicEffectParameters => m_EnableDynamicEffectParameters;
        public float DynamicEffectMinMultiplier => m_DynamicEffectMinMultiplier;
        public float DynamicEffectMaxMultiplier => m_DynamicEffectMaxMultiplier;

        // ── Static profile cache ──────────────────────────────────────────────

        private static NpcDialogueProfile[] s_ProfileCache;
        private static Dictionary<string, NpcDialogueProfile> s_ProfileById;

        /// <summary>Find all dialogue profiles in Resources folders. Result is cached for the session.</summary>
        public static NpcDialogueProfile[] GetAllProfiles()
        {
            if (s_ProfileCache == null)
                BuildProfileCache();
            return s_ProfileCache;
        }

        /// <summary>Find profile by its ProfileId. O(1) dictionary lookup after first call.</summary>
        public static NpcDialogueProfile GetProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;
            if (s_ProfileById == null)
                BuildProfileCache();
            return s_ProfileById.TryGetValue(profileId, out var hit) ? hit : null;
        }

        private static void BuildProfileCache()
        {
            s_ProfileCache = Resources.LoadAll<NpcDialogueProfile>("");
            s_ProfileById = new Dictionary<string, NpcDialogueProfile>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var p in s_ProfileCache)
                if (p != null && !string.IsNullOrWhiteSpace(p.m_ProfileId))
                    s_ProfileById.TryAdd(p.m_ProfileId, p);
        }

        private void OnEnable()
        {
            s_ProfileCache = null;
            s_ProfileById = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            s_ProfileCache = null;
            s_ProfileById = null;
        }
#endif
    }
}
