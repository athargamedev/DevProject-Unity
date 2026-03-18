# Diagnostic Brain Sprint 1 Implementation Spec

Author: Codex
Project: DevProject
Date: 2026-03-18
Status: Draft for implementation

## 1. Sprint 1 Outcome

Sprint 1 establishes the structured runtime contract that later diagnostics and AI tooling will depend on.

This sprint does **not** attempt to solve all debugging UX. It makes the runtime explainable in four areas:

1. NGO authority and ownership state
2. curated scene projection for dialogue
3. LLM inference request envelope visibility
4. top-priority blocker detection for spawn/auth/authority/dialogue readiness

At the end of Sprint 1, an AI agent or human operator should be able to answer:

1. Who owns the local player, and is the player actually playable?
2. What exact scene snapshot is available to NPC dialogue?
3. What exact inference configuration was used for the most recent dialogue request?
4. What is the current top blocker, based on structured facts rather than console spam?

## 2. Non-Goals

Sprint 1 does **not** include:

1. full action-plan parsing and execution tracing
2. full RPC replay/timeline editor window
3. effect execution validation
4. combat replication analysis
5. automatic code-fix generation from the brain
6. arbitrary property mutation exposure to the LLM

Those belong to Sprint 2+.

## 3. Existing Project Anchors

Sprint 1 must reuse and integrate with these existing files instead of building parallel systems:

1. `Assets/Network_Game/Diagnostics/NGLog.cs`
2. `Assets/Network_Game/Diagnostics/SceneWorkflowDiagnostics.cs`
3. `Assets/Network_Game/Diagnostics/DialogueFlowDiagnostics.cs`
4. `Assets/Network_Game/Diagnostics/DebugWatchdog.cs`
5. `Assets/Network_Game/Auth/LocalPlayerAuthService.cs`
6. `Assets/Network_Game/Behavior/Unity Behavior Example/NetworkBootstrap.cs`
7. `Assets/Network_Game/Behavior/Unity Behavior Example/NetworkBootstrapEvents.cs`
8. `Assets/Network_Game/Behavior/Unity Behavior Example/PlayerBootstrap.cs`
9. `Assets/Network_Game/Dialogue/DialogueSceneTargetRegistry.cs`
10. `Assets/Network_Game/Dialogue/NetworkDialogueService.cs`
11. `Assets/Network_Game/Dialogue/OpenAIChatClient.cs`
12. `Assets/Network_Game/Dialogue/MCP/DialogueMCPBridge.cs`
13. `Assets/Network_Game/Dialogue/DialogueClientUI.cs`
14. `Assets/Network_Game/ThirdPersonController/Scripts/ThirdPersonController.cs`

## 4. Deliverables

### 4.1 New folders

Create:

1. `Assets/Network_Game/Diagnostics/Contracts/`
2. `Assets/Network_Game/Diagnostics/Tracing/`
3. `Assets/Network_Game/Diagnostics/Brain/`
4. `Assets/Editor/Diagnostics/`

### 4.2 New runtime classes

Create these files in Sprint 1:

1. `Assets/Network_Game/Diagnostics/Contracts/AuthorityEnums.cs`
2. `Assets/Network_Game/Diagnostics/Contracts/AuthoritySnapshot.cs`
3. `Assets/Network_Game/Diagnostics/Contracts/MutableSurfaceDescriptor.cs`
4. `Assets/Network_Game/Diagnostics/Contracts/SceneObjectDescriptor.cs`
5. `Assets/Network_Game/Diagnostics/Contracts/AuthoritativeSceneSnapshot.cs`
6. `Assets/Network_Game/Diagnostics/Contracts/DialogueInferenceEnvelope.cs`
7. `Assets/Network_Game/Diagnostics/Tracing/AuthoritySnapshotBuilder.cs`
8. `Assets/Network_Game/Diagnostics/Tracing/SceneProjectionBuilder.cs`
9. `Assets/Network_Game/Diagnostics/Tracing/DialogueInferenceEnvelopeStore.cs`
10. `Assets/Network_Game/Diagnostics/Brain/DiagnosticBrainSeverity.cs`
11. `Assets/Network_Game/Diagnostics/Brain/DiagnosticBrainVariableKind.cs`
12. `Assets/Network_Game/Diagnostics/Brain/DiagnosticBrainVariable.cs`
13. `Assets/Network_Game/Diagnostics/Brain/DiagnosticBrainPacket.cs`
14. `Assets/Network_Game/Diagnostics/Brain/DiagnosticBrainSession.cs`
15. `Assets/Network_Game/Diagnostics/Brain/DiagnosticPriorityEngine.cs`
16. `Assets/Network_Game/Diagnostics/Brain/DiagnosticPromptComposer.cs`
17. `Assets/Network_Game/Diagnostics/Brain/DiagnosticBrainRuntime.cs`

