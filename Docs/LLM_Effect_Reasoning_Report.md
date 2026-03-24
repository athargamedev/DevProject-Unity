# LLM Dialogue System: Effect Reasoning Analysis & Recommendations

**Date:** 2026-03-24  
**System:** Network_Game Dialogue Pipeline  
**Focus:** Improving LLM Decision-Making for Effect Triggering

---

## Executive Summary

The current dialogue system provides rich context to the LLM but has several gaps that limit the LLM's ability to make **optimal effect decisions**. The system primarily relies on:
1. **Whitelist enforcement** (NPC profiles restrict available effects)
2. **Heuristic parameter extraction** (regex parsing of natural language)
3. **Static context injection** (system prompts with capabilities guide)

This report identifies **7 key improvement areas** that would significantly enhance the LLM's reasoning capabilities for effect selection, timing, and parameterization.

---

## 1. Current Dataflow Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ STAGE 1: CONTEXT BUILDING (NpcDialogueActor.BuildSystemPrompt)                          │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ • Base system prompt (persona + lore)                                                   │
│ • CAPABILITIES GUIDE (effect definitions with constraints)                              │
│ • Player context (name, customization_json, narrative hints)                            │
│ • Scene context (objects, semantic roles)                                               │
└─────────────────────────────────────────────────────────────────────────────────────────┘
                                           ↓
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ STAGE 2: LLM INFERENCE (OpenAIChatClient)                                               │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ • JSON schema response format enforced                                                  │
│ • Structured actions: {"type":"EFFECT","tag":"...","target":"..."}                      │
│ • Natural language speech + effect decisions                                            │
└─────────────────────────────────────────────────────────────────────────────────────────┘
                                           ↓
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ STAGE 3: VALIDATION & PARSING (EffectParser + ParticleParameterExtractor)               │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ • Whitelist validation against NPC profile                                              │
│ • Regex-based parameter extraction (scale, duration, color, emotion, element)           │
│ • Multi-layer clamping (dynamic → profile bounds → definition bounds)                   │
└─────────────────────────────────────────────────────────────────────────────────────────┘
                                           ↓
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ STAGE 4: EXECUTION (DialogueSceneEffectsController)                                     │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ • ClientRpc dispatch to all clients                                                     │
│ • Prefab instantiation with parameter application                                       │
│ • Spatial validation (line-of-sight, ground snap, collision)                            │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Current Capabilities Guide (Provided to LLM)

The system constructs a capabilities guide that looks like:

```
[Available effects — use in EFFECT actions]
  Fireball — A blazing projectile of fire [fire] (target player, scale 0.5-3, duration 2-8s, color)
  IceLance — Piercing ice projectile [ice] (target player, scale 0.5-2, damage)
  LightningStorm — Calls down lightning [storm] (scale 0.8-2.5, duration 3-10s)
  ...

[Scene objects] "Ground", "Arena_Floor", "Crystal_01", ...

[NarrativeHints]
- This player has a fire affinity; reference fire-themed imagery
- The player's color theme is orange; prefer orange-toned effects
- This player is a Mage; open with lore fitting their archetype
```

---

## 3. Identified Gaps & Recommendations

### 3.1 Gap: Missing Effect Relationship Context

**Problem:** The LLM sees effects as independent items. It doesn't understand:
- Which effects combine well (combos)
- Which effects are alternatives for the same intent
- Elemental interactions (fire vs ice effectiveness)
- Effect hierarchies (basic → advanced → ultimate)

**Impact:** The LLM may:
- Use the same effect repeatedly instead of varying
- Miss opportunities for thematic combos
- Select ineffective elements against certain contexts

**Recommendation:**
```csharp
// Add to EffectDefinition
[Header("LLM Reasoning Context")]
[Tooltip("Tags for effect categorization (attack, defense, heal, buff, ultimate)")]
public string[] effectCategories;

[Tooltip("Effects that synergize well with this one")]
public EffectDefinition[] synergies;

[Tooltip("Effects that are alternatives for similar intents")]
public EffectDefinition[] alternatives;

[Tooltip("Elements this effect is strong/weak against")]
public string strongAgainst;  // e.g., "ice,nature"
public string weakAgainst;    // e.g., "water,earth"
```

**Prompt Addition:**
```
[Effect Relationships]
Fireball [fire] synergizes with: Inferno, FlameWhip
Alternatives to Fireball: Inferno (larger AOE), FlameWhip (melee)
Fire is strong against: ice, nature | weak against: water, earth

Combo Suggestions:
- "Fireball + Inferno" = intensified fire zone
- "IceLance + Blizzard" = frozen vulnerability
```

