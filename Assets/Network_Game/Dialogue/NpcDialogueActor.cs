using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network_Game.Combat;
using Network_Game.Diagnostics;
using Network_Game.Dialogue.Effects;
using Unity.Netcode;
using UnityEngine;

namespace Network_Game.Dialogue
{
    /// <summary>
    /// Consolidated per-NPC dialogue component.
    /// - Persona (system prompt + profile selection)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NpcDialogueActor : NetworkBehaviour
    {
        [Header("Persona")]
        [SerializeField]
        private NpcDialogueProfile m_Profile;

        [SerializeField]
        [Tooltip("Optional runtime id override when multiple NPCs share one profile asset.")]
        private string m_ProfileIdOverride = "";

        private static string s_CachedSceneContext;
        private static float s_SceneContextCacheTime = -1f;
        private const float SceneContextCacheDuration = 5f;

        // Capabilities guide cache — rebuilt only when listenerName changes.
        private string m_CachedCapabilitiesGuide;
        private string m_CachedCapabilitiesListenerName;

        // Per-NPC runtime effect lookup built from m_Profile.Effects in Awake.
        private Dictionary<string, EffectDefinition> m_EffectLookup;

        // Enhanced LLM reasoning context
        [Header("LLM Reasoning Context")]
        [Tooltip("Enable enhanced context for better effect decisions")]
        [SerializeField]
        private bool m_EnableEnhancedContext = true;

        [Tooltip("Enable target state tracking (health, distance, active effects)")]
        [SerializeField]
        private bool m_EnableTargetContext = true;

        [Tooltip("Enable conversation memory (effect history, escalation)")]
        [SerializeField]
        private bool m_EnableConversationMemory = true;

        // Runtime context tracking
        private EffectDecisionContext m_DecisionContext;
        private DialogueTargetContext m_TargetContext;
        private string m_CurrentConversationKey;
        private int m_CurrentExchangeNumber;

        public NpcDialogueProfile Profile => m_Profile;

        public string ProfileId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(m_ProfileIdOverride))
                {
                    return m_ProfileIdOverride.Trim();
                }

                if (m_Profile != null && !string.IsNullOrWhiteSpace(m_Profile.ProfileId))
                {
                    return m_Profile.ProfileId.Trim();
                }

