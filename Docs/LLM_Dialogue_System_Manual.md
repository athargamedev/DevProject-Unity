# LLM Dialogue System Manual

## Overview

The LLM Dialogue System is a server-authoritative dialogue pipeline that enables natural language interactions between players and NPCs in a multiplayer 3rd-person game. It integrates with Large Language Models (LLMs) to generate contextual responses, triggers visual effects based on dialogue content, and maintains conversation history.

## System Architecture

### Core Components

1. **NetworkDialogueService.cs** - The central server-authoritative dialogue router (~9k LOC)
   - Handles request queuing, processing, and response distribution
   - Manages conversation history per player/NPC pair
   - Coordinates LLM inference, effect dispatch, and UI updates

2. **DialogueClientUI.cs** - Client-side UI for chat presentation and input (~3.3k LOC)
   - Displays chat transcript with player/NPC/system messages
   - Handles user input and dialogue submission
   - Manages chat bubbles and targeting indicators

3. **NpcDialogueActor.cs** - Networked NPC speech and persona binding (~1k LOC)
   - Represents NPCs in the dialogue system
   - Binds NPCs to their dialogue profiles
   - Handles NPC speech output and effect triggering

4. **NpcDialogueProfile.cs** - Per-NPC ScriptableObject configuration (~200 LOC)
   - Defines NPC identity, personality, and lore
   - Configures available effects and their parameters
   - Sets dynamic effect parameter clamps

5. **Effects Subsystem** - Visual effect generation and dispatch (~10.5k LOC)
   - Parses LLM responses for effect triggers
   - Extracts parameters from dialogue text
   - Resolves spatial positioning for effects
   - Dispatches effects to all clients via ClientRpc

## Dialogue Flow

### Request Lifecycle

1. **Request Submission**
   - Player submits dialogue via `DialogueClientUI`
   - System creates a `DialogueRequest` with:
     - Prompt text
     - Conversation key (player-NPC pair)
     - Speaker/listener network IDs
     - Request flags (broadcast, notify client, etc.)

2. **Validation & Queuing**
   - Request passes through admission control (`RequestValidation.cs`)
   - Valid requests are enqueued (`Requests.cs`)
   - System respects rate limits and concurrency settings

3. **Processing**
   - Worker dequeues request (`RequestExecution.cs`)
   - LLM inference is performed (local or remote)
   - Response is parsed for structured actions and effects
   - Effects are resolved and dispatched
   - Response is delivered to requesting client

4. **Response Handling**
   - Client receives response via RPC
   - UI updates chat transcript
   - NPC speaks response via `NpcDialogueActor`
   - Triggered effects are visualized

### Effect Pipeline

When an LLM generates a response, the system follows this pipeline:

```
LLM Response Text
    ↓
EffectParser.Parse(text, profile) → EffectIntent
    ↓
ParticleParameterExtractor.ExtractIntent(text) → ParticleParameterIntent
    ↓
DialogueSceneEffectsController.ApplyContextEffects()
    → Match keywords from NpcDialogueProfile
    → Resolve spatial target via DialogueEffectSpatialResolver
    → Dispatch via typed ClientRpc to all clients
```

## Configuration

### Dialogue Backend Settings

Configured via `DialogueBackendConfig` ScriptableObject:
- Remote inference endpoint (OpenAI-compatible)
- Model selection and parameters
- Prompt budgeting and history limits
- Retry and timeout policies

### NPC Profile Configuration

Each NPC has a `NpcDialogueProfile` asset defining:
- **Identity**: Profile ID, display name
- **Personality**: System prompt template
- **Lore**: Background information injected into prompts
- **Effects**: Available effect definitions with parameters
- **Dynamic Effects**: Whether dialogue can modulate effect parameters
- **Parameter Clamps**: Min/max multipliers for dynamic effects

### Effect Types

The system supports 8 effect types, each with specific parameters:

| Effect Type | Key Parameters | RPC Method |
|-------------|---------------|------------|
| Bored Lighting | color, intensity, transition duration | `ApplyBoredLightingEffectClientRpc` |
| Prop Spawn | type, count, scale, spacing, lifetime, color | `SpawnContextPropsClientRpc` |
| Wall Image | width, height, lifetime, tint | `ApplyProceduralWallImageClientRpc` |
| Rain Burst | radius, duration, count, speed, emission, size | `ApplyRainBurstEffectClientRpc` |
| Shockwave | force, radius, upward force, visual duration, color | `ApplyShockwaveEffectClientRpc` |
| Shield Bubble | mesh radius, shader props, duration | `ApplyShieldBubbleEffectClientRpc` |
| Waypoint Ping | position, color, duration | `ApplyWaypointPingEffectClientRpc` |
| Prefab Power | scale, emission scaling, lifetime, color | `ApplyPrefabPowerEffectClientRpc` |

### Parameter Extraction

The `ParticleParameterExtractor` analyzes dialogue text to produce:
- **Multipliers** (1.0 baseline): intensity, duration, radius, size, speed, count, force
- **Explicit Numerics**: explicit duration, radius, scale, count values
- **Emotional Multipliers**: epic=1.6x, legendary=1.5x, chaotic=1.45x, peaceful=0.65x
- **Element Detection**: fire, ice, storm, water, earth, nature, mystic, void
- **Color Override**: blue, red, green, yellow, orange, purple, white, black

Profile-specific clamps (`DynamicEffectMinMultiplier`/`DynamicEffectMaxMultiplier`) are applied to extracted multipliers.

## Usage Guidelines

### For Developers

1. **Adding Dialogue Capabilities**
   - Never bypass `NetworkDialogueService` with direct LLM calls
   - All dialogue requests must go through the official request pipeline
   - Use `NetworkDialogueService.RequestDialogue()` for programmatic requests

