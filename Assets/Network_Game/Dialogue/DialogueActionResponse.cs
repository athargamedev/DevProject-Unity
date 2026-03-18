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

        /// <summary>Absolute scale override. Null = no change.</summary>
        public float? Scale;

        /// <summary>Material color as hex (#RRGGBB) or named color. Null = no change.</summary>
        public string PatchColor;

        /// <summary>Material emission intensity multiplier. Null = no change.</summary>
        public float? Emission;

        /// <summary>Renderer visibility override. Null = no change.</summary>
        public bool? Visible;
    }
}