---

### 3.2 Gap: No Situational Effect Scoring

**Problem:** The LLM doesn't know when to use which effect. It lacks:
- Distance-based recommendations (melee vs ranged)
- Target-state awareness (low health → finisher, full health → opener)
- Environmental triggers (indoor vs outdoor, ground type)
- Crowd-size appropriateness (single target vs AOE)

**Impact:** 
- May use inappropriate effects for the situation
- Misses dramatic moments (e.g., not using ultimate when player is low)
- Doesn't adapt to spatial context

**Recommendation:**
```csharp
// Add contextual triggers to EffectDefinition
[Header("Situational Triggers")]
public bool preferWhenTargetLowHealth;
public bool preferWhenTargetFullHealth;
public bool preferForSingleTarget;
public bool preferForMultipleTargets;
public float optimalRangeMin = 0f;
public float optimalRangeMax = 50f;
public bool requiresOutdoor;
public bool requiresLineOfSight;
```

**Prompt Addition:**
```
[Situational Guidance]
- Use "Execute" when target health < 25%
- Use "OpeningBlast" when target health > 75%
- "Meteor" requires outdoor space (sky access)
- "ChainLightning" is optimal for 2+ clustered targets
- "Backstab" requires being behind target
```

---

### 3.3 Gap: Static Parameter Ranges

**Problem:** While min/max bounds are provided, the LLM doesn't understand:
- What "normal" vs "extreme" values feel like
- How parameters affect visual/gameplay experience
- When to push bounds vs stay conservative

**Impact:**
- Conservative parameter selection (always middle-range)
- No escalation pattern over conversation
- Missing dramatic moments

**Recommendation:**
```csharp
// Add semantic parameter guidance
[Header("Parameter Semantics")]
[Tooltip("Example values with semantic meaning")]
public ParameterExample[] parameterExamples = new[]
{
    new ParameterExample { scale = 0.5f, label = "subtle", context = "whisper, hint" },
    new ParameterExample { scale = 1.0f, label = "normal", context = "standard attack" },
    new ParameterExample { scale = 2.5f, label = "dramatic", context = "climactic moment" },
    new ParameterExample { scale = 5.0f, label = "epic", context = "boss ability, finale" }
};
```

**Prompt Addition:**
```
[Effect Intensity Guide — Fireball]
Scale 0.5 = Subtle ember (whispered threat, background ambience)
Scale 1.0 = Standard fireball (normal combat)
Scale 2.0 = Intense blaze (dramatic moment, important hit)
Scale 3.0+ = Inferno (climax, ultimate ability, story moment)

Duration 2s = Quick burst (surprise, interruption)
Duration 5s = Sustained (ongoing threat, zone control)
Duration 8s = Lingering (environmental change, after-effect)
```

---

### 3.4 Gap: No Conversation Memory for Effect Patterns

**Problem:** Each dialogue request is stateless. The LLM doesn't know:
- What effects were recently used
- Player's reaction to previous effects
- Escalation/de-escalation patterns needed

**Impact:**
- Repetitive effect usage
- No narrative arc in effect deployment
- Misses opportunities for callback/references

**Recommendation:**
```csharp
// Add to DialogueRequest
public string[] recentEffectsUsed;  // Last 3-5 effects
public Dictionary<string, int> effectUsageCount;  // Frequency map
public string lastEffectReaction;  // "impressed", "scared", "bored", etc.

// In BuildSystemPrompt()
if (recentEffectsUsed?.Length > 0)
{
    prompt.AppendLine($"[Recent Effects You've Used] {string.Join(", ", recentEffectsUsed)}");
    prompt.AppendLine("Avoid repeating the same effect. Vary your approach.");
}
```

**Prompt Addition:**
```
[Effect Memory — This Conversation]
Recently used: IceLance, FrostShield, IceLance (repetition detected!)
Player reaction: Seemed unimpressed by last IceLance
Suggestion: Try fire or lightning instead of more ice
```

---

### 3.5 Gap: Missing Target State Context

**Problem:** The LLM knows target identity but not:
- Current health/status
- Active buffs/debuffs
- Position relative to NPC
- Recent actions

**Impact:**
- Effects that don't match target state
- Missed opportunities for reactive effects
- No tactical depth

