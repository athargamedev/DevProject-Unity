# Network Game AGENTS

## Scope

All gameplay code under `Assets/Network_Game/` — auth, bootstraps, combat, dialogue, effects, player controller, diagnostics, and editor tooling. Single `Network_Game` assembly (asmdef) with `Network_Game` root namespace.

## System Map

| Subsystem | Key Files | LOC |
|-----------|-----------|-----|
| Auth & identity | `Auth/LocalPlayerAuthService.cs` | 1322 |
| Network bootstrap | `Behavior/Unity Behavior Example/NetworkBootstrap.cs`, `NetworkBootstrapEvents.cs` | ~600 |
| Scene wiring | `Behavior/Unity Behavior Example/BehaviorSceneBootstrap.cs` | ~500 |
| Player spawn | `Behavior/Unity Behavior Example/PlayerBootstrap.cs` | ~300 |
| Dialogue router | `Dialogue/NetworkDialogueService.cs` | 9178 |
| Dialogue UI | `Dialogue/DialogueClientUI.cs`, `Dialogue/UI/` | ~5500 |
| NPC actors | `Dialogue/NpcDialogueActor.cs`, `NpcDialogueProfile.cs` | ~1250 |
| Effects pipeline | `Dialogue/Effects/` (17 scripts) | ~10500 |
| ML-Agents | `Dialogue/Scripts/NpcDialogueAgent.cs`, `LlmDialogueChannel.cs` | ~1000 |
| MCP bridge | `Dialogue/MCP/DialogueMCPBridge.cs` | 3354 |
| Combat | `Combat/CombatHealth.cs`, `CombatRuntimeOverlay.cs` | ~1000 |
| Player control | `ThirdPersonController/Scripts/ThirdPersonController.cs`, `FlyModeController.cs` | ~500 |
| Diagnostics | `Diagnostics/NGLog.cs`, `DebugWatchdog.cs`, `LlmDebugAssistant.cs` | ~600 |

## Shared Contracts

1. `NetworkBootstrap` owns transport mode and connection approval.
2. `PlayerBootstrap` owns local-player discovery/readiness and host fallback spawn.
3. `LocalPlayerAuthService` owns login state and prompt-context initialization.
4. `ThirdPersonController` remains owner-authoritative for runtime control.
5. `NetworkDialogueService` is the ONLY server-authoritative dialogue router.
6. Editor custom tools are the preferred fast path for dialogue diagnostics and profile automation.

## Preferred Tooling

- Register MCP custom tools first: `Network Game/MCP/Admin/Register All Custom Tools`
- Use `ng_pipeline_status` and `ng_get_full_diagnostics` before manual dialogue deep-dives
- Log categories: `NG:Auth`, `NG:NetworkBootstrap`, `NG:PlayerBootstrap`, `NG:Dialogue`, `NG:DialogueUI`, `NG:DialogueFX`, `NG:LLMChat`, `NG:DialogueLoRA`, `NG:DialogueSanity`

## Do Not Do

- Do not bypass auth, spawn, or dialogue routers with temporary side paths unless explicitly a test harness.
- Do not fix owner/authority bugs by enabling systems on every client.
- Do not trade correctness for temporary convenience in multiplayer flow.
- Do not modify `ParticlePack/` or `StarterAssets/` — 3rd-party assets.
