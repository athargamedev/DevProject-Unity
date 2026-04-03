using System;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Parsing helpers for player/ground target tokens.
        private static bool IsPlayerTargetToken(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower
                    is "player"
                        or "listener"
                        or "me"
                        or "myself"
                        or "target"
                        or "hero"
                        or "user"
                || lower.Equals("role:player", StringComparison.Ordinal)
                || lower.Equals("semantic:player", StringComparison.Ordinal)
                || lower.StartsWith("id:player", StringComparison.Ordinal)
                || lower.StartsWith("semantic:player:", StringComparison.Ordinal)
                || lower.StartsWith("player:", StringComparison.Ordinal);
        }

        private static bool LooksLikeExplicitPlayerTargetToken(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            if (TryExtractExplicitPlayerQualifier(lower, out string qualifier))
            {
                lower = qualifier;
            }

            string compact = lower.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);

            if (
                compact is "requester"
                    or "requestingplayer"
                    or "currentplayer"
                    or "localplayer"
                    or "host"
                    or "hostplayer"
                    or "server"
                    or "serverplayer"
                || lower is "player:requester"
                    or "player:self"
                    or "player:me"
                    or "player:host"
                    or "player:server"
            )
            {
                return true;
            }

            return TryParseOrderedPlayerTargetToken(lower, out _)
                || TryParseClientPlayerTargetToken(lower, out _);
        }

        private static bool TryParseOrderedPlayerTargetToken(string lower, out int orderedIndex)
        {
            orderedIndex = 0;
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            if (
                lower.Length > 1
                && lower[0] == 'p'
                && int.TryParse(lower.Substring(1), out orderedIndex)
                && orderedIndex > 0
            )
            {
                return true;
            }

            if (
                lower.StartsWith("player", StringComparison.Ordinal)
                && lower.Length > "player".Length
                && int.TryParse(lower.Substring("player".Length), out orderedIndex)
                && orderedIndex > 0
            )
            {
                return true;
            }

            string[] prefixes = { "player:", "role:player:", "semantic:player:" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (
                    lower.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(lower.Substring(prefix.Length), out orderedIndex)
                    && orderedIndex > 0
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseClientPlayerTargetToken(string lower, out ulong clientId)
        {
            clientId = 0;
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            string[] prefixes = { "client:", "player:client:" };
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (
                    lower.StartsWith(prefix, StringComparison.Ordinal)
                    && ulong.TryParse(lower.Substring(prefix.Length), out clientId)
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGroundAlias(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower
                    is "ground"
                        or "floor"
                        or "terrain"
                        or "grounded"
                        or "fllor"
                        or "flor"
                        or "grond"
                || lower.EndsWith(" ground", StringComparison.Ordinal)
                || lower.EndsWith(" floor", StringComparison.Ordinal)
                || lower.EndsWith(" terrain", StringComparison.Ordinal)
                || lower.Contains("on ground", StringComparison.Ordinal)
                || lower.Contains("at ground", StringComparison.Ordinal)
                || lower.Contains("on floor", StringComparison.Ordinal)
                || lower.Contains("on fllor", StringComparison.Ordinal)
                || lower.Contains("at fllor", StringComparison.Ordinal)
                || lower.Contains("at feet", StringComparison.Ordinal)
                || lower.Contains("under feet", StringComparison.Ordinal);
        }

        private static bool IsPlayerHeadAlias(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower is "head" or "hair" or "face"
                || lower.EndsWith(" head", StringComparison.Ordinal)
                || lower.EndsWith(" hair", StringComparison.Ordinal)
                || lower.EndsWith(" face", StringComparison.Ordinal)
                || lower.Contains("player head", StringComparison.Ordinal)
                || lower.Contains("on head", StringComparison.Ordinal)
                || lower.Contains("at head", StringComparison.Ordinal)
                || lower.Contains("player hair", StringComparison.Ordinal);
        }

        private static bool IsPlayerFeetAlias(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            return lower is "feet" or "foot" or "toes" or "legs" or "atfeet"
                || lower.EndsWith(" feet", StringComparison.Ordinal)
                || lower.EndsWith(" foot", StringComparison.Ordinal)
                || lower.EndsWith(" toes", StringComparison.Ordinal)
                || lower.EndsWith(" legs", StringComparison.Ordinal)
                || lower.Contains("player feet", StringComparison.Ordinal)
                || lower.Contains("at feet", StringComparison.Ordinal)
                || lower.Contains("on feet", StringComparison.Ordinal)
                || lower.Contains("under player", StringComparison.Ordinal);
        }
    }
}
