using System;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Shared heuristic policy used by both runtime playback and ML-Agents training.
    /// This keeps the production fallback and the training target aligned.
    /// </summary>
    public static class DialogueAnimationDecisionPolicy
    {
        public readonly struct AnimationIntent
        {
            public AnimationIntent(DialogueAnimationAction action, string target, string rawTag = null)
            {
                Action = action;
                Target = string.IsNullOrWhiteSpace(target) ? "Self" : target.Trim();
                RawTag = rawTag ?? string.Empty;
            }

            public DialogueAnimationAction Action { get; }
            public string Target { get; }

            /// <summary>
            /// The raw action token from the LLM tag. Non-empty when <see cref="IsCatalogTag"/> is true,
            /// meaning the token did not match any <see cref="DialogueAnimationAction"/> enum value and
            /// must be resolved against an <see cref="AnimationCatalog"/> at the call site.
            /// </summary>
            public string RawTag { get; }

            /// <summary>
            /// True when the LLM emitted an unrecognized action token that may exist in the
            /// <see cref="AnimationCatalog"/>. The caller should look up <see cref="RawTag"/> there.
            /// </summary>
            public bool IsCatalogTag => !string.IsNullOrEmpty(RawTag);

            public bool TargetsSelf =>
                string.Equals(Target, "Self", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Target, "NPC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Target, "Speaker", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsEffectTag(string responseText)
        {
            return !string.IsNullOrWhiteSpace(responseText)
                && responseText.IndexOf("[EFFECT:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ContainsAnimationTag(string responseText)
        {
            return !string.IsNullOrWhiteSpace(responseText)
                && responseText.IndexOf("[ANIM:", StringComparison.OrdinalIgnoreCase) >= 0;
        }


        public static bool TryParseFirstAnimationTag(
            string responseText,
            out AnimationIntent intent)
        {
            intent = default;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return false;
            }

            int start = responseText.IndexOf("[ANIM:", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return false;
            }

            int contentStart = start + 6;
            int end = responseText.IndexOf(']', contentStart);
            if (end <= contentStart)
            {
                return false;
            }

            string content = responseText.Substring(contentStart, end - contentStart).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            string[] segments = content.Split('|');
            string actionToken = segments[0].Trim();
            bool knownAction = TryMapAction(actionToken, out DialogueAnimationAction action);

            string target = "Self";
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                int separator = segment.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string key = segment.Substring(0, separator).Trim();
                string value = segment.Substring(separator + 1).Trim();
                if (key.Equals("Target", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    target = value;
                }
            }

            if (knownAction)
            {
                intent = new AnimationIntent(action, target);
            }
            else
            {
                // Unknown token — pass it up as a catalog tag so the caller can look it up.
                intent = new AnimationIntent(DialogueAnimationAction.HoldNeutral, target, rawTag: actionToken);
            }

            return true;
        }

        public static string StripAnimationTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string stripped = text;
            int start = stripped.IndexOf("[ANIM:", StringComparison.OrdinalIgnoreCase);
            while (start >= 0)
            {
                int end = stripped.IndexOf(']', start);
                if (end < 0)
                {
                    break;
                }

                stripped = stripped.Remove(start, end - start + 1).Trim();
                start = stripped.IndexOf("[ANIM:", StringComparison.OrdinalIgnoreCase);
            }

            return stripped;
        }


        private static bool TryMapAction(string token, out DialogueAnimationAction action)
        {
            action = DialogueAnimationAction.HoldNeutral;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "hold":
                case "holdneutral":
                case "neutral":
                case "idle":
                    action = DialogueAnimationAction.HoldNeutral;
                    return true;

                case "idlevariant":
                case "idlevariant1":
                case "idle shift":
                case "idle_shift":
                    action = DialogueAnimationAction.IdleVariant;
                    return true;

                case "turnleft":
                case "leftturn":
                case "left turn":
                case "lookleft":
                    action = DialogueAnimationAction.TurnLeft;
                    return true;

                case "turnright":
                case "rightturn":
                case "right turn":
                case "lookright":
                    action = DialogueAnimationAction.TurnRight;
                    return true;

                case "emphasisreact":
                case "emphasis":
                case "hitreaction":
                case "hit reaction":
                case "big body blow":
                case "body blow":
                case "blow":
                    action = DialogueAnimationAction.EmphasisReact;
                    return true;
            }

            return Enum.TryParse(token.Trim(), true, out action);
        }
    }
}
