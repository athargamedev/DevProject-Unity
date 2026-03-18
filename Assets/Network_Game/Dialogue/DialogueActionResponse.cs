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
        /// <summary>"EFFECT" or "ANIM".</summary>
        public string Type;

        /// <summary>Effect tag or animation tag (e.g. "LightningBolt", "BowGreeting").</summary>
        public string Tag;

        /// <summary>
        /// Resolved target: "Self", a player NameId, or a scene-object name.
        /// Defaults to "Self" when omitted.
        /// </summary>
        public string Target;

        /// <summary>Seconds to wait before firing this action (0 = immediate).</summary>
        public float Delay;
    }
}
