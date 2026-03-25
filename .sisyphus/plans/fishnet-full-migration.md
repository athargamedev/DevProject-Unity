# FishNet Full Migration Plan

## TL;DR

> **Objective**: Replace Unity Netcode for GameObjects (NGO) with FishNet v4.6.22 across the entire project.
> 
> **Deliverables**:
> - FishNet NetworkManager replacing NGO NetworkManager
> - Migrated bootstrap system (transport, host/client)
> - Migrated dialogue system with improved RPCs and SyncTypes
> - Migrated player spawning and ownership
> - Migrated combat health system
> 
> **Estimated Effort**: XL (large multi-week effort)
> **Parallel Execution**: YES - 4 waves
> **Critical Path**: Wave 1 Foundation → Wave 2 Dialogue Core → Wave 3 Integration → Wave 4 Testing

---

## Context

### Current Architecture (NGO)
- **NetworkManager**: Unity's `Unity.Netcode.NetworkManager`
- **Transport**: Unity Transport (UTP)
- **Dialogue**: `NetworkDialogueService` (~9k LOC) with `ClientRpc`
- **Player**: Custom `NetworkBootstrap`, `PlayerBootstrap`
- **Auth**: Custom `LocalPlayerAuthService` (non-network, local)

### Target Architecture (FishNet)
- **NetworkManager**: FishNet's `FishNet.Managing.NetworkManager`
- **Transport**: Tugboat (built-in) or Multipass
- **Dialogue**: FishNet `NetworkBehaviour` with `ServerRpc`/`TargetRpc`/`ObserversRpc`
- **Sync**: `SyncList`, `SyncDictionary`, `SyncVar` for automatic state sync

### Migration Scope
- **IN**: All NGO networking code → FishNet
- **OUT**: Non-network code (LLM integration, effects pipeline, UI)

---

## Work Objectives

### Core Objective
Complete replacement of Unity Netcode for GameObjects with FishNet while preserving all game functionality.

### Concrete Deliverables
1. FishNet NetworkManager in scene
2. Migrated `NetworkBootstrap` (host/client, transport)
3. Migrated `NetworkDialogueService` with FishNet RPCs
4. Migrated `NpcDialogueActor` with FishNet RPCs
5. Migrated player spawning (`PlayerBootstrap`)
6. Migrated combat (`CombatHealthV2`)
7. Working multiplayer dialogue in test scene

### Definition of Done
- [ ] Host starts successfully with FishNet
- [ ] Client connects to host
- [ ] Player spawns and has ownership
- [ ] Dialogue works: Player → NPC → Response
- [ ] Effects propagate to all clients
- [ ] Combat (health) syncs across clients

### Must Have
- Server-authoritative dialogue (LLM never runs on client)
- Player ownership for actions
- Effect broadcast to all connected clients

### Must NOT Have
- No dual networking stacks (NGO + FishNet simultaneously)
- No LLM processing on clients (security)
- No ownership bypasses

---

## Verification Strategy

### Test Decision
- **Infrastructure**: FishNet has built-in testing, use existing test patterns
- **Manual Testing**: Required - multiplayer must be tested with 2+ players
- **QA Scenarios**: See each task for specific verification

### QA Policy
Every task includes agent-executed QA scenarios for verification.

---

## Execution Strategy

### Wave Structure

```
Wave 1 (Foundation - Setup FishNet):
├── T1: Install FishNet and remove NGO dependency conflicts
├── T2: Create FishNet NetworkManager setup in scene
├── T3: Migrate transport configuration (Tugboat)
├── T4: Create FishNet version of NetworkBootstrap
├── T5: Run FishNet codegen
└── T6: Test basic host/client connection

Wave 2 (Dialogue Core - Migrate Dialogue System):
├── T7: Migrate NetworkDialogueService base class
├── T8: Replace ClientRpc with ObserversRpc/TargetRpc
├── T9: Add SyncList for conversation history
├── T10: Migrate NpcDialogueActor
├── T11: Migrate DialogueSceneEffectsController
└── T12: Test dialogue flow in multiplayer

Wave 3 (Player & Gameplay - Complete Migration):
├── T13: Migrate PlayerBootstrap (spawn/ownership)
├── T14: Migrate CombatHealthV2
├── T15: Migrate PlayerObjectIdentity
├── T16: Migrate NetworkPlayerSetup
├── T17: Update scene references
└── T18: Test full gameplay loop

Wave 4 (Integration & Polish):
├── T19: Verify all components work together
├── T20: Performance testing
├── T21: Edge case handling
└── T22: Documentation update

Critical Path: T1 → T4 → T7 → T10 → T13 → T19 → Complete
```

