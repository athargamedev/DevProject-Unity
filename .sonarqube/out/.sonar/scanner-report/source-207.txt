using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Data-driven animation definition that the LLM can reference by tag name.
    /// Mirrors the EffectDefinition pattern for the [ANIM:] pipeline.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationDefinition", menuName = "Dialogue/Animation Definition")]
    public class AnimationDefinition : ScriptableObject
    {
        [Header("Tag that the LLM must emit")]
        [Tooltip("Exact name used in [ANIM: TagName | Target: Self]")]
        public string animTag;

        [Header("Human-readable description injected into the LLM system prompt")]
        [TextArea(2, 4)]
        public string description;

        [Header("Animator State")]
        [Tooltip("Exact state name in MixamoController to crossfade to")]
        public string stateName;

        [Min(0f)]
        [Tooltip("Crossfade blend duration in seconds")]
        public float crossFadeDuration = 0.12f;

        [Header("Legacy / Aliases")]
        [Tooltip("Alternative tags that also map to this definition")]
        public string[] alternativeTags = new string[0];

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(animTag))
                animTag = name;
        }
    }
}