### 4.3 Existing files to modify

Modify these files in Sprint 1:

1. `Assets/Network_Game/Behavior/Unity Behavior Example/BehaviorSceneBootstrap.cs`
2. `Assets/Network_Game/Behavior/Unity Behavior Example/NetworkBootstrap.cs`
3. `Assets/Network_Game/Behavior/Unity Behavior Example/NetworkBootstrapEvents.cs`
4. `Assets/Network_Game/Behavior/Unity Behavior Example/PlayerBootstrap.cs`
5. `Assets/Network_Game/Auth/LocalPlayerAuthService.cs`
6. `Assets/Network_Game/Dialogue/NetworkDialogueService.cs`
7. `Assets/Network_Game/Dialogue/OpenAIChatClient.cs`
8. `Assets/Network_Game/Dialogue/MCP/DialogueMCPBridge.cs`
9. `Assets/Network_Game/Diagnostics/DebugWatchdog.cs`

### 4.4 Optional Sprint 1 editor helper

If time allows, create:

1. `Assets/Editor/Diagnostics/DiagnosticBrainPacketMenu.cs`

This can expose simple menu items to dump packets to the Console or clipboard.

## 5. Data Contracts

## 5.1 AuthorityEnums.cs

Namespace: `Network_Game.Diagnostics`

Define:

```csharp
public enum AuthorityRole
{
    Unknown = 0,
    Server = 1,
    Host = 2,
    ClientOwner = 3,
    ClientObserver = 4,
}

public enum AuthorityCapability
{
    None = 0,
    SpawnPlayer = 1,
    ApproveConnection = 2,
    DriveInput = 3,
    MutateDialogueContext = 4,
    IssueDialogueRequest = 5,
    ExecuteSceneMutation = 6,
    BroadcastEffect = 7,
}

public enum MutableSurfaceKind
{
    Unknown = 0,
    Transform = 1,
    Material = 2,
    Animation = 3,
    GameplayStat = 4,
    EffectSocket = 5,
}
```

Notes:

1. `AuthorityCapability` is descriptive in Sprint 1, not a generic permission engine.
2. Flags are allowed if useful, but not required.

## 5.2 AuthoritySnapshot.cs

Namespace: `Network_Game.Diagnostics`

Purpose:
Represents the authoritative runtime truth for the current local peer and player.

Required fields:

```csharp
[Serializable]
public struct AuthoritySnapshot
{
    public string RunId;
    public string BootId;
    public string SceneName;
    public int Frame;
    public float RealtimeSinceStartup;

    public bool NetworkManagerPresent;
    public bool IsListening;
    public bool IsServer;
    public bool IsHost;
    public bool IsClient;
    public bool IsConnectedClient;
    public ulong LocalClientId;

    public bool LocalPlayerResolved;
    public ulong LocalPlayerNetworkObjectId;
    public ulong LocalPlayerOwnerClientId;
    public string LocalPlayerObjectName;
    public bool LocalPlayerIsSpawned;
    public bool LocalPlayerIsOwner;

    public bool LocalControllerPresent;
    public bool LocalControllerEnabled;
    public bool LocalInputComponentPresent;
    public bool LocalInputEnabled;
    public string LocalActionMap;
    public bool CameraFollowAssigned;

    public bool AuthServicePresent;
    public bool HasAuthenticatedPlayer;
    public string AuthNameId;
    public ulong AuthAttachedNetworkObjectId;
    public bool PromptContextInitialized;
    public bool PromptContextAppliedToDialogue;

    public string CurrentPhase;
    public string Summary;
}
```

