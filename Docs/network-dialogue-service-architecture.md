# NetworkDialogueService Architecture

`NetworkDialogueService` is split into partials so the multiplayer dialogue runtime can be edited by subsystem instead of as a single monolith.

## Core

- `NetworkDialogueService.cs`
  Shared types, serialized fields, and primary service state.
- `NetworkDialogueService.Runtime.cs`
  Lifecycle, backend config, and scene/runtime bootstrapping.
- `NetworkDialogueService.Metadata.cs`
  Shared utility helpers and metadata helpers.
- `NetworkDialogueService.Trace.cs`
  Flow, execution, and replication tracing.
- `NetworkDialogueService.Rpc.cs`
  Netcode RPC entrypoints and client-targeted response delivery.
- `NetworkDialogueService.PromptContext.cs`
  Prompt-context bridge methods used by auth/runtime systems.

## Requests

- `NetworkDialogueService.RequestApi.cs`
  Command-side surface for enqueue, submit, and cancel.
- `NetworkDialogueService.RequestInspection.cs`
  Response consumption, stats, and identity inspection.
- `NetworkDialogueService.RequestValidation.cs`
  Admission control and enqueue validation.
- `NetworkDialogueService.Requests.cs`
  Queue orchestration and worker scheduling.
- `NetworkDialogueService.RequestExecution.cs`
  Worker execution, inference flow, and retry logic.
- `NetworkDialogueService.RequestOutcome.cs`
  Terminal completion, telemetry, and client notification.
- `NetworkDialogueService.RequestMessaging.cs`
  Local rejection publishing and manual history append helpers.
- `NetworkDialogueService.RequestSpecialEffects.cs`
  Response rewriting and special-effect handling tied to request processing.

## Prompting And Memory

- `NetworkDialogueService.Prompting.cs`
  Top-level system prompt composition and remote prompt budgeting.
- `NetworkDialogueService.PersistentMemoryPrompting.cs`
  Persistent transcript recall and semantic memory injection.
- `NetworkDialogueService.Conversations.cs`
  Conversation state, history, and client-request lookup bookkeeping.
- `NetworkDialogueService.InferenceRuntime.cs`
  Inference client selection, warmup, and backend readiness.

## Player Context

- `NetworkDialogueService.PlayerIdentity.cs`
  Player identity cache maintenance and lookup.
- `NetworkDialogueService.PlayerPromptContext.cs`
  Runtime player-context prompt assembly.
- `NetworkDialogueService.PlayerCustomizationContext.cs`
  Customization JSON parsing, narrative hints, and player effect modifiers.

## Targeting And Actions

- `NetworkDialogueService.Targeting.cs`
  Requester-first participant resolution and network target selection.
- `NetworkDialogueService.Actions.cs`
  Structured action dispatch and delayed action handling.
- `NetworkDialogueService.StructuredEffects.cs`
  Structured special-effect normalization before execution.

## Effects

- `NetworkDialogueService.Effects.cs`
  Top-level response effect coordination.
- `NetworkDialogueService.EffectIntentExecution.cs`
  Catalog intent execution and prefab effect dispatch.
- `NetworkDialogueService.EffectDispatch.cs`
  Effect RPC transport and runtime dispatch helpers.
- `NetworkDialogueService.EffectPowerFallback.cs`
  Power fallback, probe-mode, and dynamic modifier helpers.
- `NetworkDialogueService.EffectSpatial.cs`
  Spatial policy inference and placement resolution.
- `NetworkDialogueService.EffectTargetResolution.cs`
  Target-object resolution for effect intents.
- `NetworkDialogueService.EffectTargetTokens.cs`
  Parsing helpers for player and ground target tokens.
- `NetworkDialogueService.EffectGeometry.cs`
  Shared geometry and anchor-context helpers.
- `NetworkDialogueService.EffectSceneAnchors.cs`
  Scene-object anchor parsing and semantic scene lookup.

## Editing Guidance

- Start in the narrowest partial that matches the behavior you want to change.
- Only touch `NetworkDialogueService.cs` when the change affects shared state, serialized fields, or public service-wide types.
- Request-path bugs usually live in `RequestValidation`, `Requests`, `RequestExecution`, or `RequestOutcome`.
- Prompt and memory issues usually live in `Prompting`, `PersistentMemoryPrompting`, `PlayerPromptContext`, or `PlayerCustomizationContext`.
- Multiplayer effect targeting issues usually live in `Targeting`, `EffectTargetResolution`, `EffectTargetTokens`, `EffectSceneAnchors`, or `EffectSpatial`.
