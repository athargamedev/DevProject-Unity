using System;
using Network_Game.Dialogue.Effects;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        // Customization JSON parsing, narrative hints, and player effect modifiers.
        private static string NormalizePromptContextJson(string nameId, string customizationJson)
        {
            string trimmed = string.IsNullOrWhiteSpace(customizationJson)
                ? "{}"
                : customizationJson.Trim();
            if (!IsPlaceholderPromptContextJson(trimmed))
            {
                return trimmed;
            }

            string resolvedName = string.IsNullOrWhiteSpace(nameId)
                ? "player_local"
                : nameId.Trim();
            return "{"
                + $"\"name_id\":\"{EscapeJsonString(resolvedName)}\","
                + "\"customization\":{},"
                + "\"source\":\"network_context\""
                + "}";
        }

        private static bool IsPlaceholderPromptContextJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            string trimmed = json.Trim();
            return trimmed == "{}" || trimmed == "{ }" || trimmed == "null";
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private static PlayerEffectModifier BuildPlayerEffectModifier(
            PlayerIdentityBinding identity
        )
        {
            if (identity == null || string.IsNullOrWhiteSpace(identity.CustomizationJson))
            {
                return PlayerEffectModifier.Neutral;
            }

            PlayerEffectModifier mod = PlayerEffectModifier.Neutral;
            string json = identity.CustomizationJson;

            mod.DamageScaleReceived = TryReadJsonFloat(json, "vulnerability", 1f);
            mod.EffectSizeScale = TryReadJsonFloat(json, "effect_size_bias", 1f);
            mod.EffectDurationScale = TryReadJsonFloat(json, "effect_duration_bias", 1f);
            mod.AggressionBias = TryReadJsonFloat(json, "aggression_bias", 1f);

            string shieldStr = TryReadJsonString(json, "has_shield");
            mod.IsShielded =
                string.Equals(shieldStr, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(shieldStr, "1", StringComparison.Ordinal);

            mod.PreferredElement = TryReadJsonString(json, "element_affinity");

            string colorTheme = TryReadJsonString(json, "color_theme");
            if (!string.IsNullOrWhiteSpace(colorTheme))
            {
                mod.PreferredColor = ParseColorFromTheme(colorTheme);
            }

            mod.DamageScaleReceived = Mathf.Clamp(mod.DamageScaleReceived, 0f, 3f);
            mod.EffectSizeScale = Mathf.Clamp(mod.EffectSizeScale, 0.25f, 4f);
            mod.EffectDurationScale = Mathf.Clamp(mod.EffectDurationScale, 0.25f, 4f);
            mod.AggressionBias = Mathf.Clamp(mod.AggressionBias, 0.25f, 3f);
            return mod;
        }

        private static string TryReadJsonString(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }
            string search = $"\"{key}\"";
            int keyIdx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0)
            {
                return null;
            }
            int colonIdx = json.IndexOf(':', keyIdx + search.Length);
            if (colonIdx < 0)
            {
                return null;
            }
            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0)
            {
                return null;
            }
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0)
            {
                return null;
            }
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private static float TryReadJsonFloat(string json, string key, float defaultValue = 1f)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }
            string search = $"\"{key}\"";
            int keyIdx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0)
            {
                return defaultValue;
            }
            int colonIdx = json.IndexOf(':', keyIdx + search.Length);
            if (colonIdx < 0)
            {
                return defaultValue;
            }
            int valueStart = colonIdx + 1;
            while (
                valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t')
            )
            {
                valueStart++;
            }
            if (valueStart < json.Length && json[valueStart] == '"')
            {
                int qEnd = json.IndexOf('"', valueStart + 1);
                if (qEnd > valueStart)
                {
                    string quoted = json.Substring(valueStart + 1, qEnd - valueStart - 1);
                    if (
                        float.TryParse(
                            quoted,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float qv
                        )
                    )
                    {
                        return qv;
                    }
                }
                return defaultValue;
            }
            int valueEnd = valueStart;
            while (
                valueEnd < json.Length
                && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '.' || json[valueEnd] == '-')
            )
            {
                valueEnd++;
            }
            if (valueEnd > valueStart)
            {
                string numStr = json.Substring(valueStart, valueEnd - valueStart);
                if (
                    float.TryParse(
                        numStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float v
                    )
                )
                {
                    return v;
                }
            }
            return defaultValue;
        }

        private static Color? ParseColorFromTheme(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                return null;
            }
            switch (theme.Trim().ToLowerInvariant())
            {
                case "red":
                    return new Color(0.9f, 0.15f, 0.15f);
                case "blue":
                    return new Color(0.15f, 0.4f, 0.95f);
                case "green":
                    return new Color(0.1f, 0.8f, 0.2f);
                case "yellow":
                    return new Color(1f, 0.9f, 0.1f);
                case "orange":
                    return new Color(1f, 0.5f, 0.05f);
                case "purple":
                    return new Color(0.6f, 0.1f, 0.9f);
                case "white":
                    return Color.white;
                case "black":
                    return new Color(0.1f, 0.1f, 0.1f);
                case "fire":
                    return new Color(1f, 0.4f, 0.05f);
                case "ice":
                    return new Color(0.4f, 0.8f, 1f);
                case "storm":
                    return new Color(0.5f, 0.5f, 0.9f);
                case "void":
                    return new Color(0.2f, 0f, 0.3f);
                case "nature":
                    return new Color(0.2f, 0.7f, 0.2f);
                case "water":
                    return new Color(0.1f, 0.5f, 0.9f);
                case "earth":
                    return new Color(0.5f, 0.35f, 0.1f);
                case "mystic":
                    return new Color(0.7f, 0.3f, 0.9f);
                default:
                    if (ColorUtility.TryParseHtmlString(theme, out Color parsed))
                    {
                        return parsed;
                    }
                    return null;
            }
        }

        private static string BuildNarrativeHints(
            PlayerIdentityBinding identity,
            NpcDialogueProfile npcProfile,
            string customizationJson
        )
        {
            if (string.IsNullOrWhiteSpace(customizationJson) || customizationJson.Trim() == "{}")
            {
                return string.Empty;
            }

            var hints = new System.Text.StringBuilder();
            hints.Append("[NarrativeHints]\n");
            bool hasHints = false;

            string element = TryReadJsonString(customizationJson, "element_affinity");
            if (!string.IsNullOrWhiteSpace(element))
            {
                hints.Append(
                    $"- This player has a {element} affinity; reference {element}-themed imagery in your response.\n"
                );
                hasHints = true;
            }

            string colorTheme = TryReadJsonString(customizationJson, "color_theme");
            if (!string.IsNullOrWhiteSpace(colorTheme))
            {
                hints.Append(
                    $"- The player's color theme is {colorTheme}; prefer {colorTheme}-toned effects.\n"
                );
                hasHints = true;
            }

            float aggressionBias = TryReadJsonFloat(customizationJson, "aggression_bias", 1f);
            if (aggressionBias >= 1.3f)
            {
                hints.Append(
                    "- You are hostile to this player; use menacing language and cast aggressive effects.\n"
                );
                hasHints = true;
            }
            else if (aggressionBias <= 0.7f)
            {
                hints.Append(
                    "- You are friendly toward this player; use warm, supportive language.\n"
                );
                hasHints = true;
            }

            string shieldStr = TryReadJsonString(customizationJson, "has_shield");
            bool hasShield =
                string.Equals(shieldStr, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(shieldStr, "1", StringComparison.Ordinal);
            if (hasShield)
            {
                hints.Append(
                    "- This player has an active shield; acknowledge their defenses in your response.\n"
                );
                hasHints = true;
            }

            float vulnerability = TryReadJsonFloat(customizationJson, "vulnerability", 1f);
            if (vulnerability >= 1.4f)
            {
                hints.Append(
                    "- This player is vulnerable; effects may hit harder than expected.\n"
                );
                hasHints = true;
            }

            string playerClass = TryReadJsonString(customizationJson, "class");
            if (!string.IsNullOrWhiteSpace(playerClass))
            {
                hints.Append(
                    $"- This player is a {playerClass}; open your greeting with lore that fits their archetype.\n"
                );
                hasHints = true;
            }

            if (npcProfile != null && !string.IsNullOrWhiteSpace(npcProfile.ProfileId))
            {
                string repKey = $"reputation_{npcProfile.ProfileId}";
                string repStr = TryReadJsonString(customizationJson, repKey);
                if (!string.IsNullOrWhiteSpace(repStr) && int.TryParse(repStr, out int rep))
                {
                    rep = Mathf.Clamp(rep, 0, 100);
                    string repHint = rep switch
                    {
                        < 20 =>
                            $"- This player has deeply wronged you (reputation {rep}/100); be cold, suspicious, and unwilling to help.\n",
                        < 40 =>
                            $"- This player is distrusted (reputation {rep}/100); keep them at arm's length and be guarded.\n",
                        < 60 =>
                            $"- This player is a neutral acquaintance (reputation {rep}/100); treat them professionally.\n",
                        < 80 =>
                            $"- This player has earned your goodwill (reputation {rep}/100); be warm and willing to share extra lore.\n",
                        _ =>
                            $"- This player is a trusted champion (reputation {rep}/100); speak with reverence and share your deepest secrets.\n",
                    };
                    hints.Append(repHint);
                    hasHints = true;
                }
                else
                {
                    string relationship = TryReadJsonString(
                        customizationJson,
                        $"relationship_{npcProfile.ProfileId}"
                    );
                    if (string.Equals(relationship, "hostile", StringComparison.OrdinalIgnoreCase))
                    {
                        hints.Append(
                            "- Your relationship with this player is hostile; act accordingly.\n"
                        );
                        hasHints = true;
                    }
                    else if (
                        string.Equals(relationship, "ally", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        hints.Append("- This player is your ally; protect and empower them.\n");
                        hasHints = true;
                    }
                }
            }

            string inventoryRaw = TryReadJsonString(customizationJson, "inventory_tags");
            if (!string.IsNullOrWhiteSpace(inventoryRaw))
            {
                hints.Append(
                    $"- The player carries: {inventoryRaw}. Reference these items naturally if relevant.\n"
                );
                hasHints = true;
            }

            string questRaw = TryReadJsonString(customizationJson, "quest_flags");
            if (!string.IsNullOrWhiteSpace(questRaw))
            {
                hints.Append(
                    $"- Player story flags: {questRaw}. Use these to advance or acknowledge the narrative.\n"
                );
                hasHints = true;
            }

            string lastAction = TryReadJsonString(customizationJson, "last_action");
            if (!string.IsNullOrWhiteSpace(lastAction))
            {
                hints.Append(
                    $"- The player recently: {lastAction}. React to this in your opening line if appropriate.\n"
                );
                hasHints = true;
            }

            hints.Append(
                "- Do not reference internal identifiers or JSON formatting in your response."
            );

            return hasHints ? hints.ToString() : string.Empty;
        }
    }
}
