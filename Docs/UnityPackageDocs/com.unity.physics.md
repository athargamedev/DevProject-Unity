# Unity Physics overview

Package: `com.unity.physics`
Manifest version: `1.4.5`
Lock version: `file:com.unity.physics`
Docs lookup version: `1.4.5`
Docs stream: `1.4`
Resolved docs version: `1.4.5`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-physics #unity/editor/6000-4 #unity/physics #unity/dots #unity/ecs

## Summary
The Unity Physics package, part of Unity's Data-Oriented Technology Stack (DOTS), provides a deterministic rigid body dynamics system and spatial query system.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.27`
- [[com.unity.collections]] `2.6.5`
- [[com.unity.entities]] `1.4.5`
- [[com.unity.mathematics]] `1.3.2`
- [[com.unity.modules.imgui]] `1.0.0`
- [[com.unity.modules.jsonserialize]] `1.0.0`
- [[com.unity.test-framework]] `1.4.6`

### Required By
- [[com.unity.charactercontroller]]

## Manual Map
- Unity Physics package
- What's new
- Get started
- Installation
- ECS packages
- Physics engine overview
- Design philosophy
- The simulation pipeline
- Simulation setup demonstration
- Principal data components
- Rigid bodies
- Colliders
- Compound colliders
- Joints
- Motors
- Authoring
- Physics Step
- Built-in physics authoring

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Physics`: 148 documented types
- `Unity.Physics.Authoring`: 38 documented types
- `Unity.Physics.Systems`: 21 documented types
- `Unity.Physics.GraphicsIntegration`: 11 documented types
- `Unity.Physics.Aspects`: 10 documented types
- `Unity.Physics.Extensions`: 6 documented types
- `Unity.DebugDisplay`: 1 documented types
- `Unity.Physics.Hybrid`: 1 documented types

## API Type Index
- `ColorIndex` in `Unity.DebugDisplay`
- `Aabb` in `Unity.Physics`
- `AllHitsCollector` in `Unity.Physics`
- `AnyHitCollector` in `Unity.Physics`
- `BlobArray` in `Unity.Physics`
- `BlobArray.Accessor` in `Unity.Physics`
- `BlobArray.Accessor .Enumerator` in `Unity.Physics`
- `BodyFrame` in `Unity.Physics`
- `BodyIndexPair` in `Unity.Physics`
- `BoxCollider` in `Unity.Physics`
- `BoxGeometry` in `Unity.Physics`
- `CapsuleCollider` in `Unity.Physics`
- `CapsuleGeometry` in `Unity.Physics`
- `ChildCollider` in `Unity.Physics`
- `ClosestHitCollector` in `Unity.Physics`
- `Collider` in `Unity.Physics`

## API Type Details
### `ColorIndex` (Struct)
- Namespace: `Unity.DebugDisplay`
- Summary: Color utility for physics debug display.
- Page: https://docs.unity3d.com/Packages/com.unity.physics@1.4/api/Unity.DebugDisplay.ColorIndex.html
- Fields:
- `Black`: Black.
- `Blue`: Blue.
- `BrightBlack`: BrightBlack.
- `BrightBlue`: BrightBlue.
- `BrightCyan`: BrightCyan.
### `Aabb` (Struct)
- Namespace: `Unity.Physics`
- Summary: An axis-aligned bounding box, or AABB for short, is a box aligned with coordinate axes and fully enclosing some object.
- Page: https://docs.unity3d.com/Packages/com.unity.physics@1.4/api/Unity.Physics.Aabb.html
- Fields:
- `Empty`: Create an empty, invalid AABB.
- `Max`: The maximum point.
- `Min`: The minimum point.
- Properties:
- `Center`: Gets the center.
- `Extents`: Gets the extents.
- `IsValid`: Gets a value indicating whether this aabb is valid.
- `SurfaceArea`: Gets the surface area.
- Methods:
- `ClosestPoint(float3)`: Returns the closest point on the bounds of the AABB to the specified position.
- `Contains(float3)`: Query if this aabb contains the given point.
- `Contains(Aabb)`: Query if this aabb contains the given aabb.
- `Expand(float)`: Expands the aabb by the provided distance.
- `Include(float3)`: Includes the given point in the aabb.
### `AllHitsCollector<T>` (Struct)
- Namespace: `Unity.Physics`
- Summary: A collector which stores every hit.
- Page: https://docs.unity3d.com/Packages/com.unity.physics@1.4/api/Unity.Physics.AllHitsCollector-1.html
- Constructors:
- `AllHitsCollector(float, ref NativeList<T>)`: Constructor.
- Fields:
- `AllHits`: All hits.
- Properties:
- `EarlyOutOnFirstHit`: Gets a value indicating whether the early out on first hit.
- `MaxFraction`: Gets the maximum fraction.
- `NumHits`: Gets the number of hits.
- Methods:
- `AddHit(T)`: Adds a hit.

## Related Packages
- [[com.unity.charactercontroller]]: shared signals #unity/dots #unity/ecs #unity/physics #unity/dependency-graph
- [[com.unity.entities]]: shared signals #unity/dots #unity/ecs #unity/dependency-graph
- [[com.unity.burst]]: shared signals #unity/dependency-graph
- [[com.unity.collections]]: shared signals #unity/dependency-graph
- [[com.unity.mathematics]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.physics@1.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.physics@1.4/api/index.html
- Package index: [[Unity Package Docs Index]]
