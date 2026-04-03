using System.Collections.Generic;
using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Central registry for all animation definitions in the dialogue system.
    /// Mirrors EffectCatalog so the LLM can reference Mixamo state names by tag.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationCatalog", menuName = "Dialogue/Animation Catalog")]
    public class AnimationCatalog : ScriptableObject
    {
        public static AnimationCatalog Instance { get; private set; }

        [Header("Animation Definitions")]
        [Tooltip("All available animations the LLM can trigger. Populated manually in the inspector.")]
        public List<AnimationDefinition> allAnimations = new List<AnimationDefinition>();

        [Header("Settings")]
        [Tooltip("Log warnings when LLM uses unknown animation tags")]
        public bool logUnknownTags = true;

        // Runtime lookup cache
        private Dictionary<string, AnimationDefinition> _byTag;
        private Dictionary<string, AnimationDefinition> _byAlternativeTag;
        private bool _isInitialized;

        /// <summary>
        /// Initialize lookup tables. Called automatically when accessed.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized && _byTag != null && _byAlternativeTag != null)
                return;

            _byTag = new Dictionary<string, AnimationDefinition>(System.StringComparer.OrdinalIgnoreCase);
            _byAlternativeTag = new Dictionary<string, AnimationDefinition>(System.StringComparer.OrdinalIgnoreCase);

            if (allAnimations == null)
                allAnimations = new List<AnimationDefinition>();

            foreach (AnimationDefinition def in allAnimations)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.animTag))
                    continue;

                if (!_byTag.ContainsKey(def.animTag))
                    _byTag[def.animTag] = def;

                if (def.alternativeTags != null)
                {
                    foreach (string alt in def.alternativeTags)
                    {
                        if (!string.IsNullOrWhiteSpace(alt) && !_byAlternativeTag.ContainsKey(alt))
                            _byAlternativeTag[alt] = def;
                    }
                }
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Try to find an animation definition by tag (case-insensitive).
        /// </summary>
        public bool TryGet(string tag, out AnimationDefinition def)
        {
            if (!_isInitialized || _byTag == null || _byAlternativeTag == null)
                Initialize();

            if (string.IsNullOrWhiteSpace(tag))
            {
                def = null;
                return false;
            }

            if (_byTag != null && _byTag.TryGetValue(tag, out def))
                return true;

            if (_byAlternativeTag != null && _byAlternativeTag.TryGetValue(tag, out def))
                return true;

            def = null;
            return false;
        }

        /// <summary>
        /// Get all animation tags as a formatted string for LLM prompts.
        /// </summary>
        public string GetPromptCatalog()
        {
            if (!_isInitialized)
                Initialize();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Available Animations:");
            foreach (AnimationDefinition def in allAnimations)
            {
                if (def == null)
                    continue;
                sb.AppendLine($"  [ANIM: {def.animTag} | Target: Self]  — {def.description}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Load the catalog from Resources (path: Resources/Dialogue/AnimationCatalog).
        /// </summary>
        public static AnimationCatalog Load()
        {
            if (Instance != null)
                return Instance;

            Instance = Resources.Load<AnimationCatalog>("Dialogue/AnimationCatalog");
            if (Instance != null)
                Instance.Initialize();

            return Instance;
        }

        /// <summary>
        /// Force rebuild of lookup tables (call after modifying allAnimations at runtime).
        /// </summary>
        public void RebuildLookup()
        {
            _isInitialized = false;
            Initialize();
        }

        private void OnEnable()
        {
            Instance = this;
            Initialize();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