---

## TODOs

### Wave 1: Foundation

- [ ] 1. **Install FishNet and Resolve Dependencies**

  **What to do**:
  - Remove or disable Unity.Netcode package
  - Ensure FishNet package.json dependencies are met
  - Check for assembly definition conflicts
  - Run FishNet codegen via menu: Tools → Fish-Net → Cooking → Generate

  **Must NOT do**:
  - Don't delete NGO code yet - keep for reference
  - Don't modify non-network code

  **References**:
  - FishNet docs: https://fish-networking.gitbook.io/docs/
  - `Assets/FishNet/package.json` - dependency versions

  **QA Scenarios**:
  - Scenario: Fresh project load
    - Steps: Open Unity, check Console for FishNet warnings
    - Expected: No errors related to FishNet

  **Commit**: YES
  - Message: `chore: install FishNet v4.6.22`

---

- [ ] 2. **Create FishNet NetworkManager Scene Setup**

  **What to do**:
  - Add FishNet NetworkManager to scene
  - Configure default settings (frame rate, server tick rate)
  - Add to bootstrap object or create new

  **References**:
  - FishNet Runtime/Managing/NetworkManager.cs

  **QA Scenarios**:
  - Scenario: NetworkManager in scene
    - Steps: Check for FishNet NetworkManager component
    - Expected: Component visible in Inspector

  **Commit**: YES

---

- [ ] 3. **Migrate Transport to FishyUnityTransport**

  **What to do**:
  - Keep existing Unity Transport (UTP) - don't remove it
  - Install FishNet's FishyUnityTransport (wraps UTP for FishNet)
  - Configure port (matching current: 7778 for host)
  - Enable `Use WebSockets` for WebGL builds
  - Keep existing `WebGLTransportAdapter` logic

  **Must NOT do**:
  - Don't remove Unity Transport package
  - Don't break existing port configuration
  - Don't lose WebGL WebSocket support

  **References**:
  - FishNet docs: https://fish-networking.gitbook.io/docs/manual/guides/components/transports/fishyunitytransport
  - Current: `Assets/Network_Game/Core/WebGLTransportAdapter.cs`

  **QA Scenarios**:
  - Scenario: Transport configuration
    - Steps: Check NetworkManager transport property
    - Expected: FishyUnityTransport selected with correct port
  - Scenario: WebGL mode
    - Steps: Check WebGL build uses WebSockets
    - Expected: WebSocket enabled in WebGL

  **Commit**: YES

---

- [ ] 4. **Create FishNet NetworkBootstrap**

  **What to do**:
  - Create new `FishNetworkBootstrap.cs` based on current `NetworkBootstrap.cs`
  - Replace `Unity.Netcode.NetworkManager` with `FishNet.Managing.NetworkManager`
  - Replace NGO callbacks with FishNet:
    - `OnClientConnectedCallback` → `OnClientConnectionState`
    - `OnClientDisconnectCallback` → same
  - Migrate host/client startup logic
  - Migrate port fallback logic

  **Must NOT do**:
  - Don't copy NGO code directly - adapt to FishNet patterns
  - Don't remove fallback port logic

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Behavior/NetworkBootstrap.cs` (keep for reference)
  - FishNet: `Assets/FishNet/Runtime/Managing/NetworkManager.cs`

  **QA Scenarios**:
  - Scenario: Host starts
    - Steps: Run in editor as host, check logs
    - Expected: "Server started" or similar FishNet message
  - Scenario: Client connects
    - Steps: Run second instance as client
    - Expected: Client connects to host

  **Commit**: YES
  - Message: `feat: add FishNet bootstrap system`

---

- [ ] 5. **Run FishNet Codegen**

  **What to do**:
  - Run: Tools → Fish-Net → Cooking → Generate All
  - Check for any codegen errors
  - Fix any missing base classes

  **QA Scenarios**:
  - Scenario: Codegen completes
    - Steps: Run codegen, check Console
    - Expected: "FishNet Code Generation Complete" with no errors

  **Commit**: YES

---

- [ ] 6. **Test Basic Host/Client Connection**

  **What to do**:
  - Start host in editor
  - Connect client (second editor or build)
  - Verify basic connection works

  **QA Scenarios**:
  - Scenario: Host/client connection
    - Tool: Play in editor (host) + build (client)
    - Steps: Start host, start client, verify connection
    - Expected: Client shows connected, host shows client joined
  - Scenario: Disconnect
    - Steps: Disconnect client
    - Expected: Clean disconnect, no errors

  **Commit**: YES
  - Message: `test: verify FishNet basic connection`

---

### Wave 2: Dialogue Core

- [ ] 7. **Migrate NetworkDialogueService Base Class**

  **What to do**:
  - Change base class from `NetworkBehaviour` (NGO) to `FishNet.Object.NetworkBehaviour`
  - Remove `using Unity.Netcode` → add `using FishNet.Object`
  - Keep all existing logic intact
  - Create backup: `NetworkDialogueService.cs.backup`

  **Must NOT do**:
  - Don't change dialogue logic yet - only base class
  - Don't break LLM integration

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Dialogue/NetworkDialogueService.cs`
  - FishNet: `Assets/FishNet/Runtime/Object/NetworkBehaviour/NetworkBehaviour.cs`

  **QA Scenarios**:
  - Scenario: Scene loads with dialogue service
    - Steps: Open Behavior_Scene, check for errors
    - Expected: No compilation errors

  **Commit**: YES
  - Message: `refactor: migrate NetworkDialogueService to FishNet`