Required computed helpers:

1. `bool HasAuthorityBlocker`
2. `string ResolvePrimaryAuthorityProblem()`
3. `AuthorityRole ResolveLocalRole()`

## 5.3 MutableSurfaceDescriptor.cs

Namespace: `Network_Game.Diagnostics`

Purpose:
Whitelisted object mutation surface for scene projection.

Required fields:

```csharp
[Serializable]
public struct MutableSurfaceDescriptor
{
    public MutableSurfaceKind Kind;
    public string SurfaceId;
    public string DisplayName;
    public string[] AllowedProperties;
    public string[] AllowedOperations;
    public string RequiredAuthority;
    public bool Replicated;
}
```

Examples:

1. Material surface: `_BaseColor`, `_EmissionColor`, `alpha`
2. Animation surface: `SetTrigger`, `PlayStateTag`
3. Transform surface: `teleport`, `impulse`, `face_target`

Sprint 1 rule:
Only describe surfaces. Do not add mutation execution here.

## 5.4 SceneObjectDescriptor.cs

Namespace: `Network_Game.Diagnostics`

Purpose:
Curated object representation for dialogue scene projection and future validation.

Required fields:

```csharp
[Serializable]
public struct SceneObjectDescriptor
{
    public string ObjectId;
    public string SemanticId;
    public string DisplayName;
    public string Role;
    public string[] Aliases;

    public bool IsNetworkObject;
    public ulong NetworkObjectId;
    public ulong OwnerClientId;
    public bool IsSpawned;

    public Vector3 Position;
    public Vector3 EulerAngles;
    public Vector3 BoundsSize;
    public float DistanceFromProbe;

    public string RendererSummary;
    public string MaterialSummary;
    public string MeshSummary;
    public string AnimationSummary;
    public string GameplaySummary;

    public MutableSurfaceDescriptor[] MutableSurfaces;
}
```

Rules:

1. `ObjectId` should be stable for the session. Recommended format: `semantic:<id>` else `net:<id>` else `path:<scene path>`.
2. `RendererSummary`, `MaterialSummary`, `MeshSummary`, `AnimationSummary`, `GameplaySummary` should be concise strings, not raw dumps.
3. `GameplaySummary` must stay small enough for prompt composition.

## 5.5 AuthoritativeSceneSnapshot.cs

Namespace: `Network_Game.Diagnostics`

Purpose:
Curated scene snapshot suitable for MCP export and prompt composition.

Required fields:

```csharp
[Serializable]
public struct AuthoritativeSceneSnapshot
{
    public string SnapshotId;
    public string SnapshotHash;
    public string SceneName;
    public int Frame;
    public float RealtimeSinceStartup;

    public ulong ProbeListenerNetworkObjectId;
    public string ProbeListenerName;
    public Vector3 ProbeOrigin;
    public float MaxDistance;

    public string SemanticSummary;
    public SceneObjectDescriptor[] Objects;
}
```

Rules:

1. Snapshot hash may be a compact deterministic hash over object ids and key fields.
2. Target size for Sprint 1: max 24 objects in the structured snapshot, ordered by semantic priority then distance.
3. Use `DialogueSceneTargetRegistry` as the primary source of scene relevance.

## 5.6 DialogueInferenceEnvelope.cs

Namespace: `Network_Game.Diagnostics`

Purpose:
First-class record of what was actually sent to the dialogue backend.

Required fields:

