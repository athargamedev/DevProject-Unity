# About Unity Profiling Core

Package: `com.unity.profiling.core`
Manifest version: `Not listed`
Lock version: `1.0.3`
Docs lookup version: `1.0.3`
Docs stream: `1.0`
Resolved docs version: `1.0.3`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-profiling-core #unity/editor/6000-4 #unity/profiling #unity/tooling

## Summary
Use the Unity Profiling Core package to add contextual information to the Unity Profiler captures. You can use the Scripting APIs provided with the Unity Profiling Core package to add a string or number to a Profiler sample or pass custom data to the Profiler data stream to later use in the Editor.

## Package Graph
### Depends On
- None listed in packages-lock.json

### Required By
- [[com.unity.addressables]]
- [[com.unity.entities]]
- [[com.unity.multiplayer.tools]]

## Manual Map
- Unity Profiling Core Package
- What's new
- Upgrade guide
- ProfilerMarker API guide
- Profiler Counter API guide

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Profiling`: 7 documented types

## API Type Index
- `ProfilerCategory` in `Unity.Profiling`
- `ProfilerCounterValue` in `Unity.Profiling`
- `ProfilerCounter` in `Unity.Profiling`
- `ProfilerMarkerExtension` in `Unity.Profiling`
- `ProfilerMarker` in `Unity.Profiling`
- `ProfilerMarker` in `Unity.Profiling`
- `ProfilerMarker` in `Unity.Profiling`

## API Type Details
### `ProfilerCategory` (Struct)
- Namespace: `Unity.Profiling`
- Summary: Defines a profiling category when you create a ProfilerMarker.
- Page: https://docs.unity3d.com/Packages/com.unity.profiling.core@1.0/api/Unity.Profiling.ProfilerCategory.html
- Properties:
- `Ai`: A ProfilerMarker that belongs to the Ai or NavMesh system.
- `Animation`: A ProfilerMarker that belongs to the Animation system.
- `Audio`: A ProfilerMarker that belongs the to Audio system.
- `GUI`: A ProfilerMarker that belongs to the UI system.
- `Input`: A ProfilerMarker that belongs to the Input system.
- Operators:
- `implicit operator ushort(ProfilerCategory)`: Utility operator that simplifies usage of the ProfilerCategory with ProfilerUnsafeUtility.
### `ProfilerCounterValue<T>` (Struct)
- Namespace: `Unity.Profiling`
- Summary: Reports a value of an integral or floating point type to the Unity Profiler.
- Page: https://docs.unity3d.com/Packages/com.unity.profiling.core@1.0/api/Unity.Profiling.ProfilerCounterValue-1.html
- Constructors:
- `ProfilerCounterValue(string)`: Constructs a ProfilerCounter that belongs to the generic ProfilerCategory.Scripts category. It is reported at the end of CPU frame to the…
- `ProfilerCounterValue(string, ProfilerMarkerDataUnit)`: Constructs a ProfilerCounter that belongs to the generic ProfilerCategory.Scripts category. It is reported at the end of CPU frame to the…
- `ProfilerCounterValue(string, ProfilerMarkerDataUnit, ProfilerCounterOptions)`: Constructs a ProfilerCounter that belongs to generic ProfilerCategory.Scripts category.
- `ProfilerCounterValue(ProfilerCategory, string, ProfilerMarkerDataUnit)`: Constructs a ProfilerCounter that is reported at the end of CPU frame to the Unity Profiler.
- `ProfilerCounterValue(ProfilerCategory, string, ProfilerMarkerDataUnit, ProfilerCounterOptions)`: Constructs a ProfilerCounter .
- Properties:
- `Value`: Gets or sets value of the ProfilerCounter.
- Methods:
- `Sample()`: Sends the value to Unity Profiler immediately.
### `ProfilerCounter<T>` (Struct)
- Namespace: `Unity.Profiling`
- Summary: Reports a value of an integer or floating point type to the Unity Profiler.
- Page: https://docs.unity3d.com/Packages/com.unity.profiling.core@1.0/api/Unity.Profiling.ProfilerCounter-1.html
- Constructors:
- `ProfilerCounter(ProfilerCategory, string, ProfilerMarkerDataUnit)`: Constructs a ProfilerCounter that is reported to the Unity Profiler whenever you call Sample().
- Methods:
- `Sample(T)`: Sends the value to Unity Profiler immediately.

## Related Packages
- [[com.unity.multiplayer.tools]]: shared signals #unity/profiling #unity/tooling #unity/dependency-graph
- [[com.unity.performance.profile-analyzer]]: shared signals #unity/profiling #unity/tooling
- [[com.unity.test-framework.performance]]: shared signals #unity/profiling #unity/tooling
- [[com.unity.addressables]]: shared signals #unity/dependency-graph
- [[com.unity.bindings.openimageio]]: shared signals #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.profiling.core@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.profiling.core@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
