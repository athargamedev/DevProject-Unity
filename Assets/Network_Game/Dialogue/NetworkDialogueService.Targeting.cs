using System;
using System.Collections.Generic;
using Network_Game.Auth;
using Network_Game.Combat;
using Network_Game.Core;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    public partial class NetworkDialogueService
    {
        private GameObject ResolveSpawnedObject(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                networkObjectId == 0
                || manager == null
                || !manager.IsListening
                || manager.SpawnManager == null
            )
            {
                return null;
            }

            if (
                manager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject
                )
            )
            {
                return networkObject.gameObject;
            }

            return null;
        }

        private NpcDialogueActor ResolveDialogueActor(ulong speakerNetworkId)
        {
            GameObject speakerObject = ResolveSpawnedObject(speakerNetworkId);
            if (speakerObject == null)
            {
                return null;
            }

            return speakerObject.GetComponent<NpcDialogueActor>();
        }

        private NpcDialogueActor ResolveDialogueActorForRequest(
            DialogueRequest request,
            out ulong resolvedSpeakerNetworkId,
            out ulong resolvedListenerNetworkId,
            out bool usedListenerFallback
        )
        {
            resolvedSpeakerNetworkId = request.SpeakerNetworkId;
            resolvedListenerNetworkId = request.ListenerNetworkId;
            usedListenerFallback = false;

            NpcDialogueActor speakerActor = ResolveDialogueActor(request.SpeakerNetworkId);
            if (speakerActor != null && speakerActor.Profile == null)
            {
                speakerActor.TryAutoAssignProfileFromName();
            }
            if (speakerActor != null && speakerActor.Profile != null)
            {
                return speakerActor;
            }

            NpcDialogueActor listenerActor = ResolveDialogueActor(request.ListenerNetworkId);
            if (listenerActor != null && listenerActor.Profile == null)
            {
                listenerActor.TryAutoAssignProfileFromName();
            }
            if (listenerActor != null && listenerActor.Profile != null)
            {
                resolvedSpeakerNetworkId = request.ListenerNetworkId;
                resolvedListenerNetworkId = request.SpeakerNetworkId;
                usedListenerFallback = request.SpeakerNetworkId != request.ListenerNetworkId;
                if (m_LogDebug)
                {
                    NGLog.Debug(
                        "Dialogue",
                        NGLog.Format(
                            "Resolved NPC speaker fallback",
                            ("originalSpeaker", request.SpeakerNetworkId),
                            ("originalListener", request.ListenerNetworkId),
                            ("resolvedSpeaker", resolvedSpeakerNetworkId),
                            ("resolvedListener", resolvedListenerNetworkId)
                        )
                    );
                }
                return listenerActor;
            }

            if (speakerActor != null)
            {
                return speakerActor;
            }

            if (listenerActor != null)
            {
                resolvedSpeakerNetworkId = request.ListenerNetworkId;
                resolvedListenerNetworkId = request.SpeakerNetworkId;
                usedListenerFallback = request.SpeakerNetworkId != request.ListenerNetworkId;
                return listenerActor;
            }

            return null;
        }

        private void RebuildPlayerPromptContextLookup()
        {
            m_PlayerPromptContextByNetworkId.Clear();
            if (m_PlayerPromptContextBindings == null)
            {
                m_PlayerPromptContextBindings = new List<PlayerPromptContextBinding>();
                return;
            }

            for (int i = 0; i < m_PlayerPromptContextBindings.Count; i++)
            {
                PlayerPromptContextBinding binding = m_PlayerPromptContextBindings[i];
                if (binding == null || !binding.Enabled || binding.PlayerNetworkId == 0)
                {
                    continue;
                }

                binding.NameId = string.IsNullOrWhiteSpace(binding.NameId)
                    ? string.Empty
                    : binding.NameId.Trim();
                binding.CustomizationJson = string.IsNullOrWhiteSpace(binding.CustomizationJson)
                    ? "{}"
                    : binding.CustomizationJson.Trim();
                m_PlayerPromptContextByNetworkId[binding.PlayerNetworkId] = binding;
            }
        }

        private void RebuildPlayerIdentityLookup()
        {
            m_PlayerIdentityByClientId.Clear();
            m_PlayerIdentityByNetworkId.Clear();
            if (m_PlayerIdentityBindings == null)
            {
                m_PlayerIdentityBindings = new List<PlayerIdentityBinding>();
                return;
            }

            for (int i = 0; i < m_PlayerIdentityBindings.Count; i++)
            {
                PlayerIdentityBinding binding = m_PlayerIdentityBindings[i];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                binding.NameId = string.IsNullOrWhiteSpace(binding.NameId)
                    ? string.Empty
                    : binding.NameId.Trim();
                binding.CustomizationJson = NormalizePromptContextJson(
                    binding.NameId,
                    binding.CustomizationJson
                );
                if (string.IsNullOrWhiteSpace(binding.LastUpdatedUtc))
                {
                    binding.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
                }

                if (binding.ClientId != ulong.MaxValue)
                {
                    m_PlayerIdentityByClientId[binding.ClientId] = binding;
                }

                if (binding.PlayerNetworkId != 0)
                {
                    m_PlayerIdentityByNetworkId[binding.PlayerNetworkId] = binding;
                }
            }
        }

        private void SynthesizeIdentityBindingsFromRuntimeBindings()
        {
            foreach (var promptBinding in m_PlayerPromptContextByNetworkId.Values)
            {
                if (
                    promptBinding == null
                    || !promptBinding.Enabled
                    || promptBinding.PlayerNetworkId == 0
                )
                {
                    continue;
                }

                UpsertPlayerIdentity(
                    ResolveOwnerClientIdForPlayerNetworkId(promptBinding.PlayerNetworkId),
                    promptBinding.PlayerNetworkId,
                    promptBinding.NameId,
                    promptBinding.CustomizationJson
                );
            }
        }

        private PlayerPromptContextBinding FindOrCreatePlayerPromptContextBinding(
            ulong playerNetworkId
        )
        {
            if (m_PlayerPromptContextBindings == null)
            {
                m_PlayerPromptContextBindings = new List<PlayerPromptContextBinding>();
            }

            for (int i = 0; i < m_PlayerPromptContextBindings.Count; i++)
            {
                PlayerPromptContextBinding existing = m_PlayerPromptContextBindings[i];
                if (existing == null || existing.PlayerNetworkId != playerNetworkId)
                {
                    continue;
                }

                return existing;
            }

            var created = new PlayerPromptContextBinding
            {
                PlayerNetworkId = playerNetworkId,
                NameId = string.Empty,
                CustomizationJson = "{}",
                Enabled = true,
            };
            m_PlayerPromptContextBindings.Add(created);
            return created;
        }

        private PlayerIdentityBinding FindOrCreatePlayerIdentityBinding(
            ulong clientId,
            ulong playerNetworkId
        )
        {
            if (m_PlayerIdentityBindings == null)
            {
                m_PlayerIdentityBindings = new List<PlayerIdentityBinding>();
            }

            if (
                clientId != ulong.MaxValue
                && m_PlayerIdentityByClientId.TryGetValue(clientId, out var byClient)
            )
            {
                return byClient;
            }

            if (
                playerNetworkId != 0
                && m_PlayerIdentityByNetworkId.TryGetValue(playerNetworkId, out var byPlayer)
            )
            {
                return byPlayer;
            }

            for (int i = 0; i < m_PlayerIdentityBindings.Count; i++)
            {
                PlayerIdentityBinding existing = m_PlayerIdentityBindings[i];
                if (existing == null)
                {
                    continue;
                }

                if (
                    (clientId != ulong.MaxValue && existing.ClientId == clientId)
                    || (playerNetworkId != 0 && existing.PlayerNetworkId == playerNetworkId)
                )
                {
                    return existing;
                }
            }

            var created = new PlayerIdentityBinding
            {
                ClientId = clientId,
                PlayerNetworkId = playerNetworkId,
                NameId = string.Empty,
                CustomizationJson = "{}",
                Enabled = true,
                LastUpdatedUtc = DateTime.UtcNow.ToString("o"),
            };
            m_PlayerIdentityBindings.Add(created);
            return created;
        }

        private void UpsertPlayerIdentity(
            ulong clientId,
            ulong playerNetworkId,
            string nameId,
            string customizationJson
        )
        {
            PlayerIdentityBinding identity = FindOrCreatePlayerIdentityBinding(
                clientId,
                playerNetworkId
            );
            if (identity == null)
            {
                return;
            }

            ulong oldClientId = identity.ClientId;
            ulong oldPlayerNetworkId = identity.PlayerNetworkId;
            string oldNameId = identity.NameId ?? string.Empty;
            string oldCustomization = identity.CustomizationJson ?? "{}";

            if (clientId != ulong.MaxValue)
            {
                identity.ClientId = clientId;
            }

            if (playerNetworkId != 0)
            {
                identity.PlayerNetworkId = playerNetworkId;
            }

            if (!string.IsNullOrWhiteSpace(nameId))
            {
                string normalizedName = nameId.Trim();
                bool incomingIsPlaceholder = normalizedName.StartsWith(
                    "client_",
                    StringComparison.OrdinalIgnoreCase
                );
                if (string.IsNullOrWhiteSpace(identity.NameId) || !incomingIsPlaceholder)
                {
                    identity.NameId = normalizedName;
                }
            }

            if (!string.IsNullOrWhiteSpace(customizationJson))
            {
                identity.CustomizationJson = NormalizePromptContextJson(
                    identity.NameId,
                    customizationJson
                );
            }

            identity.Enabled = true;
            identity.LastUpdatedUtc = DateTime.UtcNow.ToString("o");

            if (identity.ClientId != ulong.MaxValue)
            {
                m_PlayerIdentityByClientId[identity.ClientId] = identity;
            }

            if (identity.PlayerNetworkId != 0)
            {
                m_PlayerIdentityByNetworkId[identity.PlayerNetworkId] = identity;
            }

            if (m_LogDebug)
            {
                bool changed =
                    oldClientId != identity.ClientId
                    || oldPlayerNetworkId != identity.PlayerNetworkId
                    || !string.Equals(oldNameId, identity.NameId, StringComparison.Ordinal)
                    || !string.Equals(
                        oldCustomization,
                        identity.CustomizationJson,
                        StringComparison.Ordinal
                    );

                if (changed)
                {
                    NGLog.Info(
                        "Dialogue",
                        NGLog.Format(
                            "Player identity updated",
                            ("clientId", identity.ClientId),
                            ("playerNetworkId", identity.PlayerNetworkId),
                            ("name_id", identity.NameId ?? string.Empty),
                            (
                                "hasCustomization",
                                !IsPlaceholderPromptContextJson(identity.CustomizationJson)
                            )
                        )
                    );
                }
            }
        }

        private PlayerIdentityBinding ResolvePlayerIdentityForRequest(DialogueRequest request)
        {
            if (request.RequestingClientId != 0)
            {
                if (
                    m_PlayerIdentityByClientId.TryGetValue(
                        request.RequestingClientId,
                        out PlayerIdentityBinding byClient
                    )
                    && byClient != null
                    && byClient.Enabled
                )
                {
                    return byClient;
                }
            }

            ulong playerNetworkId = ResolvePlayerNetworkIdForRequest(request);
            if (
                playerNetworkId != 0
                && m_PlayerIdentityByNetworkId.TryGetValue(
                    playerNetworkId,
                    out PlayerIdentityBinding byNetwork
                )
                && byNetwork != null
                && byNetwork.Enabled
            )
            {
                return byNetwork;
            }

            if (
                request.RequestingClientId != 0
                && TryGetPlayerNetworkObjectIdForClient(
                    request.RequestingClientId,
                    out ulong resolvedPlayerNetworkId
                )
            )
            {
                if (
                    m_PlayerIdentityByNetworkId.TryGetValue(
                        resolvedPlayerNetworkId,
                        out PlayerIdentityBinding fallback
                    )
                    && fallback != null
                    && fallback.Enabled
                )
                {
                    return fallback;
                }
            }

            return null;
        }

        private ulong ResolveOwnerClientIdForPlayerNetworkId(ulong playerNetworkId)
        {
            if (playerNetworkId == 0 || NetworkManager.Singleton?.SpawnManager == null)
            {
                return ulong.MaxValue;
            }

            if (
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkId,
                    out NetworkObject playerObject
                )
                || playerObject == null
            )
            {
                return ulong.MaxValue;
            }

            return playerObject.OwnerClientId;
        }

        private static PlayerIdentitySnapshot ToSnapshot(PlayerIdentityBinding identity)
        {
            return new PlayerIdentitySnapshot
            {
                ClientId = identity.ClientId,
                PlayerNetworkId = identity.PlayerNetworkId,
                NameId = identity.NameId ?? string.Empty,
                CustomizationJson = identity.CustomizationJson ?? "{}",
                LastUpdatedUtc = identity.LastUpdatedUtc ?? string.Empty,
            };
        }

        private PlayerPromptContextBinding ResolvePlayerPromptContextBinding(
            DialogueRequest request
        )
        {
            if (!m_EnablePlayerPromptContext)
            {
                return null;
            }

            ulong playerNetworkId = ResolvePlayerNetworkIdForRequest(request);
            if (playerNetworkId == 0)
            {
                return null;
            }

            return m_PlayerPromptContextByNetworkId.TryGetValue(playerNetworkId, out var binding)
                ? binding
                : null;
        }

        private string BuildPlayerContextPrompt(DialogueRequest request, GameObject listenerObject)
        {
            if (!m_EnablePlayerPromptContext)
            {
                return string.Empty;
            }

            PlayerPromptContextBinding binding = ResolvePlayerPromptContextBinding(request);
            PlayerIdentityBinding identity = ResolvePlayerIdentityForRequest(request);
            string nameId = identity?.NameId;
            if (string.IsNullOrWhiteSpace(nameId))
            {
                nameId = binding?.NameId ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(nameId))
            {
                LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
                if (authService != null && authService.HasCurrentPlayer)
                {
                    nameId = authService.CurrentPlayer.NameId;
                }
            }

            if (string.IsNullOrWhiteSpace(nameId) && listenerObject != null)
            {
                nameId = listenerObject.name;
            }

            string customizationJson = identity?.CustomizationJson;
            if (string.IsNullOrWhiteSpace(customizationJson))
            {
                customizationJson = binding?.CustomizationJson ?? "{}";
            }
            if (string.IsNullOrWhiteSpace(customizationJson))
            {
                customizationJson = "{}";
            }

            if (IsPlaceholderPromptContextJson(customizationJson))
            {
                LocalPlayerAuthService authService = LocalPlayerAuthService.Instance;
                if (
                    authService != null
                    && authService.HasCurrentPlayer
                    && (
                        string.IsNullOrWhiteSpace(nameId)
                        || string.Equals(
                            authService.CurrentPlayer.NameId,
                            nameId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    customizationJson = authService.GetCustomizationJson();
                }
            }

            customizationJson = NormalizePromptContextJson(nameId, customizationJson);

            int maxChars = Mathf.Max(64, m_MaxPlayerCustomizationChars);
            if (UseOpenAIRemote)
            {
                maxChars = Mathf.Min(maxChars, Mathf.Max(64, m_RemoteMaxPlayerCustomizationChars));
            }
            if (customizationJson.Length > maxChars)
            {
                customizationJson = customizationJson.Substring(0, maxChars).TrimEnd() + "...";
            }

            ulong playerNetworkId =
                identity?.PlayerNetworkId != 0
                    ? identity.PlayerNetworkId
                    : ResolvePlayerNetworkIdForRequest(request);
            ulong clientId = request.RequestingClientId;
            if (clientId == 0 && identity != null)
            {
                clientId = identity.ClientId;
            }

            string playerRole = clientId == 0 ? "host" : "client";

            NpcDialogueActor narrativeActor = ResolveDialogueActorForRequest(
                request,
                out _,
                out _,
                out _
            );
            string narrativeHints = BuildNarrativeHints(
                identity,
                narrativeActor?.Profile,
                customizationJson
            );
            string playerContextBlock =
                "[PlayerContext]\n"
                + $"name_id: {nameId}\n"
                + $"client_id: {clientId}\n"
                + $"player_network_id: {playerNetworkId}\n"
                + $"player_role: {playerRole}\n"
                + $"customization_json: {customizationJson}\n"
                + "Use this context to personalize details naturally without exposing internal formatting.";
            if (!string.IsNullOrEmpty(narrativeHints))
            {
                playerContextBlock += "\n\n" + narrativeHints;
            }
            return playerContextBlock;
        }

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

        /// <summary>
        /// Parses a player's CustomizationJson to build per-player effect modifiers.
        /// Applied server-side before ClampDynamicMultiplier so NPC profile bounds still govern extremes.
        /// </summary>
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

            // Clamp to safe ranges
            mod.DamageScaleReceived = Mathf.Clamp(mod.DamageScaleReceived, 0f, 3f);
            mod.EffectSizeScale = Mathf.Clamp(mod.EffectSizeScale, 0.25f, 4f);
            mod.EffectDurationScale = Mathf.Clamp(mod.EffectDurationScale, 0.25f, 4f);
            mod.AggressionBias = Mathf.Clamp(mod.AggressionBias, 0.25f, 3f);
            return mod;
        }

        /// <summary>
        /// Lightweight JSON string field reader. Looks for "key":"value" or "key": "value".
        /// Returns null if key is not found.
        /// </summary>
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

        /// <summary>
        /// Lightweight JSON float field reader. Returns defaultValue if key is not found or not parseable.
        /// </summary>
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
            // Skip whitespace
            int valueStart = colonIdx + 1;
            while (
                valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t')
            )
            {
                valueStart++;
            }
            // Handle quoted float string ("1.5")
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
            // Unquoted number
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

        /// <summary>
        /// Converts a color theme keyword to a Unity Color.
        /// Returns null if not recognized.
        /// </summary>
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

        /// <summary>
        /// Converts player customization data and the NPC's profile into English roleplay hints
        /// injected into the NPC system prompt so the LLM reasons about the player's traits.
        /// </summary>
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

            // Player class / archetype greeting
            string playerClass = TryReadJsonString(customizationJson, "class");
            if (!string.IsNullOrWhiteSpace(playerClass))
            {
                hints.Append(
                    $"- This player is a {playerClass}; open your greeting with lore that fits their archetype.\n"
                );
                hasHints = true;
            }

            // NPC-specific reputation (5-tier), with fallback to legacy binary relationship
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
                    // Fallback: legacy binary relationship field
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

            // Inventory
            string inventoryRaw = TryReadJsonString(customizationJson, "inventory_tags");
            if (!string.IsNullOrWhiteSpace(inventoryRaw))
            {
                hints.Append(
                    $"- The player carries: {inventoryRaw}. Reference these items naturally if relevant.\n"
                );
                hasHints = true;
            }

            // Quest flags
            string questRaw = TryReadJsonString(customizationJson, "quest_flags");
            if (!string.IsNullOrWhiteSpace(questRaw))
            {
                hints.Append(
                    $"- Player story flags: {questRaw}. Use these to advance or acknowledge the narrative.\n"
                );
                hasHints = true;
            }

            // Last in-world action
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

        private ulong ResolvePlayerNetworkIdForRequest(DialogueRequest request)
        {
            // Prefer the requesting client's player for per-player targeting/context.
            if (
                (request.RequestingClientId != 0 || request.IsUserInitiated)
                && TryGetPlayerNetworkObjectIdForClient(
                    request.RequestingClientId,
                    out ulong requesterPlayerNetworkId
                )
            )
            {
                return requesterPlayerNetworkId;
            }

            if (IsConnectedClientPlayerObject(request.ListenerNetworkId))
            {
                return request.ListenerNetworkId;
            }

            if (IsConnectedClientPlayerObject(request.SpeakerNetworkId))
            {
                return request.SpeakerNetworkId;
            }

            return 0;
        }

        private ulong ResolvePreferredListenerTargetNetworkObjectId(DialogueRequest request)
        {
            ulong resolvedPlayerNetworkId = ResolvePlayerNetworkIdForRequest(request);
            if (resolvedPlayerNetworkId != 0)
            {
                return resolvedPlayerNetworkId;
            }

            if (request.ListenerNetworkId != 0)
            {
                return request.ListenerNetworkId;
            }

            return request.SpeakerNetworkId;
        }

        private GameObject ResolvePreferredListenerTargetObject(DialogueRequest request)
        {
            ulong networkObjectId = ResolvePreferredListenerTargetNetworkObjectId(request);
            return networkObjectId != 0 ? ResolveSpawnedObject(networkObjectId) : null;
        }

        private bool TryResolveExplicitPlayerTargetNetworkObjectId(
            string targetHint,
            DialogueRequest request,
            out ulong playerNetworkObjectId
        )
        {
            playerNetworkObjectId = 0;
            if (string.IsNullOrWhiteSpace(targetHint))
            {
                return false;
            }

            string lower = targetHint.Trim().ToLowerInvariant();
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
                || lower is "player:requester" or "player:self" or "player:me"
            )
            {
                playerNetworkObjectId = ResolvePlayerNetworkIdForRequest(request);
                return playerNetworkObjectId != 0;
            }

            if (
                compact is "host" or "hostplayer" or "server" or "serverplayer"
                || lower is "player:host" or "player:server"
            )
            {
                return TryGetPlayerNetworkObjectIdForClient(
                    NetworkManager.ServerClientId,
                    out playerNetworkObjectId
                );
            }

            if (TryParseClientPlayerTargetToken(lower, out ulong targetClientId))
            {
                return TryGetPlayerNetworkObjectIdForClient(
                    targetClientId,
                    out playerNetworkObjectId
                );
            }

            if (TryParseOrderedPlayerTargetToken(lower, out int orderedIndex))
            {
                return TryResolveOrderedPlayerTargetNetworkObjectId(
                    orderedIndex,
                    out playerNetworkObjectId
                );
            }

            return false;
        }

        private static bool TryExtractExplicitPlayerQualifier(string lower, out string qualifier)
        {
            qualifier = string.Empty;
            if (string.IsNullOrWhiteSpace(lower))
            {
                return false;
            }

            string[] suffixes =
            {
                " head",
                " hair",
                " face",
                " feet",
                " foot",
                " toes",
                " legs",
                " ground",
                " floor",
                " terrain",
            };

            for (int i = 0; i < suffixes.Length; i++)
            {
                string suffix = suffixes[i];
                if (!lower.EndsWith(suffix, StringComparison.Ordinal) || lower.Length <= suffix.Length)
                {
                    continue;
                }

                qualifier = lower.Substring(0, lower.Length - suffix.Length).Trim();
                return !string.IsNullOrWhiteSpace(qualifier);
            }

            return false;
        }

        private bool TryResolveOrderedPlayerTargetNetworkObjectId(
            int orderedIndex,
            out ulong playerNetworkObjectId
        )
        {
            playerNetworkObjectId = 0;
            if (orderedIndex <= 0)
            {
                return false;
            }

            List<EffectTargetResolverService.PlayerTarget> orderedPlayers =
                new List<EffectTargetResolverService.PlayerTarget>(8);
            EffectTargetResolverService.GetOrderedPlayers(orderedPlayers, includeDead: true);
            for (int i = 0; i < orderedPlayers.Count; i++)
            {
                EffectTargetResolverService.PlayerTarget playerTarget = orderedPlayers[i];
                if (
                    playerTarget == null
                    || playerTarget.OrderedIndex != orderedIndex
                    || playerTarget.NetworkObject == null
                )
                {
                    continue;
                }

                playerNetworkObjectId = playerTarget.NetworkObject.NetworkObjectId;
                return playerNetworkObjectId != 0;
            }

            return false;
        }

        private bool TryResolveSingleConnectedPlayerFallback(out ulong playerNetworkId)
        {
            playerNetworkId = 0;
            if (
                NetworkManager.Singleton == null
                || NetworkManager.Singleton.ConnectedClients == null
                || NetworkManager.Singleton.ConnectedClients.Count == 0
            )
            {
                return false;
            }

            ulong resolved = 0;
            int matchedCount = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (
                    TryGetPlayerNetworkObjectIdForClient(
                        client.ClientId,
                        out ulong candidatePlayerNetworkId
                    )
                )
                {
                    matchedCount++;
                    resolved = candidatePlayerNetworkId;
                    if (matchedCount > 1)
                    {
                        break;
                    }
                }
            }

            if (matchedCount == 1 && resolved != 0)
            {
                playerNetworkId = resolved;
                return true;
            }

            return false;
        }

        private bool IsConnectedClientPlayerObject(ulong networkObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                networkObjectId == 0
                || manager == null
                || !manager.IsListening
                || manager.ConnectedClients == null
            )
            {
                return false;
            }

            foreach (var client in manager.ConnectedClients.Values)
            {
                if (client?.PlayerObject == null)
                {
                    continue;
                }

                if (client.PlayerObject.NetworkObjectId == networkObjectId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPlayerNetworkObjectIdForClient(ulong clientId, out ulong playerNetworkId)
        {
            playerNetworkId = 0;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || manager.ConnectedClients == null)
            {
                return false;
            }

            if (
                manager.ConnectedClients.TryGetValue(clientId, out var client)
                && client?.PlayerObject != null
            )
            {
                playerNetworkId = client.PlayerObject.NetworkObjectId;
                return true;
            }

            return false;
        }

        private bool IsObjectOwnedByClient(ulong networkObjectId, ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                networkObjectId == 0
                || manager == null
                || !manager.IsListening
                || manager.SpawnManager == null
            )
            {
                return false;
            }

            if (
                !manager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out NetworkObject networkObject
                )
            )
            {
                return false;
            }

            return networkObject != null && networkObject.OwnerClientId == clientId;
        }

        private bool TryValidateClientDialogueParticipants(
            ulong senderClientId,
            ulong speakerNetworkId,
            ulong listenerNetworkId,
            out string rejectionReason
        )
        {
            rejectionReason = null;
            bool hasSenderPlayer = TryGetPlayerNetworkObjectIdForClient(
                senderClientId,
                out ulong senderPlayerNetworkId
            );

            if (speakerNetworkId == 0 && listenerNetworkId == 0)
            {
                rejectionReason = "participants_missing";
                return false;
            }

            bool senderOwnsSpeaker = IsObjectOwnedByClient(speakerNetworkId, senderClientId);
            bool senderOwnsListener = IsObjectOwnedByClient(listenerNetworkId, senderClientId);
            if (!hasSenderPlayer && !senderOwnsSpeaker && !senderOwnsListener)
            {
                rejectionReason = "requester_player_missing";
                return false;
            }

            bool senderMatchesPlayer =
                hasSenderPlayer
                && (
                    speakerNetworkId == senderPlayerNetworkId
                    || listenerNetworkId == senderPlayerNetworkId
                );
            if (!senderMatchesPlayer && !senderOwnsSpeaker && !senderOwnsListener)
            {
                rejectionReason = "invalid_participants";
                return false;
            }

            return true;
        }

        private bool IsConversationKeyVisibleToClient(string conversationKey, ulong senderClientId)
        {
            if (string.IsNullOrWhiteSpace(conversationKey))
            {
                return false;
            }

            bool hasSenderPlayer = TryGetPlayerNetworkObjectIdForClient(
                senderClientId,
                out ulong senderPlayerNetworkId
            );

            string key = conversationKey.Trim();
            string clientScopePrefix = $"client:{senderClientId}";
            if (
                string.Equals(key, clientScopePrefix, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith($"{clientScopePrefix}:", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }

            if (
                key.StartsWith("actor:", StringComparison.OrdinalIgnoreCase)
                && ulong.TryParse(key.Substring("actor:".Length), out ulong actorId)
            )
            {
                return (hasSenderPlayer && actorId == senderPlayerNetworkId)
                    || IsObjectOwnedByClient(actorId, senderClientId);
            }

            int firstColon = key.IndexOf(':');
            int secondColon = firstColon >= 0 ? key.IndexOf(':', firstColon + 1) : -1;
            if (
                firstColon > 0
                && secondColon < 0
                && ulong.TryParse(key.Substring(0, firstColon), out ulong first)
                && ulong.TryParse(key.Substring(firstColon + 1), out ulong second)
            )
            {
                return (
                        hasSenderPlayer
                        && (first == senderPlayerNetworkId || second == senderPlayerNetworkId)
                    )
                    || IsObjectOwnedByClient(first, senderClientId)
                    || IsObjectOwnedByClient(second, senderClientId);
            }

            return false;
        }

    }
}
