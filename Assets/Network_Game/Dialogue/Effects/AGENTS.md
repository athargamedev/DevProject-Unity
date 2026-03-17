# Dialogue Effects AGENTS

## Scope

`Assets/Network_Game/Dialogue/Effects/` — VFX dispatch, parameter extraction, spatial resolution, feedback, and effect catalog. 17 scripts, ~10.5k LOC total.

## Key files

| File | LOC | Role |
|------|-----|------|
| `DialogueSceneEffectsController.cs` | 3281 | Central effect dispatcher — 8 effect types via ClientRpc |
| `DialogueEffectFeedbackPrompt.cs` | 1703 | Player feedback UI for effect quality tuning |
| `ParticleParameterExtractor.cs` | 867 | NLP → numeric effect parameters (intensity, color, element) |
| `EffectParser.cs` | 851 | Parse LLM response text into structured effect intents |
| `DialogueEffectSpatialResolver.cs` | 646 | World-space effect positioning (raycast, NPC-relative) |
| `EffectTargetResolverService.cs` | 565 | Resolve effect targets (player, NPC, world position) |
| `DialogueFeedbackCollector.cs` | 534 | Collect and aggregate player effect feedback |
| `DialogueEffectFeedbackRuntimeTuner.cs` | 520 | Runtime auto-tuning from feedback data |
| `DialogueEffectProjectile.cs` | 350 | Projectile trajectory + collision for effect prefabs |
| `DialogueParticleCollisionDamage.cs` | 221 | Damage-on-collision for particle effects |
| `EffectCatalog.cs` | 212 | ScriptableObject: effect definitions database |
| `EffectSurface.cs` | 191 | Surface type detection for effect grounding |
| `EffectSandboxRunner.cs` | 155 | Editor tool for isolated effect testing |
| `EffectDefinition.cs` | 140 | Data class: single effect definition |
| `EffectIntent.cs` | 129 | Data class: parsed effect intent from LLM text |
| `DialogueSemanticTag.cs` | 118 | Semantic tagging for effect-NPC persona mapping |
| `SpatialResolverConstants.cs` | 23 | Shared spatial constants (distances, angles) |

## Pipeline flow

```
LLM response text
  → EffectParser.Parse(text, profile) → EffectIntent
  → ParticleParameterExtractor.ExtractIntent(text) → ParticleParameterIntent
  → DialogueSceneEffectsController.ApplyContextEffects()
    → Match keywords from NpcDialogueProfile
    → Resolve spatial target via DialogueEffectSpatialResolver
    → Dispatch via typed ClientRpc to all clients
```

## Subsystems

- **Dispatch**: `DialogueSceneEffectsController` — 8 ClientRpc methods, one per effect type
- **Parsing**: `EffectParser` + `ParticleParameterExtractor` — text to structured params
- **Spatial**: `DialogueEffectSpatialResolver` + `EffectTargetResolverService` + `SpatialResolverConstants`
- **Feedback**: `DialogueFeedbackCollector` → `DialogueEffectFeedbackRuntimeTuner` → `DialogueEffectFeedbackPrompt`
- **Catalog**: `EffectCatalog` + `EffectDefinition` + `EffectDefinitions/*.asset`
- **Physics**: `DialogueEffectProjectile` + `DialogueParticleCollisionDamage`

## Do not do

- Do not add effect types without a corresponding ClientRpc in `DialogueSceneEffectsController` AND keyword entry in `NpcDialogueProfile`
- Do not extract parameters without respecting profile clamp bounds (`DynamicEffectMinMultiplier` / `DynamicEffectMaxMultiplier`)
- Do not bypass spatial resolution — effects must be world-positioned through the resolver
- Do not modify `EffectDefinitions/*.asset` files without updating the `EffectCatalog` reference
