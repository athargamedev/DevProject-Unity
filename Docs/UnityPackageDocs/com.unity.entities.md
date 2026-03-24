# Entities package

Package: `com.unity.entities`
Manifest version: `Not listed`
Lock version: `6.4.0`
Docs lookup version: `6.4.0`
Docs stream: `6.4`
Resolved docs version: `6.4.0`
Source: `builtin`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-entities #unity/editor/6000-4 #unity/ecs #unity/dots

## Summary
The Entities package is part of Unity's Data-Oriented Technology Stack (DOTS). It provides a data-oriented implementation of the Entity Component System (ECS) architecture.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.23`
- [[com.unity.collections]] `6.4.0`
- [[com.unity.mathematics]] `1.3.2`
- [[com.unity.modules.assetbundle]] `1.0.0`
- [[com.unity.modules.audio]] `1.0.0`
- [[com.unity.modules.physics]] `1.0.0`
- [[com.unity.modules.uielements]] `1.0.0`
- [[com.unity.modules.unityanalytics]] `1.0.0`
- [[com.unity.modules.unitywebrequest]] `1.0.0`
- [[com.unity.nuget.mono-cecil]] `1.11.5`
- [[com.unity.profiling.core]] `1.0.2`
- [[com.unity.scriptablebuildpipeline]] `2.4.3`
- [[com.unity.serialization]] `3.1.2`
- [[com.unity.test-framework.performance]] `3.0.3`

### Required By
- [[com.unity.charactercontroller]]
- [[com.unity.physics]]

## Manual Map
- Entities package
- What's new
- Upgrade guide
- Get started
- Installation
- ECS packages
- ECS workflow examples
- Introduction to the ECS workflow
- Starter ECS workflow
- Authoring and baking workflow
- Prefab instantiation workflow
- Make a system multithreaded
- Use entity command buffer for structural changes
- Entity component system concepts
- Entity component system introduction
- Entity concepts
- Component concepts
- System concepts

## API Overview
This page contains an overview of some key APIs that make up Unity's Entity Component System (ECS).

### Entity types
- `Entity`: The fundamental identifier in ECS.
- `EntityArchetype`: A unique combination of component types. For more information, see Archetype concepts .
- `EntityQuery`: Select entities with specific characteristics. For more information, see Querying entity data with an entity query .
- `EntityQueryBuilder`: Create EntityQuery objects.
- `EntityManager`: Manages entities and provides utility methods.
- `World`: An isolated collection of entities. For more information see World concepts

### Component types
- `IComponentData`: A marker interface for general purpose components.
- `ISharedComponentData`: A marker interface for components that more than one entity shares. For moe information, see Shared components .
- `ICleanupComponentData`: A marker interface for specialized system components. For more information, see Cleanup components .
- `IBufferElementData`: A marker interface for buffer elements. For more information, see Buffer components .
- `DynamicBuffer`: Access buffer elements.
- `BlobAssetReference`: A reference to a blob asset in a component.

### System types
- `ISystem`: An interface to create systems from.
- `ComponentSystemBase`: Defines a set of basic functionality for systems. For more information, see Creating systems with SystemBase .
- `SystemBase`: The base class to extend when writing a system.
- `ComponentSystemGroup`: A group of systems that update as a unit. For more information, see System groups .

### ECS job types
- `IJobEntity`: An interface to implicitly create a job that iterates over the entities. For more information, see Iterate over component data with IJobEntity .
- `Entities.ForEach`: An implicitly created job that iterates over a set of entities. Warning: Entities.ForEach is deprecated and will be removed in a future release. Use IJobEntity or SystemAPI.Query…
- `Job.WithCode`: An implicitly created single job.
- `IJobChunk`: An interface to explicitly create a job that iterates over the chunks matched by an entity query. For more information, see Iterate over data with IJobChunk .

