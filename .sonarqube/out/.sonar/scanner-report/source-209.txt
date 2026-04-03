using System.Collections.Generic;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Structured response returned by the LLM in the unified action pipeline.
    /// <c>Speech</c> is shown in the chat bubble; <c>Actions</c> is dispatched
    /// server-side for animations and effects.
    /// </summary>
    public class DialogueActionResponse
    {
        /// <summary>User-facing spoken text (shown in the speech bubble).</summary>
        public string Speech;

        /// <summary>
        /// Optional list of animation / effect actions to dispatch.
        /// May be null or empty when the LLM has nothing to perform.
        /// </summary>
        public List<DialogueAction> Actions;
    }

    /// <summary>
    /// A single timed action emitted by the LLM: either an animation on the
    /// speaker ("ANIM") or a particle/projectile effect ("EFFECT").
    /// </summary>
    public class DialogueAction
    {
        /// <summary>"EFFECT", "ANIM", or "PATCH".</summary>
        public string Type;

        /// <summary>Effect tag, animation tag, or target object name for PATCH.</summary>
        public string Tag;

        /// <summary>
        /// Resolved target: "Self", a player NameId, or a scene-object name.
        /// Defaults to "Self" when omitted.
        /// </summary>
        public string Target;

        /// <summary>Seconds to wait before firing this action (0 = immediate).</summary>
        public float Delay;

        // ── PATCH-only property manipulation fields ────────────────────────────

        /// <summary>Health delta: negative = damage, positive = heal. Null = no change.</summary>
        public float? HealthDelta;

        /// <summary>World-space position offset [x, y, z]. Null = no movement.</summary>
        public float[] PositionOffset;

        /// <summary>
        /// PATCH: absolute scale override. EFFECT: scale multiplier (1.0 = definition default).
        /// Null = no change / use definition default.
        /// </summary>
        public float? Scale;

        /// <summary>Material color as hex (#RRGGBB) or named color. Null = no change.</summary>
        public string PatchColor;

        /// <summary>Material emission intensity multiplier. Null = no change.</summary>
        public float? Emission;

        /// <summary>Renderer visibility override. Null = no change.</summary>
        public bool? Visible;

        // ── EFFECT-only parameter fields ───────────────────────────────────────
        // Note: Scale (above) is shared — for PATCH it is an absolute override;
        // for EFFECT it is a scale multiplier (1.0 = definition default).

        /// <summary>Effect intensity multiplier. Null = use definition default.</summary>
        public float? Intensity;

        /// <summary>Effect duration override in seconds. Null = use definition default.</summary>
        public float? Duration;

        /// <summary>Projectile speed override in m/s. Null = use definition default.</summary>
        public float? Speed;

        /// <summary>AoE radius override in metres. Null = use definition default.</summary>
        public float? Radius;

        /// <summary>Damage multiplier relative to base. Null = use definition default.</summary>
        public float? Damage;

        /// <summary>
        /// Effect color as hex (#RRGGBB or #RRGGBBAA) or named color (e.g. "red").
        /// Null = use definition default.
        /// </summary>
        public string EffectColor;

        /// <summary>
        /// Emotional tone keyword (e.g. "epic", "peaceful", "threatening").
        /// Used to modulate effect intensity heuristics.
        /// </summary>
        public string Emotion;

        /// <summary>
        /// Optional variant key selecting a prefab variant or parameter preset defined
        /// on the EffectDefinition (e.g. "sky", "ground", "intense", "subtle").
        /// Null or empty = use the base prefab and defaults.
        /// </summary>
        public string Variant;
    }
}