```csharp
[Serializable]
public struct DialogueInferenceEnvelope
{
    public string EnvelopeId;
    public string FlowId;
    public int RequestId;
    public int ClientRequestId;
    public ulong RequestingClientId;
    public ulong SpeakerNetworkId;
    public ulong ListenerNetworkId;
    public string ConversationKey;

    public string BackendName;
    public string EndpointLabel;
    public string ModelName;

    public string SystemPromptId;
    public string PromptTemplateId;
    public string PromptTemplateVersion;

    public string SceneSnapshotId;
    public string SceneSnapshotHash;

    public float Temperature;
    public float TopP;
    public int TopK;
    public float FrequencyPenalty;
    public float PresencePenalty;
    public float RepeatPenalty;
    public int MaxTokens;
    public string[] StopSequences;

    public int PromptCharCount;
    public int SceneSnapshotCharCount;
    public int PromptTokenEstimate;

    public float EnqueuedAt;
    public float StartedAt;
    public int RetryCount;

    public string PromptPreview;
}
```

Rules:

1. `PromptPreview` must be truncated. Recommended limit: 600 chars.
2. `SystemPromptId` and `PromptTemplateId` are allowed to start as fallback constants if the current system has not yet externalized them.
3. If the exact model name is unavailable from the current inference path, return `"unknown"` and keep the field.

## 6. Builders and Stores

## 6.1 AuthoritySnapshotBuilder.cs

Purpose:
Build an `AuthoritySnapshot` from live runtime state without mutating gameplay.

Namespace: `Network_Game.Diagnostics`

Public API:

```csharp
public static class AuthoritySnapshotBuilder
{
    public static AuthoritySnapshot Build(string runId, string bootId);
}
```

Data sources:

1. `NetworkManager.Singleton`
2. `PlayerBootstrap` state if available
3. `LocalPlayerAuthService.Instance`
4. local `NetworkObject`
5. local `PlayerInput`
6. `ThirdPersonController`
7. `SceneCameraManager` or current camera follow target
8. `SceneWorkflowDiagnostics`

Implementation rules:

1. No side effects.
2. No allocations inside per-frame loops unless necessary.
3. Use existing public properties first. Do not add reflection.

## 6.2 SceneProjectionBuilder.cs

Purpose:
Build the curated `AuthoritativeSceneSnapshot`.

Namespace: `Network_Game.Diagnostics`

Public API:

```csharp
public static class SceneProjectionBuilder
{
    public static AuthoritativeSceneSnapshot Build(
        ulong probeListenerNetworkObjectId = 0,
        float maxDistance = 120f,
        int maxObjects = 24);
}
```

Primary sources:

1. `DialogueSceneTargetRegistry`
2. `DialogueMCPBridge.BuildSceneSnapshotText()` for compatibility
3. `DialogueSemanticTag`
4. `NetworkObject`
5. `Animator`
6. `Renderer` and `MeshFilter` metadata

Object inclusion order:

1. semantic-tagged objects included in snapshots
2. local player
3. target NPCs
4. closest gameplay-relevant objects

Material summary rules:

1. list material names
2. do not dump shader internals
3. when possible, expose concise mutable material slots and approved properties

Animation summary rules:

1. current animator state tag or state name
2. layer 0 only in Sprint 1
3. allowed trigger names only if explicitly curated later; otherwise leave empty

## 6.3 DialogueInferenceEnvelopeStore.cs

Purpose:
Retain recent inference envelopes in memory for MCP export and the brain.

Namespace: `Network_Game.Diagnostics`

Public API:

```csharp
public sealed class DialogueInferenceEnvelopeStore : MonoBehaviour
{
    public static DialogueInferenceEnvelopeStore Instance { get; }
    public void Record(DialogueInferenceEnvelope envelope);
    public bool TryGetLatest(out DialogueInferenceEnvelope envelope);
    public bool TryGetByRequestId(int requestId, out DialogueInferenceEnvelope envelope);
    public DialogueInferenceEnvelope[] GetRecent(int maxCount = 10);
}
```

Retention:

1. ring buffer size 64
2. keep only this session
3. do not write to disk in Sprint 1

## 7. Brain Runtime

Sprint 1 uses a minimal but structured brain.