---

- [ ] 8. **Replace ClientRpc with FishNet RPCs**

  **What to do**:
  - Replace NGO `ClientRpc` attributes with FishNet equivalents:
    - `ClientRpc` (broadcast) → `ObserversRpc`
    - `ServerRpc` (already similar) → `ServerRpc`
  - For private responses: Use `TargetRpc`
  - Add `RunLocally = true` where needed for local execution
  - Migrate RPC parameters (FishNet uses `ServerRpcParams`)

  **Specific RPCs to migrate**:
  - `SendDialogueResponseClientRpc` → `ObserversRpc` or `TargetRpc`
  - `ApplyBoredLightingEffectClientRpc` → `ObserversRpc`
  - Other effect ClientRpcs → `ObserversRpc`

  **Must NOT do**:
  - Don't change message content/format
  - Don't break existing effect parameters

  **QA Scenarios**:
  - Scenario: Dialogue response sent
    - Steps: Trigger NPC dialogue, check response
    - Expected: Response appears on client
  - Scenario: Effect triggered
    - Steps: Trigger effect from dialogue
    - Expected: Effect plays on all clients

  **Commit**: YES

---

- [ ] 9. **Add SyncList for Conversation History**

  **What to do**:
  - Add `SyncList<string>` or `SyncList<ChatMessage>` for conversation history
  - This automatically syncs to observers
  - Replace manual history sync with built-in sync

  **Pattern Reference**:
  - FishNet: `Assets/FishNet/Runtime/Object/NetworkBehaviour/Synchronizing/SyncList.cs`

  **QA Scenarios**:
  - Scenario: New player joins mid-conversation
    - Steps: Start conversation, have second player join
    - Expected: New player sees conversation history

  **Commit**: YES

---

- [ ] 10. **Migrate NpcDialogueActor**

  **What to do**:
  - Change base class to FishNet `NetworkBehaviour`
  - Migrate RPCs
  - Keep NPC persona binding

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Dialogue/NpcDialogueActor.cs`

  **QA Scenarios**:
  - Scenario: NPC speaks
    - Steps: Trigger NPC dialogue
    - Expected: NPC speech appears on client

  **Commit**: YES

---

- [ ] 11. **Migrate DialogueSceneEffectsController**

  **What to do**:
  - Migrate to FishNet
  - Keep effect dispatch logic
  - Update ClientRpc calls to ObserversRpc

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Dialogue/Effects/DialogueSceneEffectsController.cs`

  **QA Scenarios**:
  - Scenario: Effect plays
    - Steps: Trigger dialogue that causes effect
    - Expected: Effect visible on all clients

  **Commit**: YES

---

- [ ] 12. **Test Dialogue Flow in Multiplayer**

  **What to do**:
  - Full dialogue test: Player talks to NPC, gets response
  - Multiplayer: 2+ players, both see dialogue
  - Effects: Both see effects

  **QA Scenarios**:
  - Scenario: Full dialogue test
    - Steps: Host talks to NPC, gets LLM response
    - Expected: Dialogue appears, response is correct
  - Scenario: Multiplayer dialogue
    - Steps: Player 1 talks to NPC, Player 2 watches
    - Expected: Both see dialogue and response

  **Commit**: YES