**Recommendation:**
```csharp
// Enhance PlayerContext
public class DialogueTargetContext
{
    public string name;
    public float healthPercent;
    public string[] activeEffects;  // Currently affecting them
    public string lastAction;  // What they just did
    public float distanceToNpc;
    public bool isMoving;
    public bool isBehindCover;
}
```

**Prompt Addition:**
```
[Target State — "Andre"]
Health: 32% (wounded) → Consider: heal, mercy, or finisher
Active effects: Burning (from your Fireball), Slowed
Distance: 3m (close) → Melee effects viable
Last action: Casting heal spell → Interrupt opportunity
```

---

### 3.6 Gap: No Feedback Loop for Effect Success

**Problem:** The system doesn't tell the LLM:
- Whether an effect was successfully applied
- Why an effect failed (spatial, whitelist, etc.)
- How to adjust for next time

**Impact:**
- LLM continues making same mistakes
- No learning from failed attempts
- Frustrating player experience

**Recommendation:**
```csharp
// Add post-effect feedback
public class EffectResult
{
    public string effectTag;
    public bool succeeded;
    public string failureReason;  // "out_of_range", "los_blocked", "not_in_profile"
    public float finalScale;
    public float finalDuration;
}

// In next prompt, include:
[Previous Effect Results]
Fireball → SUCCESS (scale 2.5, duration 5s)
Meteor → FAILED (reason: "requires_outdoor_space" — you're indoors!)
Suggestion: Use GroundPound or CollapseCeiling for indoor destruction
```

---

### 3.7 Gap: Limited Emotional/Story Context

**Problem:** While emotion keywords exist, the LLM lacks:
- Story arc progression (setup → rising → climax → resolution)
- NPC emotional state evolution
- Effect appropriateness for story beats

**Impact:**
- Effects feel disconnected from narrative
- No dramatic timing
- Missed story moments

**Recommendation:**
```csharp
// Add narrative state tracking
public enum StoryBeat
{
    Greeting, Exposition, RisingAction, Climax, FallingAction, Resolution
}

public class NarrativeContext
{
    public StoryBeat currentBeat;
    public float tensionLevel;  // 0-1
    public string sceneMood;  // "ominous", "triumphant", "desperate"
    public int exchangeCount;  // How many back-and-forths
}
```

**Prompt Addition:**
```
[Narrative Context]
Scene: "The Final Confrontation"
Beat: Climax (tension: 0.92/1.0)
Mood: Desperate, high stakes
Exchange: 7/10 (approaching resolution)

Effect Guidance:
- This is THE moment — use your most dramatic effects
- Scale should be 2x normal minimum
- Consider combining multiple effects
- Your ultimate ability is appropriate now
```

---

## 4. Implementation Priority

| Priority | Improvement | Complexity | Impact |
|----------|-------------|------------|--------|
| **P0** | Situational triggers (3.2) | Low | High |
| **P0** | Parameter semantics (3.3) | Low | High |
| **P1** | Effect relationships (3.1) | Medium | High |
| **P1** | Conversation memory (3.4) | Medium | Medium |
| **P2** | Target state context (3.5) | Medium | Medium |
| **P2** | Feedback loop (3.6) | High | High |
| **P3** | Narrative context (3.7) | High | Medium |

---

## 5. Code Implementation: Enhanced EffectDefinition

```csharp
// Assets/Network_Game/Dialogue/Effects/EffectDefinition.cs
// Add these sections:

[Header("LLM Reasoning Context")]
[Tooltip("Categories for effect classification")]
public string[] categories = new[] { "attack" }; // attack, defense, heal, buff, debuff, ultimate, ambient

[Tooltip("Effects that combine well with this")]
public EffectDefinition[] synergisticEffects;

[Tooltip("Alternative effects for similar situations")]
public EffectDefinition[] alternativeEffects;

[Tooltip("Tags describing when this effect is optimal")]
public SituationalTrigger situationalTriggers;

[Serializable]
public struct SituationalTrigger
{
    public bool useWhenTargetLowHealth;
    public bool useWhenTargetFullHealth;
    public bool preferSingleTarget;
    public bool preferMultipleTargets;
    public float optimalRangeMin;
    public float optimalRangeMax;
    public bool requiresLineOfSight;
    public bool requiresOutdoor;
    public string[] requiredTags; // target must have these
    public string[] forbiddenTags; // target must NOT have these
}

[Header("Parameter Semantics for LLM")]
[Tooltip("Guidance for scale parameter interpretation")]
public ParameterSemantic scaleSemantics = new ParameterSemantic
{
    subtle = 0.5f,
    normal = 1.0f,
    dramatic = 2.0f,
    epic = 3.5f
};

[Serializable]
public struct ParameterSemantic
{
    public float subtle;
    public float normal;
    public float dramatic;
    public float epic;
    [TextArea(2, 3)] public string contextDescription;
}
```