## 7.1 DiagnosticBrainSeverity.cs

```csharp
public enum DiagnosticBrainSeverity
{
    P0 = 0,
    P1 = 1,
    P2 = 2,
    P3 = 3,
}
```

## 7.2 DiagnosticBrainVariableKind.cs

```csharp
public enum DiagnosticBrainVariableKind
{
    Fact = 0,
    Focus = 1,
    Hypothesis = 2,
    Suppression = 3,
    Goal = 4,
}
```

## 7.3 DiagnosticBrainVariable.cs

Required fields:

```csharp
[Serializable]
public struct DiagnosticBrainVariable
{
    public string Key;
    public DiagnosticBrainVariableKind Kind;
    public DiagnosticBrainSeverity Severity;
    public string Phase;
    public string Value;
    public float Confidence;
    public string Source;
    public bool Pinned;
    public float CreatedAt;
    public float ExpiresAt;
}
```

Rules:

1. Keys must be namespaced, for example `focus.local_player_missing`.
2. `Value` should be concise human-readable text or compact JSON-like text.
3. `ExpiresAt <= 0` means no expiry.

## 7.4 DiagnosticBrainPacket.cs

Required fields:

```csharp
[Serializable]
public struct DiagnosticBrainPacket
{
    public string RunId;
    public string BootId;
    public string Objective;
    public string SceneName;
    public string CurrentPhase;

    public AuthoritySnapshot Authority;
    public AuthoritativeSceneSnapshot SceneSnapshot;
    public DialogueInferenceEnvelope LatestEnvelope;

    public DiagnosticBrainVariable[] TopPriorities;
    public DiagnosticBrainVariable[] ActiveFacts;
    public DiagnosticBrainVariable[] ActiveSuppressions;

    public string Summary;
}
```

## 7.5 DiagnosticBrainSession.cs

Purpose:
Own the current variable set and emit packets.

Public API:

```csharp
public sealed class DiagnosticBrainSession : MonoBehaviour
{
    public static DiagnosticBrainSession Instance { get; }

    public string RunId { get; }
    public string Objective { get; set; }

    public void UpsertVariable(DiagnosticBrainVariable variable);
    public void RemoveVariable(string key);
    public bool TryGetVariable(string key, out DiagnosticBrainVariable variable);
    public DiagnosticBrainVariable[] GetActiveVariables();
    public DiagnosticBrainPacket BuildPacket();
}
```

Rules:

1. One singleton for the session.
2. Run id should regenerate each Play Mode session.
3. Boot id should use `SceneWorkflowDiagnostics.ActiveBootId` when available.

## 7.6 DiagnosticPriorityEngine.cs

Purpose:
Rank the active variables.

Public API:

```csharp
public static class DiagnosticPriorityEngine
{
    public static DiagnosticBrainVariable[] GetTopPriorities(
        IReadOnlyList<DiagnosticBrainVariable> variables,
        int maxCount = 5);
}
```

Priority rules for Sprint 1:

1. `P0` always outranks `P1+`
2. `Focus` outranks `Fact`
3. spawn/authority blockers outrank dialogue blockers
4. suppressions never appear in top priorities
5. pinned items get a small positive boost, but must not override severity class

## 7.7 DiagnosticPromptComposer.cs

Purpose:
Convert the packet into AI-facing text.

Public API:

```csharp
public static class DiagnosticPromptComposer
{
    public static string Compose(DiagnosticBrainPacket packet);
}
```

Prompt layout:

1. objective
2. current phase
3. top priorities
4. authority snapshot summary
5. scene snapshot summary
6. latest inference envelope summary
7. suppressions

No raw console dumps in Sprint 1.

## 7.8 DiagnosticBrainRuntime.cs

Purpose:
Drive packet refresh and rule evaluation.

Public API:

```csharp
public sealed class DiagnosticBrainRuntime : MonoBehaviour
{
    [SerializeField] private float m_RefreshIntervalSeconds = 0.5f;
}
```

Responsibilities:

1. ensure `DiagnosticBrainSession` exists
2. ensure `DialogueInferenceEnvelopeStore` exists
3. rebuild authority snapshot on interval
4. rebuild scene snapshot on interval
5. evaluate detector rules
6. keep the current packet ready for MCP export

Recommended execution order:

1. after `SceneWorkflowDiagnostics`
2. after `DialogueFlowDiagnostics`
3. before any optional debug UI refresh

## 8. Detector Rules

Sprint 1 detectors are intentionally narrow.

## 8.1 P0 blockers

### `focus.network_not_ready`

Inject when:

1. `NetworkManagerPresent == false`, or
2. `IsListening == false` after scene bootstrap has completed for more than 2 seconds

Clear when:

1. `IsListening == true`

### `focus.auth_identity_missing`

Inject when:

1. `AuthServicePresent == true`
2. `HasAuthenticatedPlayer == false`
3. current phase is `auth_gate` or later

Clear when:

1. `HasAuthenticatedPlayer == true`

### `focus.local_player_missing`

Inject when:

1. `IsClient == true || IsHost == true`
2. `auth_gate_passed` milestone is complete
3. `LocalPlayerResolved == false`

Clear when:

1. `LocalPlayerResolved == true`

### `focus.local_player_not_owner`

Inject when:

1. `LocalPlayerResolved == true`
2. `LocalPlayerIsSpawned == true`
3. `LocalPlayerIsOwner == false`

Clear when:

1. `LocalPlayerIsOwner == true`

### `focus.local_input_disabled`

Inject when:

1. `LocalPlayerResolved == true`
2. `LocalPlayerIsOwner == true`
3. `LocalInputComponentPresent == true`
4. `LocalInputEnabled == false`

Clear when:

1. `LocalInputEnabled == true`

## 8.2 P1 blockers

### `focus.prompt_context_not_applied`

Inject when:

1. `HasAuthenticatedPlayer == true`
2. `LocalPlayerResolved == true`
3. `PromptContextInitialized == true`
4. `PromptContextAppliedToDialogue == false`

Clear when:

1. `PromptContextAppliedToDialogue == true`

### `focus.dialogue_backend_unready`

Inject when:

1. dialogue service exists
2. latest envelope is absent for current request attempts, or
3. `WarmupDegraded == true`, or
4. backend config is missing in a scenario where dialogue requests are already being attempted

Clear when:

1. the service is healthy and a valid envelope can be recorded

### `focus.scene_snapshot_empty`

Inject when:

1. scene snapshot object count is zero, or
2. snapshot has no local player and no semantic targets

Clear when:

1. snapshot contains at least one player or NPC plus one semantic object

## 8.3 Suppressions

### `suppress.transport_bind_noise`

Inject when:

1. transport bind warnings are present and match current noise rules

Rules:

1. suppressions must appear in the packet
2. suppressions must not outrank blockers

## 9. Integration Changes

## 9.1 BehaviorSceneBootstrap.cs

Add:

1. ensure `DiagnosticBrainRuntime` exists during composition
2. emit runtime composition facts into the brain after `scene_bootstrap_ready`

No gameplay behavior changes.

## 9.2 NetworkBootstrap.cs and NetworkBootstrapEvents.cs

Add or expose enough state for `AuthoritySnapshotBuilder` to resolve:

1. current mode
2. listening state
3. local client id
4. last network error summary

Prefer public read-only accessors over reflection.

## 9.3 PlayerBootstrap.cs

Add minimal state exposure:

1. last resolved local player object
2. whether ownership handoff was attempted
3. whether `EnableLocalInput` ran successfully

Recommended accessors:

```csharp
public GameObject CurrentResolvedLocalPlayer { get; }
public bool LastEnableLocalInputSucceeded { get; }
public string LastEnableLocalInputReason { get; }
```

Do not rewrite player bootstrap logic in Sprint 1.

## 9.4 LocalPlayerAuthService.cs

Add read-only accessors for:

1. whether prompt context is initialized
2. whether prompt context was last applied successfully
3. current attachment network object id

Recommended accessors:

```csharp
public bool IsPromptContextInitialized { get; }
public bool LastPromptContextApplySucceeded { get; }
public ulong LocalPlayerNetworkId { get; }
```

The builder should consume these, not infer them from logs.

## 9.5 NetworkDialogueService.cs

Add a hook that records a `DialogueInferenceEnvelope` whenever a request is about to be sent to the remote backend.

Minimum fields available from current code:

1. `RequestId`
2. `ClientRequestId`
3. `RequestingClientId`
4. `SpeakerNetworkId`
5. `ListenerNetworkId`
6. `ConversationKey`
7. `ActiveInferenceBackendName`
8. `RemoteInferenceEndpoint`
9. `DialogueRequest.Prompt`
10. timing and retry counters from `DialogueRequestState`

Prompt template metadata may initially use constants:

1. `SystemPromptId = "dialogue-default"`
2. `PromptTemplateId = "network-dialogue-service"`
3. `PromptTemplateVersion = "sprint1"`

## 9.6 OpenAIChatClient.cs

If this class owns concrete request options, expose or forward:

1. `temperature`
2. `top_p`
3. `top_k`
4. penalties
5. `max_tokens`
6. stop sequences
7. model name if available

If some values are unavailable at this layer, allow `DialogueInferenceEnvelope` to keep defaults such as `-1` or `"unknown"`.

## 9.7 DialogueMCPBridge.cs

Add these methods:

```csharp
public static Dictionary<string, object> GetAuthoritySnapshot();
public static Dictionary<string, object> GetAuthoritativeSceneSnapshot(
    ulong probeListenerNetworkObjectId = 0,
    float maxDistance = 120f,
    int maxObjects = 24);
public static Dictionary<string, object> GetLatestInferenceEnvelope();
public static Dictionary<string, object> GetDiagnosticBrainPacket();
public static string GetDiagnosticBrainPrompt();
```

Mapping rules:

1. return dictionaries/lists for MCP compatibility
2. keep shapes stable
3. prefer empty results over thrown exceptions

## 9.8 DebugWatchdog.cs

Add optional fields or methods to mirror:

1. top current blocker
2. authority summary
3. prompt-context application state
4. latest inference envelope summary

Sprint 1 requirement:
Do not duplicate the entire brain in serialized fields.

## 10. MCP Response Shapes

## 10.1 `GetAuthoritySnapshot()`

Return keys:

1. `scene_name`
2. `frame`
3. `is_listening`
4. `is_server`
5. `is_host`
6. `is_client`
7. `local_client_id`
8. `local_player_resolved`
9. `local_player_network_object_id`
10. `local_player_owner_client_id`
11. `local_player_is_owner`
12. `local_input_enabled`
13. `auth_name_id`
14. `prompt_context_initialized`
15. `prompt_context_applied`
16. `summary`

## 10.2 `GetAuthoritativeSceneSnapshot()`

Return keys:

1. `snapshot_id`
2. `snapshot_hash`
3. `scene_name`
4. `probe_origin`
5. `probe_listener_network_object_id`
6. `semantic_summary`
7. `objects`

Each object should contain:

1. `object_id`
2. `semantic_id`
3. `display_name`
4. `role`
5. `network_object_id`
6. `position`
7. `distance`
8. `material_summary`
9. `animation_summary`
10. `gameplay_summary`
11. `mutable_surfaces`

## 10.3 `GetLatestInferenceEnvelope()`

Return keys:

1. `envelope_id`
2. `request_id`
3. `client_request_id`
4. `flow_id`
5. `backend_name`
6. `endpoint_label`
7. `model_name`
8. `system_prompt_id`
9. `prompt_template_id`
10. `prompt_template_version`
11. `scene_snapshot_id`
12. `scene_snapshot_hash`
13. `temperature`
14. `top_p`
15. `top_k`
16. `frequency_penalty`
17. `presence_penalty`
18. `repeat_penalty`
19. `max_tokens`
20. `stop_sequences`
21. `prompt_char_count`
22. `prompt_token_estimate`
23. `retry_count`
24. `prompt_preview`

