# Dialogue Assembly Restructuring Plan

## Current State Analysis

### Problem: Cyclic Dependencies

The current assembly structure has coupling issues that cause cycles:

```
Dialogue → Core/Auth/Combat/CharacterControl → Dialogue (CYCLE!)
```

### Root Cause

`Network_Game.Dialogue` references multiple other game assemblies, and at least one of them references back to Dialogue:
- `Network_Game.Core` references `Network_Game.Auth` 
- `Network_Game.Behavior` references both `Network_Game.Core` and `Network_Game.Dialogue`

### Files Causing Cross-Assembly Coupling (Dialogue folder)

| File | Uses | Problem |
|------|------|---------|
| `NetworkDialogueService.PersistentMemoryPrompting.cs` | `Network_Game.Core` | Tight coupling |
| `NetworkDialogueService.PlayerPromptContext.cs` | `Auth`, `Combat`, `Core` | Too many deps |
| `NetworkDialogueService.Prompting.cs` | `Combat` | Direct combat ref |
| `NetworkDialogueService.Runtime.cs` | `Combat` | Direct combat ref |
| `NpcProactiveTrigger.cs` | `Combat` | Direct combat ref |
| `Persistence/DialoguePersistenceGateway.cs` | `Combat`, `Core` | Too many deps |
| `Effects/EffectTargetResolverService.cs` | `Combat` | Should be isolated |
| `UI/Login/PlayerLoginController.cs` | `Auth`, `Core` | Should be in Auth |
| `UI/Profile/PlayerProfileController.cs` | `Auth` | Should be in Auth |
| `UI/ModernHudLayoutManager.cs` | `Auth` | UI-only, needs interface |

---

## Proposed Solution: Layered Architecture

### Principle: Dependencies Flow One Way

```
┌─────────────────────────────────────────────────────────────┐
│                    Unity Engine / Netcode                    │
├─────────────────────────────────────────────────────────────┤
│  Diagnostics.Contracts  │  Diagnostics.Core                  │
│  (Pure interfaces)      │  (LogLevel, TraceContext, NGLog)   │
├─────────────────────────────────────────────────────────────┤
│  CharacterControl      │  Combat                            │
│  (Pure gameplay)       │  (Pure gameplay)                   │
├─────────────────────────────────────────────────────────────┤
│  Auth                  │  Core                              │
│  (Player identity)     │  (Data/Transport)                 │
├─────────────────────────────────────────────────────────────┤
│  Dialogue (main)       │  Behavior                          │
│  (NPC dialogue)        │  (Scene orchestration)            │
└─────────────────────────────────────────────────────────────┘
```

### Rule: Lower layers CANNOT reference upper layers

- `Diagnostics.*` (lowest) → no game assembly refs
- `CharacterControl`, `Combat` → only Diagnostics + Unity
- `Auth`, `Core` → CharacterControl, Combat, Diagnostics, Unity
- `Dialogue`, `Behavior` → Auth, Core, CharacterControl, Combat, Diagnostics, Unity

---

## Folder Restructure

### Current Structure (85 files in one folder)

```
Dialogue/
├── *.cs                     (50+ files - too many!)
├── Effects/                 (VFX system)
├── Persistence/             (DB calls)
├── WebHttp/                 (HTTP abstraction)
├── UI/
│   ├── Login/
│   ├── Profile/
│   └── Dialogue/
├── Animations/
└── Scripts/
```

### Proposed Structure

```
Dialogue/
├── Core/                    # Pure dialogue logic - NO game deps
│   ├── NetworkDialogueService.cs
│   ├── NpcDialogueActor.cs
│   ├── NpcDialogueProfile.cs
│   ├── NpcProactiveTrigger.cs
│   ├── DialogueActionResponse.cs
│   ├── DialogueBackendConfig.cs
│   ├── DialogueInferenceTypes.cs
│   ├── DialogueHistoryEntry.cs
│   └── DialogueConstants.cs
│
├── Request/                 # Request lifecycle
│   ├── NetworkDialogueService.Requests.cs
│   ├── NetworkDialogueService.RequestApi.cs
│   ├── NetworkDialogueService.RequestValidation.cs
│   ├── NetworkDialogueService.RequestExecution.cs
│   ├── NetworkDialogueService.RequestOutcome.cs
│   ├── NetworkDialogueService.RequestMessaging.cs
│   ├── NetworkDialogueService.RequestInspection.cs
│   └── NetworkDialogueService.RequestSpecialEffects.cs
│
├── Prompting/               # LLM prompting
│   ├── NetworkDialogueService.Prompting.cs
│   ├── NetworkDialogueService.PromptContext.cs
│   ├── NetworkDialogueService.PlayerPromptContext.cs
│   ├── NetworkDialogueService.PersistentMemoryPrompting.cs
│   └── OpenAIChatClient.cs
│
├── Effects/                 # VFX system (keep as-is)
│   ├── DialogueSceneEffectsController.cs
│   ├── ParticleParameterExtractor.cs
│   ├── EffectDefinition.cs
│   ├── EffectCatalog.cs
│   └── ... (15 more files)
│
├── Persistence/             # External data (needs interface fix)
│   ├── DialoguePersistenceGateway.cs    # Use interface, not direct refs
│   └── DialogueMemoryWorker.cs
│
├── WebHttp/                 # Keep as-is
│   └── ...
│
├── UI/                      # UI layer (keep, extract Auth deps)
│   ├── Dialogue/
│   ├── Login/              # MOVE to Auth assembly
│   └── Profile/            # MOVE to Auth assembly
│
├── Animations/              # Keep as-is
│   └── ...
│
└── Scripts/                 # Keep as-is
    └── GameStateProvider.cs
```

