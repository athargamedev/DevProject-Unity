# Behavior Scene AGENTS

## Scope

Applies to the Behavior_Scene runtime environment:
- `Assets/Network_Game/Behavior/Unity Behavior Example/Behavior_Scene.unity`
- `Assets/Network_Game/Behavior/Unity Behavior Example/NPC_Behavior_Graph.asset`
- `Assets/Network_Game/Behavior/Unity Behavior Example/BehaviorSceneBootstrap.cs`
- `Assets/Network_Game/Behavior/Unity Behavior Example/Actions/*`

## Scripts in this directory

| File | Role |
|------|------|
| `BehaviorSceneBootstrap.cs` | Scene-level orchestrator: spawn, camera, NPC wiring |
| `NetworkBootstrap.cs` | Transport, host/client startup, connection approval |
| `NetworkBootstrapEvents.cs` | Static event bus for network lifecycle |
| `PlayerBootstrap.cs` | Local player readiness, ownership |
| `AuthBootstrap.cs` | Auth gate integration at scene level |
| `NPCAgentBootstrap.cs` | ML-Agents NPC initialization |
| `RuntimeBinder.cs` | Late-binding helper for scene references |
| `SceneCameraManager.cs` | Camera follow + rebinding for local player |

## Key components in scene

| Object | Component | Role |
|--------|-----------|------|
| NetworkDialogueService | `NetworkDialogueService` | Server dialogue router |
| Canvas_Dialogue | `DialogueClientUI` | Player chat UI |
| NPC_StormOracle | `NpcDialogueActor` + `BehaviorGraphAgent` | Persona NPC |
| NPC_ForgeKeeper | `NpcDialogueActor` + `BehaviorGraphAgent` | Persona NPC |
| NPC_Archivist | `NpcDialogueActor` + `BehaviorGraphAgent` | Persona NPC |
| DialogueSceneEffectsController | `DialogueSceneEffectsController` | Effect dispatch |

## Bootstrap contract

`BehaviorSceneBootstrap` runs at execution order `-100` and:
1. Starts host if needed.
2. Resolves or spawns local player.
3. Runs post-spawn rebind loop (camera, dialogue participants, auth, blackboard).
4. Runs optional continuous camera rebind monitor.
5. Ensures local auth login and attaches to spawned player.
6. Collects NPC agents tagged `NPC`.
7. Wires blackboard values per NPC: `Self`, `Player`, `Waypoints`, `Speed`, `DistanceThreshold`, `WaitSeconds`.
8. Disables per-NPC/player `LLMAgent` components (enforces single owner on `NetworkDialogueService`).
9. Rebinds `DialogueClientUI` participants.

## Dialogue integration

- Dialogue initiated via `DialogueClientUI`, routed through `NetworkDialogueService`.
- Behavior graph dialogue actions are **optional and disabled by default**.
- Effects triggered by dialogue response text via `DialogueSceneEffectsController`.
- Graph dialogue variables (when enabled): `Prompt`, `ConversationKey`, `OutputText`, `LastBlockReason`.

## Workflow

1. Reproduce in `Behavior_Scene.unity`.
2. Validate bootstrap wiring before changing graph logic.
3. Run persona sanity test after any dialogue-persona change.
4. Check `NG:Bootstrap` logs for wiring issues.

## Do not do

- Do not hardcode temporary listener objects when `Player` shared variable can be used.
- Do not remove `NetworkDialogueService`/`DialogueClientUI` without replacing integration path.
- Do not enable repeat-no-guard NPC prompts in root loop.
- Do not enable per-NPC `LLMAgent` components — bootstrap disables them intentionally.
