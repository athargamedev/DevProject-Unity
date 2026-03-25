# Diagnostics System Cleanup - Summary

## Date: 2026-03-24

## Overview
Significantly reduced the Diagnostics system from ~35 files to 8 core files, removing over-engineered LLM diagnostic features while keeping essential logging and scene workflow tracking.

## Files Kept (8)

### Core/ (1 file)
- `NGLog.cs` - Essential logging facade used throughout the project (~300+ references)

### Contracts/ (5 files)
- `AuthoritativeSceneSnapshot.cs` - Scene snapshot data structure
- `AuthorityEnums.cs` - AuthorityRole, AuthorityCapability enums
- `AuthoritySnapshot.cs` - Network authority state
- `SceneObjectDescriptor.cs` - Object metadata for scene projection
- `SceneWorkflowStateBridge.cs` - Interface for startup milestone queries

### Tracing/ (2 files)
- `AuthoritySnapshotBuilder.cs` - Builds authority snapshots
- `SceneProjectionBuilder.cs` - Builds scene object descriptors for NPC context

### Runtime/
- `SceneWorkflowDiagnostics.cs` - Tracks 9 startup milestones

## Files Removed (~27 files)

### Brain/ (deleted entirely)
- `DiagnosticBrainRuntime.cs` - LLM diagnostic brain
- `DiagnosticBrainSession.cs` - LLM session management
- `DiagnosticBreakpointAnchorResolver.cs` - Breakpoint resolution
- `DiagnosticPriorityEngine.cs` - Priority scoring
- `DiagnosticPromptComposer.cs` - LLM prompt building

### Tracing/ Stores (removed)
- `DialogueActionValidationStore.cs`
- `DialogueExecutionTraceStore.cs`
- `DialogueInferenceEnvelopeStore.cs`
- `DialogueReplicationTraceStore.cs`
- `UiDiagnosticsStore.cs`

### Root Level (removed)
- `DebugWatchdog.cs` - Inspector panel with 30+ fields
- `DialogueFlowDiagnostics.cs` - Timeline tracking with flow records
- `LlmDebugAssistant.cs` - LLM error analysis
- `InferenceWatchReporter.cs` - Bridge to DebugWatchdog

### Contracts/ (removed)
- `DiagnosticActionChainSummary.cs`
- `DiagnosticActionRecommendation.cs`
- `DiagnosticBrainPacket.cs`
- `DiagnosticBrainSeverity.cs`
- `DiagnosticBrainVariable.cs`
- `DiagnosticBrainVariableKind.cs`
- `DiagnosticBreakpointAnchor.cs`
- `DialogueActionValidationResult.cs`
- `DialogueExecutionTrace.cs`
- `DialogueInferenceEnvelope.cs`
- `DialogueReplicationTrace.cs`
- `DialoguePromptContextBridge.cs`
- `InferenceWatchBridge.cs`
- `LoginUiBridge.cs`
- `MutableSurfaceDescriptor.cs`
- `NetworkBootstrapEventsBridge.cs`
- `DiagnosticsRuntimeBridge.cs`

## Changes Made

### 1. BehaviorSceneBootstrap.cs
- Removed `DiagnosticBrainRuntime` component creation
- Removed `DialogueFlowDiagnostics` component creation
- Removed `m_AutoCreateLlmDebugAssistant` field

### 2. RuntimeBinder.cs
- Removed `m_AutoCreateLlmDebugAssistant` field
- Removed `m_EnableDebugAssistantOnClients` field
- Removed `EnsureDebugAssistant()` method

### 3. SceneObjectDescriptor.cs
- Removed `MutableSurfaces` field (was using deleted `MutableSurfaceDescriptor`)

### 4. SceneProjectionBuilder.cs
- Removed `BuildMutableSurfaces()` method
- Removed `RequiresOwnerAuthority()` helper

### 5. AuthorityEnums.cs
- Removed `MutableSurfaceKind` enum

### 6. SceneWorkflowDiagnostics.cs
- Kept class but removed some unused features
- Kept `ISceneWorkflowStateBridge` implementation (needed by Dialogue)
- Added explicit registration with `SceneWorkflowStateBridgeRegistry`

### 7. SceneWorkflowStateBridge.cs (restored)
- Created minimal version with just the interface and registry
- Needed by `DialogueClientUI` and `ModernDialogueController`

## Result
- **Before**: ~35 files, complex LLM diagnostic system
- **After**: 8 files, focused logging and startup tracking
- **Reduction**: ~77% fewer files
- **Compilation**: Clean build
- **Functionality Preserved**:
  - NGLog (all logging)
  - Scene workflow milestone tracking
  - Scene projection for NPC context
  - Authority snapshot building

## Dependencies Still Using Diagnostics
- `DialogueClientUI.cs` - Uses `ISceneWorkflowStateBridge` for startup checks
- `ModernDialogueController.cs` - Uses `ISceneWorkflowStateBridge` for startup checks
- All systems using `NGLog` for logging
