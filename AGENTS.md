# PROJECT KNOWLEDGE BASE

**Generated:** 2026-03-17
**Unity:** 6000.4.0b11 (Unity 6 beta)

## OVERVIEW

Multiplayer 3rd-person game with LLM-driven NPC dialogue, ML-Agents training, and visual effect powers. Server-authoritative via Netcode for GameObjects 2.9.2, URP rendering, Addressables asset loading.

## STRUCTURE

```
DevProject/
├── Assets/
│   ├── Network_Game/          # ALL gameplay code lives here
│   │   ├── Auth/              # Player login & identity
│   │   ├── Behavior/          # Scene bootstraps, behavior graphs, NPC wiring
│   │   ├── Combat/            # Health, damage overlay
│   │   ├── Core/              # WebGL transport adapter
│   │   ├── Diagnostics/       # Watchdog, inference reporter, debug assistant
│   │   ├── Dialogue/          # LLM pipeline, NPC profiles, effects, UI (largest subsystem)
│   │   ├── ParticlePack/      # 3rd-party VFX assets (do not modify)
│   │   ├── Scene/             # Scene assets and NPC profile assets
│   │   └── ThirdPersonController/  # Player movement, fly mode, input
│   ├── Editor/                # Editor-only tools (screenshot exporter)
│   ├── Plugins/               # Roslyn analyzers
│   ├── Settings/              # Build profiles, PlayMode settings
│   └── StarterAssets/         # Unity starter mobile input (do not modify)
├── Packages/                  # Unity packages (local ML-Agents override)
├── ProjectSettings/           # Unity project config
├── Tools/                     # Unity Accelerator scripts
├── .claude/                   # Claude Code agents, skills, MCP config
└── .serena/                   # Serena project config
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Network/transport bugs | `Behavior/Unity Behavior Example/NetworkBootstrap.cs` | Transport config, connection approval |
| Player spawn/ownership | `Behavior/Unity Behavior Example/PlayerBootstrap.cs` | Local-player resolution, host fallback |
| Auth/login issues | `Auth/LocalPlayerAuthService.cs` | Identity snapshots, prompt-context |
| Dialogue pipeline | `Dialogue/NetworkDialogueService.cs` | 9k LOC central router — start here |
| Dialogue UI | `Dialogue/DialogueClientUI.cs` + `Dialogue/UI/` | Chat panel, HUD, login |
| NPC personas | `Dialogue/NpcDialogueProfile.cs` + `Dialogue/Profiles/*.asset` | ScriptableObject configs |
| Visual effects | `Dialogue/Effects/` | 17 scripts — effect dispatch, parsing, catalog |
| ML-Agents training | `Dialogue/Scripts/NpcDialogueAgent.cs` | Agent observations/actions |
| ML-Agents SideChannel | `Dialogue/Scripts/LlmDialogueChannel.cs` | Python-Unity dialogue bridge |
| Scene wiring | `Behavior/Unity Behavior Example/BehaviorSceneBootstrap.cs` | Camera, blackboard, NPC init |
| Player movement | `ThirdPersonController/Scripts/ThirdPersonController.cs` | Owner-authoritative |
| Combat system | `Combat/CombatHealth.cs`, `CombatRuntimeOverlay.cs` | Health, damage display |
| MCP bridge | `Dialogue/MCP/DialogueMCPBridge.cs` | Editor-to-dialogue automation |

All paths relative to `Assets/Network_Game/` unless specified.

## CODE MAP

| Symbol | Type | Location | Role |
|--------|------|----------|------|
| `NetworkDialogueService` | NetworkBehaviour | Dialogue/ | Server dialogue router, LLM queue, lifecycle (~9k LOC) |
| `DialogueSceneEffectsController` | NetworkBehaviour | Dialogue/Effects/ | Effect dispatch via ClientRpc (8 effect types) |
| `NetworkBootstrap` | MonoBehaviour | Behavior/ | Transport, host/client startup, connection approval |
| `BehaviorSceneBootstrap` | MonoBehaviour | Behavior/ | Scene orchestrator: spawn, camera, blackboard, NPC wiring |
| `PlayerBootstrap` | MonoBehaviour | Behavior/ | Local player readiness, ownership |
| `LocalPlayerAuthService` | MonoBehaviour | Auth/ | Login state, identity snapshots |
| `DialogueClientUI` | MonoBehaviour | Dialogue/ | Player chat panel, NPC targeting |
| `NpcDialogueActor` | NetworkBehaviour | Dialogue/ | Networked NPC speech + persona binding |
| `NpcDialogueProfile` | ScriptableObject | Dialogue/ | Per-NPC keywords, params, powers |
| `DialogueAgentProfile` | ScriptableObject | Dialogue/Scripts/ | ML-Agents reward shaping config |
| `NpcDialogueAgent` | Agent | Dialogue/Scripts/ | ML-Agents NPC decision-making |
| `LlmDialogueChannel` | SideChannel | Dialogue/Scripts/ | Python-Unity dialogue data bridge |
| `ParticleParameterExtractor` | Static | Dialogue/Effects/ | LLM text to effect parameters |
| `EffectCatalog` | ScriptableObject | Dialogue/Effects/ | Effect definitions database |
| `OpenAIChatClient` | MonoBehaviour | Dialogue/ | Remote LLM inference path |
| `ThirdPersonController` | NetworkBehaviour | ThirdPersonController/ | Owner-authoritative player control |

## CONVENTIONS

- **Assembly**: Single `Network_Game` asmdef for all gameplay; separate `Network_Game.Dialogue.Editor` for editor tools
- **Namespace**: `Network_Game` root namespace
- **Logging**: Structured categories via `UnifiedLog` — `NG:Auth`, `NG:NetworkBootstrap`, `NG:PlayerBootstrap`, `NG:Dialogue`, `NG:DialogueUI`, `NG:DialogueFX`, `NG:LLMChat`, `NG:DialogueLoRA`, `NG:DialogueSanity`
- **Singletons**: `NetworkManager.Singleton`, `NetworkDialogueService.Instance`, `LocalPlayerAuthService.Instance`
- **ML-Agents**: Local package override (`file:../com.unity.ml-agents`) — not from registry
- **MCP**: `com.coplaydev.unity-mcp` on beta branch — editor automation via MCP tools

## ANTI-PATTERNS (THIS PROJECT)

- **Never** bypass `NetworkDialogueService` with direct LLM calls from MonoBehaviours
- **Never** enable per-NPC `LLMAgent` components — bootstrap disables them intentionally
- **Never** fix owner/authority bugs by enabling systems on every client
- **Never** trade correctness for convenience in multiplayer flow
- **Never** add effect types without corresponding ClientRpc + profile keyword entry
- **Never** extract parameters without respecting profile clamp bounds

## COMMANDS

```bash
# MCP custom tools (register first in Unity Editor)
# Menu: Network Game/MCP/Admin/Register All Custom Tools
# Then use: ng_pipeline_status, ng_get_full_diagnostics

# Scene setup
# Menu: Network Game/Dialogue/Setup Dialogue Scene Personas (3 NPCs)
# Menu: Network Game/Dialogue/Create 3 NPC Profiles

# Persona sanity check
# Context menu on PersonaDialogueSanityRunner -> Run Persona Sanity Check

# Unity Accelerator
Tools/accelerator.bat
Tools/unity-accelerator.ps1
```

## NOTES

- `NetworkDialogueService.cs` is ~9k LOC — the monolith of the project. Approach with care.
- ML-Agents package is a local override, not from the Unity registry. Changes to `../com.unity.ml-agents` affect this project.
- ParticlePack and StarterAssets are 3rd-party — do not modify.
- No git repository initialized at project root.
- Behavior scene (`Behavior_Scene.unity`) is the primary test scene for all dialogue/NPC work.
- Effects use Addressables with Resources fallback — assets must be marked Addressable in Editor.

## HIERARCHY

```
AGENTS.md (this file)
├── Assets/Network_Game/AGENTS.md
│   ├── Assets/Network_Game/Dialogue/AGENTS.md
│   │   └── Assets/Network_Game/Dialogue/Effects/AGENTS.md
│   └── Assets/Network_Game/Behavior/Unity Behavior Example/AGENTS.md
```