---

## Implementation Steps

### Step 1: Create Interface Abstractions

Instead of direct references to `Auth`, `Combat`, `Core`, create interfaces in `Diagnostics.Contracts`:

```csharp
// In Diagnostics.Contracts
namespace Network_Game.Diagnostics.Contracts
{
    public interface IPlayerIdentityProvider
    {
        bool HasCurrentPlayer { get; }
        LocalPlayerRecord CurrentPlayer { get; }
    }
    
    public interface ICombatStateProvider
    {
        int GetPlayerHealth(ulong clientId);
        bool IsPlayerInCombat(ulong clientId);
    }
    
    public interface IGameStateProvider
    {
        string CurrentScene { get; }
        float GameTime { get; }
    }
}
```

### Step 2: Move UI Controllers to Auth Assembly

Move these files from `Dialogue/UI/` to a new or existing `Auth/UI/` folder:
- `UI/Login/PlayerLoginController.cs` → `Auth/UI/`
- `UI/Profile/PlayerProfileController.cs` → `Auth/UI/`

These are Auth features that happen to show in dialogue UI.

### Step 3: Update Dialogue asmdef

After restructuring, the Dialogue assembly should only reference:
```json
{
    "references": [
        "Unity.Netcode.Runtime",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Network_Game.Diagnostics.Contracts",  // For interfaces
        "Network_Game.Diagnostics.Core",        // For logging
        // REMOVED: Auth, Combat, Core, CharacterControl
    ]
}
```

### Step 4: Create Provider Bindings in Behavior/Core

The actual implementations of `IPlayerIdentityProvider`, `ICombatStateProvider`, etc. are bound in higher-level assemblies (Behavior or Core) and passed to Dialogue via dependency injection or singleton accessors.

Example:
```csharp
// In NetworkDialogueService - use interface, not direct type
private IPlayerIdentityProvider PlayerIdentity => 
    Diagnostics.Contracts.PlayerIdentityProvider.Current;
```

---

## Files to Refactor (Priority Order)

### High Priority (causing cycles)
1. `Persistence/DialoguePersistenceGateway.cs` - Remove direct Combat/Core refs
2. `NetworkDialogueService.PlayerPromptContext.cs` - Use interfaces
3. `UI/Login/PlayerLoginController.cs` - Move to Auth

### Medium Priority (tight coupling)
4. `NetworkDialogueService.Prompting.cs` - Use ICombatStateProvider
5. `Effects/EffectTargetResolverService.cs` - Use interface
6. `NpcProactiveTrigger.cs` - Use interface

### Lower Priority (cleanup)
7. Any remaining direct refs to Auth/Combat/Core/CharacterControl

---

## Target Dependency Graph (After Fix)

```
Diagnostics.Contracts ──────► Unity (no deps)
        │
        ▼
Diagnostics.Core ──────────► Diagnostics.Contracts, Unity.Netcode
        │
        ▼
CharacterControl ──────────► Diagnostics.Core, Unity.*
        │
        ▼
Combat ───────────────────► Diagnostics.*, Unity.Netcode
        │
        ▼
Auth ─────────────────────► Diagnostics.*, Unity.*
        │
        ▼
Core ─────────────────────► Auth, Diagnostics.*, Unity.*
        │
        ▼
Dialogue ─────────────────► Core (via interfaces), Diagnostics.*, Unity.*
        │
        ▼
Behavior ─────────────────► Dialogue, Core, Auth, Diagnostics.*, Unity.*
```

**No cycles!** Dependencies flow in one direction.

---

## Summary

1. **Move Auth-specific UI** out of Dialogue → to Auth assembly
2. **Create interfaces** in Diagnostics.Contracts for cross-assembly data access
3. **Update Dialogue asmdef** to remove direct game assembly refs
4. **Implement providers** in Behavior/Core that satisfy the interfaces

This breaks the cycle while keeping all functionality intact.