### Other important types
- `ArchetypeChunk`: The storage unit for entity components.
- `EntityCommandBuffer`: A buffer for recording entity modifications used to reduce structural changes. For more information see Scheduling data changes with an EntityCommandBuffer .
- `ComponentType`: Define types when creating entity queries.
- `BlobBuilder`: A utility class to create blob assets, which are immutable data structures that can be safely shared between jobs using BlobAssetReference instances.
- `ICustomBootstrap`: An interface to implement to create your own system loop.
- `SystemAPI`: Class that gives access to access to buffers, components, time, enumeration, singletons and more. This also includes any IAspect , IJobEntity , SystemBase , and ISystem .

### Attributes
- `UpdateInGroup`: Defines the ComponentSystemGroup that a system should be added to.
- `UpdateBefore`: Specifies that one system must update before another.
- `UpdateAfter`: Specifies that one system must update after another
- `DisableAutoCreation`: Prevents a system from being automatically discovered and run when your application starts up
- `ExecuteAlways`: Specifies that a system's update function must be invoked every frame, even when no entities are returned by the system's entity query.

## API Namespaces
- `Unity.Entities`: 299 documented types
- `Unity.Entities.UniversalDelegates`: 165 documented types
- `Unity.Scenes`: 25 documented types
- `Unity.Entities.Content`: 23 documented types
- `Unity.Entities.Serialization`: 13 documented types
- `Unity.Transforms`: 11 documented types
- `Unity.Scenes.Editor`: 6 documented types
- `Unity.Entities.Build`: 5 documented types
- `Unity.Entities.Editor`: 4 documented types
- `Unity.Mathematics`: 4 documented types
- `Unity.Entities.Hybrid.Baking`: 3 documented types
- `Unity.Core`: 2 documented types
- `Unity.Deformations`: 2 documented types
- `Unity.Entities.LowLevel.Unsafe`: 2 documented types
- `Unity.Assertions`: 1 documented types
- `Unity.Entities.Conversion`: 1 documented types

## API Type Index
- `Assert` in `Unity.Assertions`
- `TimeData` in `Unity.Core`
- `XXHash` in `Unity.Core`
- `BlendShapeWeight` in `Unity.Deformations`
- `SkinMatrix` in `Unity.Deformations`
- `AdditionalEntityParent` in `Unity.Entities`
- `AlwaysSynchronizeSystemAttribute` in `Unity.Entities`
- `AlwaysUpdateSystemAttribute` in `Unity.Entities`
- `ArchetypeChunk` in `Unity.Entities`
- `AspectType` in `Unity.Entities`
- `Asset` in `Unity.Entities`
- `BakeDerivedTypesAttribute` in `Unity.Entities`
- `BakedEntity` in `Unity.Entities`
- `Baker` in `Unity.Entities`
- `BakingOnlyEntity` in `Unity.Entities`
- `BakingSystem` in `Unity.Entities`

## API Type Details
### `Assert` (Class)
- Namespace: `Unity.Assertions`
- Summary: The intent of this class is to provide debug assert utilities that don't rely on UnityEngine, for compatibility with code that needs to run in the DOTS Runtime environment. The current implement just wraps the…
- Page: https://docs.unity3d.com/Packages/com.unity.entities@6.4/api/Unity.Assertions.Assert.html
### `TimeData` (Struct)
- Namespace: `Unity.Core`
- Summary: Encapsulates state to measure a World 's simulation time.
- Page: https://docs.unity3d.com/Packages/com.unity.entities@6.4/api/Unity.Core.TimeData.html
### `XXHash` (Class)
- Namespace: `Unity.Core`
- Summary: XXHash implementation.
- Page: https://docs.unity3d.com/Packages/com.unity.entities@6.4/api/Unity.Core.XXHash.html

## Related Packages
- [[com.unity.charactercontroller]]: shared signals #unity/dots #unity/ecs #unity/dependency-graph
- [[com.unity.physics]]: shared signals #unity/dots #unity/ecs #unity/dependency-graph
- [[com.unity.burst]]: shared signals #unity/dependency-graph
- [[com.unity.collections]]: shared signals #unity/dependency-graph
- [[com.unity.mathematics]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.entities@6.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.entities@6.4/api/index.html
- Package index: [[Unity Package Docs Index]]