---

## 6. Enhanced Prompt Building

```csharp
// In NpcDialogueActor.BuildCapabilitiesGuide()

private string BuildEnhancedCapabilitiesGuide()
{
    var sb = new StringBuilder();
    sb.AppendLine("[Available Effects]");
    
    foreach (var effect in profile.Effects)
    {
        sb.AppendLine($"  {effect.effectTag} — {effect.description}");
        sb.AppendLine($"    Categories: {string.Join(", ", effect.categories)}");
        sb.AppendLine($"    Range: {effect.situationalTriggers.optimalRangeMin:F0}-{effect.situationalTriggers.optimalRangeMax:F0}m");
        sb.AppendLine($"    Scale guide: subtle({effect.scaleSemantics.subtle}) / " +
                      $"normal({effect.scaleSemantics.normal}) / " +
                      $"dramatic({effect.scaleSemantics.dramatic}) / " +
                      $"epic({effect.scaleSemantics.epic})");
        
        if (effect.synergisticEffects?.Length > 0)
            sb.AppendLine($"    Combos: {string.Join(", ", effect.synergisticEffects.Select(e => e.effectTag))}");
        
        if (effect.situationalTriggers.useWhenTargetLowHealth)
            sb.AppendLine($"    ✓ Good for finishing wounded targets");
        
        sb.AppendLine();
    }
    
    // Add situational guidance
    sb.AppendLine("[Situational Guidance]");
    sb.AppendLine($"- Target distance: {targetDistance:F1}m");
    sb.AppendLine($"- Target health: {targetHealthPercent:F0}%");
    sb.AppendLine($"- Your last effect: {lastEffectUsed}");
    sb.AppendLine($"- Suggested intensity: {(tensionLevel > 0.7 ? "EPIC (climax)" : tensionLevel > 0.4 ? "DRAMATIC" : "NORMAL")}");
    
    return sb.ToString();
}
```

---

## 7. Validation & Testing Strategy

### 7.1 Unit Tests
```csharp
[Test]
public void EffectSelection_RespectsSituationalTriggers()
{
    var lowHealthEffect = ScriptableObject.CreateInstance<EffectDefinition>();
    lowHealthEffect.situationalTriggers.useWhenTargetLowHealth = true;
    
    // When target health < 25%, should prefer this effect
    var recommendation = GetEffectRecommendation(targetHealth: 0.2f);
    Assert.Contains(lowHealthEffect, recommendation.preferredEffects);
}
```

### 7.2 Integration Tests
```csharp
[Test]
public async Task LLM_UsesAppropriateEffectForStoryBeat()
{
    SetStoryBeat(StoryBeat.Climax);
    var response = await GetLLMResponse("Prepare yourself!");
    
    // In climax, should use high-scale effects
    Assert.That(response.Action.Scale, Is.GreaterThan(2.0f));
}
```

### 7.3 Telemetry Events
```csharp
// Track effect decision quality
NGLog.Info("DialogueFX", NGLog.Format(
    "Effect decision context",
    ("effect", effectTag),
    ("situation_match", CalculateSituationMatch(intent)),
    ("scale_appropriateness", CalculateScaleAppropriateness(intent)),
    ("variety_score", CalculateVarietyScore(effectTag))
));
```

---

## 8. Summary

The current system provides a solid foundation with whitelist enforcement, parameter extraction, and structured responses. However, by adding:

1. **Situational triggers** — Guide effect selection based on context
2. **Parameter semantics** — Help LLM understand what values "mean"
3. **Effect relationships** — Enable combos and alternatives
4. **Conversation memory** — Prevent repetition, enable escalation
5. **Target state context** — Enable reactive, tactical decisions
6. **Feedback loops** — Learn from failures
7. **Narrative context** — Time effects for story impact

...we can transform the LLM from a "random effect picker" into a **dramatic director** that understands pacing, variety, and narrative impact.

---

**Next Steps:**
1. Implement P0 items (situational triggers, parameter semantics) — 1-2 days
2. Add telemetry for effect decision tracking — 1 day
3. A/B test enhanced prompts against baseline — 1 week
4. Iterate based on player feedback and metrics — ongoing