                return gameObject.name;
            }
        }

        private void Awake()
        {
            BuildEffectLookup();
            InitializeEnhancedContext();
        }

        /// <summary>
        /// Initialize enhanced context tracking for LLM reasoning.
        /// </summary>
        private void InitializeEnhancedContext()
        {
            if (m_EnableConversationMemory)
            {
                m_DecisionContext = new EffectDecisionContext();
                m_DecisionContext.BeginConversation("greeting", 0.3f);
            }

            if (m_EnableTargetContext)
            {
                m_TargetContext = new DialogueTargetContext();
            }

            m_CurrentExchangeNumber = 0;
        }

        /// <summary>
        /// Builds the per-NPC runtime effect lookup from the profile's EffectDefinition array.
        /// Primary tags and all alternative tags are indexed for O(1) resolution.
        /// </summary>
        private void BuildEffectLookup()
        {
            m_EffectLookup = new Dictionary<string, EffectDefinition>(StringComparer.OrdinalIgnoreCase);
            if (m_Profile?.Effects == null || m_Profile.Effects.Length == 0)
                return;

            int count = 0;
            foreach (var def in m_Profile.Effects)
            {
                if (def == null || !def.enabled || string.IsNullOrWhiteSpace(def.effectTag))
                    continue;
                m_EffectLookup.TryAdd(def.effectTag.Trim(), def);
                if (def.alternativeTags != null)
                    foreach (var alt in def.alternativeTags)
                        if (!string.IsNullOrWhiteSpace(alt))
                            m_EffectLookup.TryAdd(alt.Trim(), def);
                count++;
            }

            NGLog.Info(
                "DialogueFX",
                $"[NpcDialogueActor] Built effect lookup: {count} effect(s), {m_EffectLookup.Count} tag(s) | npc={gameObject.name}"
            );
        }

        /// <summary>Exact-match effect lookup against this NPC's profile effects.</summary>
        public bool TryGetEffect(string tag, out EffectDefinition def)
        {
            def = null;
            if (m_EffectLookup == null || string.IsNullOrWhiteSpace(tag))
                return false;
            return m_EffectLookup.TryGetValue(tag.Trim(), out def);
        }

        /// <summary>Fuzzy word-overlap lookup against this NPC's profile effects.</summary>
        public bool TryFuzzyGetEffect(string tag, out EffectDefinition def, out string matchedTag)
        {
            def = null;
            matchedTag = null;
            if (m_EffectLookup == null || string.IsNullOrWhiteSpace(tag))
                return false;

            string[] queryWords = SplitTagWords(tag);
            if (queryWords.Length == 0)
                return false;

            float bestScore = 0f;
            EffectDefinition bestDef = null;
            string bestKey = null;
            foreach (var kv in m_EffectLookup)
            {
                float score = ComputeWordOverlap(queryWords, SplitTagWords(kv.Key));
                if (score > bestScore) { bestScore = score; bestDef = kv.Value; bestKey = kv.Key; }
            }

            if (bestScore >= 0.4f && bestDef != null)
            {
                def = bestDef;
                matchedTag = bestKey;
                return true;
            }
            return false;
        }

        /// <summary>Returns all enabled effects on this NPC's profile.</summary>
        public IEnumerable<EffectDefinition> GetAllEffects()
        {
            if (m_Profile?.Effects == null) yield break;
            foreach (var def in m_Profile.Effects)
                if (def != null && def.enabled) yield return def;
        }

        public string BuildSystemPrompt(
            string basePrompt,
            string userPrompt,
            GameObject listenerObject,
            string listenerNameId = null
        )
        {
            if (m_Profile == null || string.IsNullOrWhiteSpace(m_Profile.SystemPrompt))
            {
                return basePrompt ?? string.Empty;
            }

            string listenerName = !string.IsNullOrWhiteSpace(listenerNameId)
                ? listenerNameId
                : (listenerObject != null ? listenerObject.name : "Player");

            // NOTE: {player_input} was intentionally removed here.
            // The user's message is already sent separately as the userPrompt parameter
            // in the LLM chat request. Injecting it into the system prompt duplicated
            // tokens and created a prompt injection surface.
            string personaPrompt = m_Profile
                .SystemPrompt.Replace("{npc_name}", gameObject.name ?? "NPC")
                .Replace("{listener_name}", listenerName);

            if (!string.IsNullOrWhiteSpace(m_Profile.Lore))
            {
                string lore = m_Profile
                    .Lore.Replace("{npc_name}", gameObject.name ?? "NPC")
                    .Replace("{listener_name}", listenerName);
                personaPrompt = $"{personaPrompt}\n\n[Lore]\n{lore}";
            }

            string composedPrompt = string.IsNullOrWhiteSpace(basePrompt)
                ? personaPrompt
                : $"{basePrompt}\n\n[Persona:{ProfileId}]\n{personaPrompt}";
            string capabilitiesGuide = BuildNpcCapabilitiesGuide(listenerName, listenerObject);
            if (!string.IsNullOrWhiteSpace(capabilitiesGuide))
            {
                composedPrompt = $"{composedPrompt}\n\n{capabilitiesGuide}";
            }

            return composedPrompt;
        }

        /// <summary>
        /// Unified NPC capabilities guide that replaces the former effect-guide and
        /// animation-guide. Describes the JSON response format once, then lists all
        /// available EFFECT and ANIM actions together with per-request listener context.
        /// </summary>
        private string BuildNpcCapabilitiesGuide(string listenerName, GameObject listenerObject)
        {
            if (m_Profile == null)
                return string.Empty;

            // Return the cached guide when the listener hasn't changed.
            // This avoids reloading and iterating the effect/animation catalogs on every LLM request.
            // The cache is intentionally keyed only on listenerName — the spatial label (distance/
            // direction) may be slightly stale between turns, which is acceptable.
            if (m_CachedCapabilitiesGuide != null && m_CachedCapabilitiesListenerName == listenerName)
                return m_CachedCapabilitiesGuide;

            var sb = new StringBuilder(900);

            // ── Response format instruction ──────────────────────────────────────
            sb.AppendLine("[Response format]");
            sb.AppendLine("Reply ONLY with a JSON object: {\"speech\":\"...\",\"actions\":[...]}");
            sb.AppendLine("Action types:");
            sb.AppendLine("  Spawn effect:  {\"type\":\"EFFECT\",\"tag\":\"EFFECT_NAME\",\"target\":\"Self\",\"delay\":0}");
            sb.AppendLine("  Spawn on object: {\"type\":\"EFFECT\",\"tag\":\"EFFECT_NAME\",\"target\":\"OBJECT_NAME\",\"delay\":0}");
            sb.AppendLine("  EFFECT target: \"Self\"=this NPC, \"player\"=the player, or any scene object name from the scene list below.");
            sb.AppendLine("  In multiplayer, you may also target \"requester\", \"host\", \"p1\", \"p2\", or \"client:CLIENT_ID\" for a specific player.");
            sb.AppendLine("  Play anim:     {\"type\":\"ANIM\",\"tag\":\"ANIM_NAME\",\"target\":\"Self\",\"delay\":0}");
            sb.AppendLine("  Special effect on player: {\"type\":\"EFFECT\",\"tag\":\"dissolve\",\"target\":\"player\",\"delay\":0}");
            sb.AppendLine("  Special effect on floor:  {\"type\":\"EFFECT\",\"tag\":\"floor_dissolve\",\"target\":\"ground\",\"delay\":0}");
            sb.AppendLine("  Special effect respawn:   {\"type\":\"EFFECT\",\"tag\":\"respawn\",\"target\":\"player\",\"delay\":0}");
            sb.AppendLine("  Modify object (PATCH) — MUST include property fields:");
            sb.AppendLine("    Make invisible: {\"type\":\"PATCH\",\"tag\":\"Self\",\"visible\":false}");
            sb.AppendLine("    Make visible:   {\"type\":\"PATCH\",\"tag\":\"Self\",\"visible\":true}");
            sb.AppendLine("    Glow red:       {\"type\":\"PATCH\",\"tag\":\"Self\",\"color\":\"red\",\"emission\":2.0}");
            sb.AppendLine("    Change color:   {\"type\":\"PATCH\",\"tag\":\"Self\",\"color\":\"blue\"}");
            sb.AppendLine("    Scale up:       {\"type\":\"PATCH\",\"tag\":\"Self\",\"scale\":2.0}");
            sb.AppendLine("    Damage player:  {\"type\":\"PATCH\",\"tag\":\"player\",\"health\":-25}");
            sb.AppendLine("  PATCH tag: \"Self\"=this NPC, \"player\"=the player, or any scene object name from the scene list below.");
            sb.AppendLine("  Never use PATCH for dissolve, floor_dissolve, or respawn. Those are EFFECT actions.");
            sb.AppendLine();

            // ── Per-request listener context ─────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(listenerName))
            {
                sb.Append($"[Listener] The player you are speaking to is named \"{listenerName}\".");
                if (listenerObject != null && listenerObject.transform != null)
                {
                    Vector3 toListener = listenerObject.transform.position - transform.position;
                    float dist = toListener.magnitude;
                    string distLabel = dist < 2f ? "very close" : dist < 5f ? "nearby" : dist < 12f ? "a few metres away" : "far away";
                    Vector3 fwd = transform.forward;
                    float dot = Vector3.Dot(fwd, toListener.normalized);
                    string dirLabel = dot > 0.5f ? "in front of you" : dot < -0.5f ? "behind you" : "to your side";
                    sb.Append($" They are {distLabel}, {dirLabel}.");
                }
                sb.AppendLine();
                sb.AppendLine();
            }

            // ── Available ANIM actions ────────────────────────────────────────────
            if (GetComponent<NpcDialogueAnimationController>() != null)
            {
                sb.AppendLine("[Available animations — target must be Self]");
                sb.AppendLine("  EmphasisReact — heavy blow, hit reaction, or forceful emphasis.");
                sb.AppendLine("  IdleVariant   — calm flourish or friendly idle shift.");
                sb.AppendLine("  TurnLeft      — curious or questioning look to the left.");
                sb.AppendLine("  TurnRight     — curious or questioning look to the right.");

                AnimationCatalog animCatalog = AnimationCatalog.Instance ?? AnimationCatalog.Load();
                if (animCatalog != null && animCatalog.allAnimations != null && animCatalog.allAnimations.Count > 0)
                {
                    foreach (AnimationDefinition def in animCatalog.allAnimations)
                    {
                        if (def == null || string.IsNullOrWhiteSpace(def.animTag))
                            continue;
                        sb.AppendLine($"  {def.animTag} — {def.description}");
                    }
                }
                sb.AppendLine();
            }

            // ── Available EFFECT actions (from profile EffectDefinitions) ────────
            if (m_Profile.Effects != null && m_Profile.Effects.Length > 0)
            {
                sb.AppendLine("[Available effects — use in EFFECT actions]");
                foreach (var def in m_Profile.Effects)
                {
                    if (def == null || !def.enabled || string.IsNullOrWhiteSpace(def.effectTag))
                        continue;
                    sb.Append($"  {def.effectTag}");
                    if (!string.IsNullOrWhiteSpace(def.description))
                        sb.Append($" — {def.description}");
                    if (!string.IsNullOrWhiteSpace(def.element))
                        sb.Append($" [{def.element}]");
                    string effectHints = BuildEffectParameterHints(def);
                    if (!string.IsNullOrWhiteSpace(effectHints))
                        sb.Append($" ({effectHints})");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            string sceneInfo = BuildSceneContextInfo();
            if (!string.IsNullOrWhiteSpace(sceneInfo))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine(sceneInfo.Trim());
            }

            // Add enhanced reasoning context if enabled
            if (m_EnableEnhancedContext)
            {
                string enhancedContext = BuildEnhancedContextSection(listenerName, listenerObject);
                if (!string.IsNullOrWhiteSpace(enhancedContext))
                {
                    sb.AppendLine();
                    sb.AppendLine(enhancedContext);
                }
            }

            m_CachedCapabilitiesGuide = sb.ToString().Trim();
            m_CachedCapabilitiesListenerName = listenerName;
            return m_CachedCapabilitiesGuide;
        }

        /// <summary>
        /// Build the enhanced context section with reasoning guidance for the LLM.
        /// </summary>
        private string BuildEnhancedContextSection(string listenerName, GameObject listenerObject)
        {
            var sb = new StringBuilder();

            // Conversation memory context
            if (m_EnableConversationMemory && m_DecisionContext != null)
            {
                sb.AppendLine(m_DecisionContext.BuildContextPrompt());
            }

            // Target state context
            if (m_EnableTargetContext && m_TargetContext != null && !string.IsNullOrWhiteSpace(listenerName))
            {
                sb.AppendLine($"[Target State — \"{listenerName}\"]");
                sb.AppendLine(m_TargetContext.ToPromptString());
                sb.AppendLine();
            }

            // Enhanced effect guidance
            if (m_Profile?.Effects != null && m_Profile.Effects.Length > 0)
            {
                sb.AppendLine(BuildEnhancedEffectGuidance());
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Build enhanced effect guidance with relationships and situational advice.
        /// </summary>
        private string BuildEnhancedEffectGuidance()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Effect Reasoning Guide]");

            // Build category index
            var categoryEffects = new Dictionary<string, List<EffectDefinition>>();
            foreach (var def in m_Profile.Effects)
            {
                if (def == null || !def.enabled) continue;
                
                if (def.categories != null)
                {
                    foreach (var category in def.categories)
                    {
                        if (!categoryEffects.ContainsKey(category))
                            categoryEffects[category] = new List<EffectDefinition>();
                        categoryEffects[category].Add(def);
                    }
                }
            }

            // Category guidance
            if (categoryEffects.Count > 0)
            {
                sb.AppendLine("Effect Categories:");
                foreach (var kvp in categoryEffects.OrderBy(k => k.Key))
                {
                    var effectNames = kvp.Value.Select(e => e.effectTag).ToList();
                    sb.AppendLine($"  {kvp.Key}: {string.Join(", ", effectNames)}");
                }
                sb.AppendLine();
            }

            // Per-effect detailed guidance
            var effectsWithGuidance = m_Profile.Effects
                .Where(e => e != null && e.enabled && HasEffectGuidance(e))
                .Take(5); // Limit to avoid token explosion

            foreach (var def in effectsWithGuidance)
            {
                sb.AppendLine($"{def.effectTag}:");
                
                // Situational triggers
                if (def.situationalTriggers.useWhenTargetLowHealth)
                    sb.AppendLine($"  ✓ USE WHEN: Target health < 25% (finisher)");
                if (def.situationalTriggers.useWhenTargetFullHealth)
                    sb.AppendLine($"  ✓ USE WHEN: Target health > 75% (opener)");
                if (def.situationalTriggers.preferSingleTarget)
                    sb.AppendLine($"  ✓ USE WHEN: Single target");
                if (def.situationalTriggers.preferMultipleTargets)
                    sb.AppendLine($"  ✓ USE WHEN: Multiple targets/AOE");
                if (def.situationalTriggers.optimalRangeMax > 0)
                    sb.AppendLine($"  ✓ RANGE: {def.situationalTriggers.optimalRangeMin:F0}-{def.situationalTriggers.optimalRangeMax:F0}m");
                if (def.situationalTriggers.requiresOutdoor)
                    sb.AppendLine($"  ⚠ REQUIRES: Outdoor space");

                // Parameter semantics
                sb.AppendLine($"  SCALE: subtle({def.scaleSemantics.subtle:F1}x) / normal({def.scaleSemantics.normal:F1}x) / dramatic({def.scaleSemantics.dramatic:F1}x) / epic({def.scaleSemantics.epic:F1}x)");
                
                // Relationships
                if (def.synergisticEffects?.Length > 0)
                {
                    var synergyNames = def.synergisticEffects.Where(e => e != null).Select(e => e.effectTag);
                    sb.AppendLine($"  COMBOS WITH: {string.Join(", ", synergyNames)}");
                }
                
                if (def.alternativeEffects?.Length > 0)
                {
                    var altNames = def.alternativeEffects.Where(e => e != null).Select(e => e.effectTag);
                    sb.AppendLine($"  ALTERNATIVES: {string.Join(", ", altNames)}");
                }

                sb.AppendLine();
            }

            // Usage restrictions
            var restrictedEffects = m_Profile.Effects
                .Where(e => e != null && e.enabled && (e.maxUsesPerConversation > 0 || e.recommendedCooldownExchanges > 0))
                .ToList();

            if (restrictedEffects.Count > 0)
            {
                sb.AppendLine("Usage Limits:");
                foreach (var def in restrictedEffects)
                {
                    if (def.maxUsesPerConversation > 0)
                        sb.AppendLine($"  {def.effectTag}: max {def.maxUsesPerConversation}x per conversation");
                    if (def.recommendedCooldownExchanges > 0)
                        sb.AppendLine($"  {def.effectTag}: wait {def.recommendedCooldownExchanges} exchanges between uses");
                }
                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Check if an effect has any enhanced guidance defined.
        /// </summary>
        private bool HasEffectGuidance(EffectDefinition def)
        {
            if (def == null) return false;
            
            return def.situationalTriggers.useWhenTargetLowHealth ||
                   def.situationalTriggers.useWhenTargetFullHealth ||
                   def.situationalTriggers.preferSingleTarget ||
                   def.situationalTriggers.preferMultipleTargets ||
                   def.situationalTriggers.requiresOutdoor ||
                   (def.synergisticEffects?.Length > 0) ||
                   (def.alternativeEffects?.Length > 0) ||
                   def.maxUsesPerConversation > 0 ||
                   def.recommendedCooldownExchanges > 0;
        }

        private static string BuildEffectParameterHints(Effects.EffectDefinition effect)
        {
            if (effect == null)
                return string.Empty;

            var hints = new List<string>(6);

            switch (effect.targetType)
            {
                case Effects.EffectTargetType.Floor:
                    hints.Add("target ground");
                    break;
                case Effects.EffectTargetType.WorldPoint:
                    hints.Add("target world point");
                    break;
                case Effects.EffectTargetType.Npc:
                    hints.Add("target Self");
                    break;
                default:
                    hints.Add("target player");
                    break;
            }

            if (effect.allowCustomScale)
                hints.Add($"scale {effect.minScale:0.#}-{effect.maxScale:0.#}");

            if (effect.allowCustomDuration)
                hints.Add($"duration {effect.minDuration:0.#}-{effect.maxDuration:0.#}s");

            if (effect.placementMode == Effects.EffectPlacementMode.GroundAoe)
                hints.Add($"radius {effect.minRadius:0.#}-{effect.maxRadius:0.#}");
            else if (effect.placementMode != Effects.EffectPlacementMode.Auto)
                hints.Add(effect.placementMode.ToString());

            if (effect.allowCustomColor)
                hints.Add("color");

            if (effect.enableGameplayDamage)
                hints.Add("damage");

            if (effect.enableHoming)
                hints.Add("homing");

            return hints.Count == 0 ? string.Empty : string.Join(", ", hints);
        }

        private static string BuildKeywordPreview(string[] keywords)
        {
            if (keywords == null || keywords.Length == 0)
            {
                return string.Empty;
            }

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var picked = new List<string>(4);
            for (int i = 0; i < keywords.Length && picked.Count < 4; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                string normalized = keyword.Trim();
                if (!unique.Add(normalized))
                {
                    continue;
                }

                picked.Add(normalized);
            }

            return picked.Count == 0 ? string.Empty : string.Join(", ", picked);
        }

        private static string BuildSceneContextInfo()
        {
            string registrySummary = DialogueSceneTargetRegistry.GetSceneContextSummary();
            if (!string.IsNullOrWhiteSpace(registrySummary))
            {
                s_CachedSceneContext = registrySummary;
                s_SceneContextCacheTime = Time.realtimeSinceStartup;
                return registrySummary;
            }

            if (
                s_CachedSceneContext != null
                && Time.realtimeSinceStartup - s_SceneContextCacheTime < SceneContextCacheDuration
            )
            {
                return s_CachedSceneContext;
            }

            string result = BuildSceneContextInfoUncached();
            s_CachedSceneContext = result;
            s_SceneContextCacheTime = Time.realtimeSinceStartup;
            return result;
        }

        private static string BuildSceneContextInfoUncached()
        {
            var sb = new StringBuilder(512);

            AppendSemanticSceneRoles(sb);

            // Find ground plane/terrain dimensions
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
                ground = GameObject.Find("Plane");
            if (ground == null)
                ground = GameObject.Find("Floor");
            if (ground == null)
                ground = GameObject.Find("Arena_Floor");
            if (ground == null)
                ground = GameObject.Find("Terrain");

            if (ground != null)
            {
                Vector3 scale = ground.transform.localScale;
                Vector3 pos = ground.transform.position;
                Renderer rend = ground.GetComponent<Renderer>();
                if (rend != null)
                {
                    Vector3 size = rend.bounds.size;
                    sb.AppendLine(
                        $"Ground: \"{ground.name}\" size={size.x:F1}x{size.z:F1}m at y={pos.y:F1}."
                    );
                    sb.AppendLine(
                        $"To cover the entire ground, use radius {Mathf.Max(size.x, size.z) * 0.5f:F0} or scale {Mathf.Max(size.x, size.z) / 10f:F1}x."
                    );
                }
                else
                {
                    // Unity default plane is 10x10 at scale 1
                    float estimatedSize = Mathf.Max(scale.x, scale.z) * 10f;
                    sb.AppendLine(
                        $"Ground: \"{ground.name}\" estimated size ~{estimatedSize:F0}x{estimatedSize:F0}m at y={pos.y:F1}."
                    );
                    sb.AppendLine(
                        $"To cover the entire ground, use radius {estimatedSize * 0.5f:F0} or scale {estimatedSize / 10f:F1}x."
                    );
                }
            }

            // Find key scene objects the LLM can target
            var interestingObjects = new List<string>(8);
            GameObject[] rootObjects = UnityEngine
                .SceneManagement.SceneManager.GetActiveScene()
                .GetRootGameObjects();
            if (rootObjects != null)
            {
                for (int i = 0; i < rootObjects.Length && interestingObjects.Count < 12; i++)
                {
                    GameObject obj = rootObjects[i];
                    if (obj == null || !obj.activeInHierarchy)
                    {
                        continue;
                    }

                    string objName = obj.name;
                    // Skip system objects
                    if (
                        objName.StartsWith("Directional", StringComparison.OrdinalIgnoreCase)
                        || objName.StartsWith("EventSystem", StringComparison.OrdinalIgnoreCase)
                        || objName.StartsWith("Canvas", StringComparison.OrdinalIgnoreCase)
                        || objName.StartsWith("Dialogue", StringComparison.OrdinalIgnoreCase)
                        || objName.StartsWith("Network", StringComparison.OrdinalIgnoreCase)
                        || objName.StartsWith("---", StringComparison.Ordinal)
                        || objName.StartsWith("_", StringComparison.Ordinal)
                    )
                    {
                        continue;
                    }

                    Renderer r = obj.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        Vector3 s = r.bounds.size;
                        Vector3 p = obj.transform.position;
                        interestingObjects.Add(
                            $"\"{objName}\" ({s.x:F1}x{s.y:F1}x{s.z:F1}m at [{p.x:F1},{p.y:F1},{p.z:F1}])"
                        );
                    }
                    else
                    {
                        Vector3 p = obj.transform.position;
                        interestingObjects.Add($"\"{objName}\" (at [{p.x:F1},{p.y:F1},{p.z:F1}])");
                    }
                }
            }

            if (interestingObjects.Count > 0)
            {
                sb.Append("Scene objects: ");
                sb.AppendLine(string.Join(", ", interestingObjects) + ".");
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendSemanticSceneRoles(StringBuilder sb)
        {
            if (sb == null)
            {
                return;
            }

            DialogueSemanticTag[] semanticTags = FindSemanticTags();
            if (semanticTags == null || semanticTags.Length == 0)
            {
                return;
            }

            var entries = new List<string>(12);
            for (int i = 0; i < semanticTags.Length && entries.Count < 12; i++)
            {
                DialogueSemanticTag semantic = semanticTags[i];
                if (semantic == null || !semantic.IncludeInSceneSnapshots)
                {
                    continue;
                }

                string name = semantic.ResolveDisplayName(semantic.gameObject);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string role = semantic.RoleKey;
                string[] aliases = semantic.GetCompactAliases(2);
                string aliasPart =
                    aliases.Length > 0 ? $" aliases={string.Join("/", aliases)}" : string.Empty;
                string desc = semantic.Description;
                string descPart = !string.IsNullOrEmpty(desc) ? $" — {desc}" : string.Empty;
                entries.Add($"\"{name}\" role={role}{aliasPart}{descPart}");
            }

            if (entries.Count == 0)
            {
                return;
            }

            sb.Append("Semantic roles: ");
            sb.AppendLine(string.Join(", ", entries) + ".");
        }

        private static DialogueSemanticTag[] FindSemanticTags()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<DialogueSemanticTag>(
                FindObjectsInactive.Exclude
            );
#else
            return UnityEngine.Object.FindObjectsOfType<DialogueSemanticTag>();
#endif
        }

        /// <summary>
        /// Runtime fallback for misconfigured scenes: if profile is missing, attempt to bind one by name/id heuristics.
        /// </summary>
        public bool TryAutoAssignProfileFromName()
        {
            if (m_Profile != null)
            {
                return true;
            }

            NpcDialogueProfile[] profiles = NpcDialogueProfile.GetAllProfiles();
            if (profiles == null || profiles.Length == 0)
            {
                return false;
            }

            string actorName =
                (gameObject != null ? gameObject.name : string.Empty) ?? string.Empty;
            string actorLower = actorName.Trim().ToLowerInvariant();
            string profileHint = (m_ProfileIdOverride ?? string.Empty).Trim().ToLowerInvariant();

            NpcDialogueProfile best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < profiles.Length; i++)
            {
                NpcDialogueProfile candidate = profiles[i];
                if (candidate == null)
                {
                    continue;
                }

                string id = (candidate.ProfileId ?? string.Empty).Trim().ToLowerInvariant();
                string idCore = id.Replace("npc.", string.Empty);
                string display = (candidate.DisplayName ?? string.Empty).Trim().ToLowerInvariant();

                int score = 0;
                if (!string.IsNullOrWhiteSpace(profileHint))
                {
                    if (profileHint == id || profileHint == idCore)
                    {
                        score += 100;
                    }
                    if (!string.IsNullOrWhiteSpace(display) && profileHint.Contains(display))
                    {
                        score += 20;
                    }
                }

                if (!string.IsNullOrWhiteSpace(actorLower))
                {
                    if (actorLower.Contains(id))
                    {
                        score += 90;
                    }
                    if (!string.IsNullOrWhiteSpace(idCore) && actorLower.Contains(idCore))
                    {
                        score += 80;
                    }
                    if (!string.IsNullOrWhiteSpace(display) && actorLower.Contains(display))
                    {
                        score += 60;
                    }

                    if (
                        actorLower.Contains("storm")
                        && (idCore.Contains("storm") || display.Contains("storm"))
                    )
                    {
                        score += 40;
                    }
                    if (
                        actorLower.Contains("forge")
                        && (idCore.Contains("forge") || display.Contains("forge"))
                    )
                    {
                        score += 40;
                    }
                    if (
                        actorLower.Contains("archiv")
                        && (idCore.Contains("archiv") || display.Contains("archiv"))
                    )
                    {
                        score += 40;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == null || bestScore <= 0)
            {
                return false;
            }

            m_Profile = best;
            m_CachedCapabilitiesGuide = null; // profile changed — invalidate guide cache
            m_CachedCapabilitiesListenerName = null;
            NGLog.Warn(
                "Dialogue",
                NGLog.Format(
                    "Auto-assigned missing NPC profile",
                    ("actor", actorName),
                    ("profileId", best.ProfileId ?? string.Empty),
                    ("score", bestScore)
                    ),
                this
            );
            return true;
        }

        #region Enhanced Context API

        /// <summary>
        /// Update the target context with current state information.
        /// Call this before building the prompt for accurate situational guidance.
        /// </summary>
        public void UpdateTargetContext(GameObject target, CombatHealthV2 health = null)
        {
            if (!m_EnableTargetContext || m_TargetContext == null || target == null)
                return;

            m_TargetContext.displayName = target.name;
            
            if (health != null)
            {
                m_TargetContext.UpdateHealth(health);
            }

            // Calculate distance
            m_TargetContext.distanceToSpeaker = Vector3.Distance(transform.position, target.transform.position);
            
            // Check line of sight
            m_TargetContext.hasLineOfSight = HasLineOfSightTo(target);

            // Update timing
            m_TargetContext.UpdateTiming();
        }

        /// <summary>
        /// Record that an effect was used for conversation memory.
        /// </summary>
        public void RecordEffectUsage(EffectDefinition definition, float scale, float duration, 
            string targetName, string emotionalTone)
        {
            if (!m_EnableConversationMemory || m_DecisionContext == null || definition == null)
                return;

            m_DecisionContext.RecordEffectUsage(definition, scale, duration, targetName, emotionalTone, m_CurrentExchangeNumber);
        }

        /// <summary>
        /// Record the result of an effect for feedback.
        /// </summary>
        public void RecordEffectResult(string effectTag, bool succeeded, string failureReason,
            float finalScale, float finalDuration)
        {
            if (!m_EnableConversationMemory || m_DecisionContext == null)
                return;

            m_DecisionContext.RecordEffectResult(effectTag, succeeded, failureReason, finalScale, finalDuration);
        }

        /// <summary>
        /// Advance to the next exchange in the conversation.
        /// </summary>
        public void NextExchange()
        {
            m_CurrentExchangeNumber++;
            m_DecisionContext?.NextExchange();
        }

        /// <summary>
        /// Set the current story beat for narrative guidance.
        /// </summary>
        public void SetStoryBeat(string beat)
        {
            m_DecisionContext?.SetStoryBeat(beat);
        }

        /// <summary>
        /// Start a new conversation with the given key.
        /// </summary>
        public void BeginConversation(string conversationKey, string storyBeat = "greeting")
        {
            m_CurrentConversationKey = conversationKey;
            m_CurrentExchangeNumber = 0;
            m_DecisionContext?.BeginConversation(storyBeat, 0.3f);
            m_TargetContext?.activeEffects?.Clear();
        }

        /// <summary>
        /// Check if an effect would be appropriate given current context.
        /// </summary>
        public EffectAppropriateness CheckEffectAppropriateness(EffectDefinition definition)
        {
            if (definition == null)
                return EffectAppropriateness.NotRecommended("Effect definition is null");

            var reasons = new List<string>();

            // Check usage limits
            if (!m_DecisionContext?.IsEffectAvailable(definition) ?? false)
            {
                int used = m_DecisionContext?.GetUsageCount(definition.effectTag) ?? 0;
                return EffectAppropriateness.NotRecommended($"Max uses reached ({used}/{definition.maxUsesPerConversation})");
            }

            // Check cooldown
            if (!m_DecisionContext?.IsEffectOffCooldown(definition) ?? true == false)
            {
                reasons.Add($"On cooldown (wait {definition.recommendedCooldownExchanges} exchanges)");
            }

            // Check for repetition
            if (m_DecisionContext?.WasUsedRecently(definition.effectTag) ?? false)
            {
                reasons.Add("Used recently — consider variety");
            }

            // Check target state suitability
            if (m_TargetContext != null)
            {
                if (definition.situationalTriggers.useWhenTargetLowHealth && !m_TargetContext.IsLowHealth)
                    reasons.Add("Designed for low-health targets (target is healthy)");
                
                if (definition.situationalTriggers.useWhenTargetFullHealth && !m_TargetContext.IsHighHealth)
                    reasons.Add("Designed for full-health targets (target is wounded)");

                if (definition.situationalTriggers.optimalRangeMax > 0 && 
                    m_TargetContext.distanceToSpeaker > definition.situationalTriggers.optimalRangeMax)
                    reasons.Add($"Target out of optimal range ({m_TargetContext.distanceToSpeaker:F1}m > {definition.situationalTriggers.optimalRangeMax:F0}m)");

                if (definition.situationalTriggers.requiresLineOfSight && !m_TargetContext.hasLineOfSight)
                    reasons.Add("Requires line of sight (blocked)");
            }

            // Check story beat appropriateness
            if (!definition.IsAppropriateForStoryBeat(m_DecisionContext?.CurrentStoryBeat))
            {
                reasons.Add($"May not fit current story beat ({m_DecisionContext?.CurrentStoryBeat})");
            }

            if (reasons.Count == 0)
                return EffectAppropriateness.Recommended();
            
            return EffectAppropriateness.Cautionary(string.Join("; ", reasons));
        }

        /// <summary>
        /// Get a tactical suggestion based on current context.
        /// </summary>
        public string GetTacticalSuggestion()
        {
            if (m_TargetContext != null)
            {
                var suggestion = m_TargetContext.GetTacticalSuggestion();
                if (!string.IsNullOrWhiteSpace(suggestion))
                    return suggestion;
            }

            return m_DecisionContext?.GetVarietySuggestion();
        }

        /// <summary>
        /// Get enhanced capabilities guide with current context.
        /// </summary>
        public string GetEnhancedCapabilitiesGuide(string listenerName, GameObject listenerObject)
        {
            // Force rebuild to include current context
            m_CachedCapabilitiesGuide = null;
            m_CachedCapabilitiesListenerName = null;
            return BuildNpcCapabilitiesGuide(listenerName, listenerObject);
        }

        /// <summary>
        /// Get the current target context (for telemetry).
        /// </summary>
        public DialogueTargetContext GetTargetContext() => m_TargetContext;

        /// <summary>
        /// Get the current decision context (for telemetry).
        /// </summary>
        public EffectDecisionContext GetDecisionContext() => m_DecisionContext;

        /// <summary>
        /// Get the current exchange number.
        /// </summary>
        public int GetCurrentExchangeNumber() => m_CurrentExchangeNumber;

        /// <summary>
        /// Check line of sight to a target.
        /// </summary>
        private bool HasLineOfSightTo(GameObject target)
        {
            if (target == null) return false;

            Vector3 direction = target.transform.position - transform.position;
            float distance = direction.magnitude;
            
            if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, distance))
            {
                return hit.transform.gameObject == target;
            }
            
            return true;
        }

        #endregion

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        [ContextMenu("Dialogue/Send Test Prompt (Server)")]
        private void SendTestPromptContextMenu()
        {
            if (!Application.isPlaying)
            {
                NGLog.Warn("NpcDialogueActor", "Enter Play Mode to send a test prompt.");
                return;
            }

            if (!IsServer)
            {
                NGLog.Warn("NpcDialogueActor", "Test prompt must be sent from server/host.");
                return;
            }

            NetworkDialogueService service = NetworkDialogueService.Instance;
            if (service == null)
            {
                NGLog.Warn("NpcDialogueActor", "NetworkDialogueService not found in scene.");
                return;
            }

            NetworkObject player =
                NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClient?.PlayerObject
                : null;
            if (player == null)
            {
                NGLog.Warn("NpcDialogueActor", "Local player object not available.");
                return;
            }

            ulong speakerId = NetworkObjectId;
            ulong listenerId = player.NetworkObjectId;
            string key = service.ResolveConversationKey(
                speakerId,
                listenerId,
                player.OwnerClientId,
                null
            );

            service.RequestDialogue(
                new NetworkDialogueService.DialogueRequest
                {
                    Prompt = "Hello.",
                    ConversationKey = key,
                    SpeakerNetworkId = speakerId,
                    ListenerNetworkId = listenerId,
                    RequestingClientId = player.OwnerClientId,
                    Broadcast = true,
                    BroadcastDuration = 2f,
                    NotifyClient = true,
                    ClientRequestId = 0,
                    IsUserInitiated = false,
                    BlockRepeatedPrompt = false,
                    MinRepeatDelaySeconds = 0f,
                    RequireUserReply = false,
                }
            );

            NGLog.Info(
                "Dialogue",
                NGLog.Format(
                    "NpcDialogueActor test prompt sent",
                    ("npc", name),
                    ("speaker", speakerId),
                    ("listener", listenerId),
                    ("key", key)
                    ),
                this
            );
        }

        private static string[] SplitTagWords(string tag)
        {
            var sb = new StringBuilder(tag.Length + 8);
            for (int i = 0; i < tag.Length; i++)
            {
                char c = tag[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(tag[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().ToLowerInvariant()
                .Split(new[] { ' ', '_', '-', '.', '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static float ComputeWordOverlap(string[] query, string[] candidate)
        {
            if (query.Length == 0 || candidate.Length == 0) return 0f;
            const int MinStem = 5;
            int matches = 0;
            for (int i = 0; i < query.Length; i++)
            {
                string q = query[i];
                for (int j = 0; j < candidate.Length; j++)
                {
                    string c = candidate[j];
                    if (string.Equals(q, c, StringComparison.Ordinal)) { matches++; break; }
                    if (q.Length >= MinStem && c.Length >= MinStem)
                    {
                        int stem = Math.Min(q.Length, c.Length);
                        if (string.Compare(q, 0, c, 0, stem, StringComparison.Ordinal) == 0) { matches++; break; }
                    }
                }
            }
            return (float)matches / Math.Max(query.Length, candidate.Length);
        }
    }

    /// <summary>
    /// Represents the appropriateness of an effect choice given current context.
    /// </summary>
    public struct EffectAppropriateness
    {
        public bool IsRecommended;
        public bool IsCautionary;
        public string Reason;

        public static EffectAppropriateness Recommended()
        {
            return new EffectAppropriateness { IsRecommended = true, IsCautionary = false, Reason = null };
        }

        public static EffectAppropriateness Cautionary(string reason)
        {
            return new EffectAppropriateness { IsRecommended = true, IsCautionary = true, Reason = reason };
        }

        public static EffectAppropriateness NotRecommended(string reason)
        {
            return new EffectAppropriateness { IsRecommended = false, IsCautionary = false, Reason = reason };
        }
    }
}
