# Character Controller package

Package: `com.unity.charactercontroller`
Manifest version: `1.4.2`
Lock version: `file:com.unity.charactercontroller`
Docs lookup version: `1.4.2`
Docs stream: `1.4`
Resolved docs version: `1.4.2`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-charactercontroller #unity/editor/6000-4 #unity/character-controller #unity/gameplay #unity/movement #unity/ecs #unity/physics #unity/dots

## Summary
The Character Controller package provides mechanisms for creating character controllers with Unity's Entity Component System (ECS) . A character controller allows you to quickly configure common character movement, such as walking, jumping, and character collision.

## Package Graph
### Depends On
- [[com.unity.entities]] `1.3.15`
- [[com.unity.modules.physics]] `1.0.0`
- [[com.unity.modules.uielements]] `1.0.0`
- [[com.unity.physics]] `1.3.15`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Character Controller
- Get started
- Standard characters implementation overview
- First-person character setup
- Third-person character setup
- Character controller concepts
- Character update
- Character components
- Grounding
- Parenting
- Character movement
- Grounding customization
- Slope management
- Jumping
- Step handling
- Limit sliding along walls
- Character interactions
- Collision activation

## API Overview
This page contains an overview of some key APIs of the Character Controller package.

### Interfaces
- `IKinematicCharacterProcessor`: Interface for implementing various customizable functions within the character update. Users are expected to create their own character processor that implements this interface…

### Authorings
- `TrackedTransformAuthoring`: Used to add a TrackedTransform component on entities that can be a character "parent".

### Components
- `KinematicCharacterProperties`: Contains the character data that defines how it behaves. Nothing in the character update will write to this component; it only reads from it.
- `KinematicCharacterBody`: Contains the character data that may get written to by the character update.
- `StoredKinematicCharacterData`: Stores key character data before the character update (data that a character A might need to access on a character B). This allows deterministic parallel execution of the…
- `KinematicCharacterHit`: DynamicBuffer containing all hits that were detected during the character update.
- `StatefulKinematicCharacterHit`: DynamicBuffer containing all hits that were detected during the character update, but with state information (Enter/Exit/Stay).
- `KinematicCharacterDeferredImpulse`: DynamicBuffer containing impulses added during the character update (to be processed later in a single-threaded system)

### Systems
- `KinematicCharacterPhysicsUpdateGroup`: Provides a sensible default update point for fixed-rate character physics update systems (used by Standard Characters).
- `KinematicCharacterDeferredImpulsesSystem`: Handles applying impulses stored in the KinematicCharacterDeferredImpulse buffer, after the character update.
- `StoreKinematicCharacterBodyPropertiesSystem`: Handles storing character data in the StoredKinematicCharacterData component. This allows deterministic parallel execution of the character update.
- `TrackedTransformFixedSimulationSystem`: Handles storing "previous transform" data in all TrackedTransform components.
- `CharacterInterpolationRememberTransformSystem`: Handles remembering the "previous transform" used for interpolation calculations.
- `CharacterInterpolationSystem`: Handles interpolating the character visual transform.

### Utilities
- `KinematicCharacterUtilities`: Contains various functions related to character queries, character entity creation, character baking, and others.
- `CharacterControlUtilities`: Contains various functions related to controlling the character velocity & rotation.
- `MathUtilities`: Contains various math helper functions.
- `PhysicsUtilities`: Contains various physics helper functions.

### Aspects
- `KinematicCharacterAspect`: An aspect containing all base character data and update steps. This is only for preserving backwards compatibility with the previous workflow for creating character controllers.

## API Namespaces
- Namespace TOC was not available for this package.

## API Type Index
- No API type list was extracted.

## API Type Details
- No detailed API type pages were extracted.

## Related Packages
- [[com.unity.physics]]: shared signals #unity/dots #unity/ecs #unity/physics #unity/dependency-graph
- [[com.unity.entities]]: shared signals #unity/dots #unity/ecs #unity/dependency-graph
- [[com.unity.addressables]]: shared signals #unity/gameplay
- [[com.unity.cinemachine]]: shared signals #unity/gameplay
- [[com.unity.dedicated-server]]: shared signals #unity/gameplay

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.charactercontroller@1.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.charactercontroller@1.4/api/index.html
- Package index: [[Unity Package Docs Index]]