## 10.4 `GetDiagnosticBrainPacket()`

Return keys:

1. `objective`
2. `scene_name`
3. `current_phase`
4. `authority`
5. `scene_snapshot`
6. `latest_envelope`
7. `top_priorities`
8. `active_facts`
9. `active_suppressions`
10. `summary`

## 11. Performance Constraints

Sprint 1 must remain safe for Play Mode and future WebGL profiling.

Rules:

1. `DiagnosticBrainRuntime` refresh interval defaults to `0.5s`
2. scene snapshot building must not run every frame
3. inference envelope recording only occurs on request dispatch
4. ring buffers only, no unbounded lists
5. no JSON serialization every frame
6. prompt previews must be truncated
7. builders must avoid scanning the entire scene more than necessary
8. `DialogueSceneTargetRegistry` should remain the preferred source for target selection

## 12. Acceptance Criteria

Sprint 1 is complete when all of the following are true.

1. `DialogueMCPBridge.GetAuthoritySnapshot()` returns a stable structured snapshot in Play Mode.
2. `DialogueMCPBridge.GetAuthoritativeSceneSnapshot()` returns a curated object list with semantic IDs and mutable surfaces.
3. the latest dialogue request can be inspected via `GetLatestInferenceEnvelope()`.
4. the brain packet ranks spawn/authority failures above dialogue warnings.
5. a missing local player produces `focus.local_player_missing`.
6. a non-owner local player produces `focus.local_player_not_owner`.
7. an authenticated player with missing prompt-context application produces `focus.prompt_context_not_applied`.
8. an empty scene projection produces `focus.scene_snapshot_empty`.
9. the composed brain prompt is shorter and more actionable than raw log output.

## 13. Manual Validation Checklist

Use `Behavior_Scene.unity`.

Test pass 1: normal host startup

1. start host
2. confirm authority snapshot reports host/server/listening
3. confirm local player is resolved and owner
4. confirm prompt context is initialized/applied
5. confirm scene snapshot includes player, NPCs, and semantic environment objects

Test pass 2: broken local player spawn

1. force a reproduction where local player is not found
2. confirm top priority is `focus.local_player_missing`
3. confirm no lower-severity dialogue warning outranks it

Test pass 3: ownership/input break

1. reproduce a case where player exists but is not owner or input is disabled
2. confirm priority is `focus.local_player_not_owner` or `focus.local_input_disabled`

Test pass 4: dialogue request visibility

1. send a player->NPC dialogue request
2. inspect latest inference envelope
3. confirm backend, endpoint, prompt preview, token estimate, penalties, and max tokens are visible

Test pass 5: scene projection quality

1. inspect scene snapshot
2. confirm object descriptors include material summary, animation summary, gameplay summary, and allowed mutable surfaces

## 14. Implementation Order

Implement in this exact order:

1. contract structs/enums
2. authority snapshot builder
3. scene projection builder
4. inference envelope store
5. dialogue request envelope hook
6. brain variable/session/runtime
7. detector rules
8. MCP export methods
9. optional watchdog summary fields

Do not start editor UX work until the MCP packet is stable.

## 15. Code Review Focus

When reviewing Sprint 1, check these risks first:

1. builder code accidentally mutates runtime state
2. snapshot generation allocates too much or scans too broadly
3. detector rules produce stale variables that never clear
4. authority blockers are ranked below dialogue noise
5. inference envelope fields diverge from actual runtime request values
6. scene object descriptors expose too much raw engine state
7. MCP methods throw in Edit Mode or when services are absent

## 16. Next Sprint Dependencies

Sprint 2 will depend on Sprint 1 exposing stable ids and snapshots for:

1. action validation
2. dialogue action plan tracing
3. replication trace
4. UI behavior/performance trace
5. breakpoint hints

That means Sprint 1 must optimize for stable contracts over fancy presentation.