2. **Configuring NPCs**
   - Create `NpcDialogueProfile` assets for each NPC type
   - Assign appropriate effects based on NPC role/personality
   - Set lore and system prompt to define NPC behavior
   - Configure effect parameters matching NPC capabilities

3. **Creating New Effects**
   - Add corresponding ClientRpc method in `DialogueSceneEffectsController`
   - Add keyword entry in `NpcDialogueProfile` effect definitions
   - Implement effect logic in appropriate Effect* classes
   - Register effect in `EffectCatalog` if needed

4. **Testing Dialogue**
   - Use `Behavior_Scene.unity` for testing
   - Verify logs show proper request flow
   - Check that effects trigger correctly from dialogue
   - Validate conversation history is maintained

### For Designers/Writers

1. **Writing NPC Dialogue**
   - Keep responses in-character and concise
   - Include trigger words for desired effects (fire, ice, storm, etc.)
   - Consider emotional tone for multiplier effects (epic, peaceful, etc.)
   - Reference NPC lore when appropriate for consistency

2. **Effect Design**
   - Match effect parameters to narrative intent
   - Use explicit numbers in dialogue for precise control ("a massive explosion")
   - Leverage emotional language for intensity modulation ("an incredibly powerful blast")
   - Reference elements for thematic effects ("ice shards", "flames of fury")

## Best Practices

### Do's

1. **Preserve Invariants**
   - Keep canonical key routing through `ResolveConversationKey()`
   - Maintain server-authoritative generation path
   - Preserve loop guard contract for auto NPC greeting
   - Keep effect dispatch server → all clients via ClientRpc
   - Maintain UnifiedLog categories for debugging

2. **Follow Preferred Workflow**
   - Reproduce issues in `Behavior_Scene.unity`
   - Capture and classify failure types from logs
   - Implement minimal reliable fixes
   - Validate no regression in request lifecycle
   - Run persona sanity checks regularly

3. **Use Tooling Effectively**
   - Register MCP custom tools first
   - Use `ng_pipeline_status` and `ng_get_full_diagnostics` for diagnostics
   - Monitor log categories: `NG:Dialogue`, `NG:LLMChat`, `NG:DialogueUI`, etc.

### Don'ts

1. **Avoid Anti-Patterns**
   - Don't bypass `NetworkDialogueService` with direct LLM calls
   - Don't hardcode non-canonical conversation keys
   - Don't disable guards to hide loops; fix root causes
   - Don't keep enabled per-NPC `LLMAgent` components
   - Don't add effect types without ClientRpc and profile keyword
   - Don't extract parameters without respecting profile clamp bounds

2. **Avoid Modifying**
   - Third-party assets (`ParticlePack/`, `StarterAssets/`)
   - Effect definition assets without updating `EffectCatalog` reference

## Troubleshooting

### Common Issues

1. **No Dialogue Response**
   - Check if `NetworkDialogueService.Instance` is initialized
   - Verify LLM backend is reachable and configured
   - Confirm request is passing validation (check logs for rejection reasons)
   - Ensure conversation key is valid and not blocked

2. **Effects Not Triggering**
   - Verify effect keywords exist in NPC profile
   - Check that LLM response contains trigger words
   - Confirm effect parameters are within profile clamps
   - Validate spatial resolution isn't failing (check for ground collision)

3. **UI Issues**
   - Ensure `DialogueClientUI` is properly referenced in scene
   - Check that input field and output text components are assigned
   - Verify UI canvas is active and visible
   - Look for errors in `DialogueUiCategory` logs

### Diagnostic Commands

- `ng_pipeline_status` - Shows current dialogue pipeline state
- `ng_get_full_diagnostics` - Provides comprehensive system diagnostics
- Check logs filtered by `NG:Dialogue`, `NG:LLMChat`, `NG:DialogueUI`, `NG:DialogueFX`

## Performance Considerations

### Rate Limiting
- `m_MaxConcurrentRequests`: Limits simultaneous LLM requests (default: 1)
- `m_MaxRequestsPerClient`: Limits requests per client (default: 4)
- `m_MinSecondsBetweenRequests`: Minimum delay between requests (default: 0.2s)

### Resource Management
- Conversation history limited by `m_MaxHistoryMessages` (default: 20)
- Request queue size limited by `m_MaxPendingRequests` (default: 32)
- Latency sampling windows configured for telemetry

### Optimization Tips
- Keep system prompts concise to reduce LLM prefill time
- Limit history messages sent to remote backends
- Use appropriate effect parameter clamps to prevent extreme values
- Consider local inference for low-latency requirements

## Integration Points

### With Auth System
- Uses `LocalPlayerAuthService` for player identity and customization
- Requires authenticated players by default (`m_RequireAuthenticatedPlayers`)
- Integrates player customization into prompt context

### With Combat System
- Can trigger combat-related effects (damage, shields)
- Effect spatial resolver considers collision layers
- Particle collision damage system available for projectile effects

### With Player Controller
- Third-person controller remains owner-authoritative for movement
- Dialogue system can trigger visual effects that interact with player
- UI integrates with standard Unity input system

### With MCP (Model Context Protocol)
- `DialogueMCPBridge` provides editor-to-dialogue automation
- Enables runtime diagnostics and profile automation via custom tools
- Preferred path for dialogue diagnostics and profile management

## Conclusion

The LLM Dialogue System provides a robust, extensible framework for integrating natural language interactions into multiplayer games. By following the architectural guidelines and usage patterns outlined in this manual, developers can create engaging NPC dialogues that enhance gameplay through contextual responses and immersive visual effects.

For the most current information, refer to the source code and documentation in the `Assets/Network_Game/Dialogue/` directory.