---

### Wave 3: Player & Gameplay

- [ ] 13. **Migrate PlayerBootstrap**

  **What to do**:
  - Migrate to FishNet patterns
  - Keep player spawn logic
  - Update ownership handling

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Behavior/Unity Behavior Example/PlayerBootstrap.cs`

  **QA Scenarios**:
  - Scenario: Player spawns
    - Steps: Connect to server
    - Expected: Player object spawns with correct ownership

  **Commit**: YES

---

- [ ] 14. **Migrate CombatHealthV2**

  **What to do**:
  - Change to FishNet `NetworkBehaviour`
  - Migrate RPCs for health changes

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Combat/CombatHealthV2.cs`

  **QA Scenarios**:
  - Scenario: Health change syncs
    - Steps: Take damage in multiplayer
    - Expected: Health syncs to all clients

  **Commit**: YES

---

- [ ] 15. **Migrate PlayerObjectIdentity**

  **What to do**:
  - Migrate to FishNet

  **Pattern Reference**:
  - Current: `Assets/Network_Game/Behavior/PlayerObjectIdentity.cs`

  **QA Scenarios**:
  - Scenario: Identity works
    - Steps: Check player identity
    - Expected: Correct client ID associated

  **Commit**: YES

---

- [ ] 16. **Migrate NetworkPlayerSetup**

  **What to do**:
  - Migrate to FishNet

  **Pattern Reference**:
  - Current: `Assets/Network_Game/ThirdPersonController/Scripts/NetworkPlayerSetup.cs`

  **Commit**: YES

---

- [ ] 17. **Update Scene References**

  **What to do**:
  - Update all scene objects to use FishNet components
  - Check NetworkManager references
  - Check bootstrap references

  **QA Scenarios**:
  - Scenario: Scene loads
    - Steps: Open Behavior_Scene
    - Expected: No missing component errors

  **Commit**: YES

---

- [ ] 18. **Test Full Gameplay Loop**

  **What to do**:
  - Host → Client connect
  - Player spawn
  - Move around
  - Talk to NPC
  - Trigger effects
  - Take damage

  **QA Scenarios**:
  - Scenario: Full gameplay
    - Steps: Complete gameplay loop
    - Expected: Everything works

  **Commit**: YES

---

### Wave 4: Integration & Polish

- [ ] 19. **Verify All Components Work Together**

  **What to do**:
  - Run extended multiplayer session
  - Check for race conditions
  - Verify all systems integrate

  **Commit**: YES

---

- [ ] 20. **Performance Testing**

  **What to do**:
  - Test with 4+ players
  - Check bandwidth usage
  - Verify no memory leaks

  **Commit**: NO

---

- [ ] 21. **Edge Case Handling**

  **What to do**:
  - Handle disconnect mid-dialogue
  - Handle player join/leave during dialogue
  - Handle host migration (if supported)

  **Commit**: YES

---

- [ ] 22. **Documentation Update**

  **What to do**:
  - Update AGENTS.md for FishNet
  - Document any new patterns

  **Commit**: YES

---

## Final Verification Wave

- [ ] F1. **Plan Compliance Audit** — Verify all Must Haves implemented
- [ ] F2. **Code Quality Review** — Check for proper FishNet usage
- [ ] F3. **Real Manual QA** — Full multiplayer test
- [ ] F4. **Scope Fidelity Check** — Ensure no NGO code remains

### WebGL Transport Decision
- **Selected**: FishyUnityTransport (wraps Unity Transport)
- **Rationale**: Keeps existing WebGL/WebSocket setup, easiest migration
- **Future Enhancement**: Can add Tugboat via Multipass later for native builds

---

## Success Criteria

### Verification Commands
- [ ] Host starts: `Start as Host` button works
- [ ] Client connects: `Start as Client` connects to host
- [ ] Dialogue works: Player → NPC → Response visible
- [ ] Effects work: Effects sync to all clients
- [ ] Combat works: Health syncs across clients

### Final Checklist
- [ ] All NGO references removed (or documented)
- [ ] All FishNet patterns followed
- [ ] Tests pass
- [ ] Multiplayer verified
