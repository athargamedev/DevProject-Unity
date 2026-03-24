# About Multiplayer Tools

Package: `com.unity.multiplayer.tools`
Manifest version: `2.2.8`
Lock version: `file:com.unity.multiplayer.tools`
Docs lookup version: `2.2.8`
Docs stream: `2.2`
Resolved docs version: `2.2.8`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-multiplayer-tools #unity/editor/6000-4 #unity/multiplayer #unity/profiling #unity/tooling #unity/networking #unity/server #unity/platform

## Summary
The Multiplayer Tools package provides a suite of tools used in multiplayer game development. These tools help you to visualize, debug, and optimize your multiplayer games.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.18`
- [[com.unity.collections]] `2.5.1`
- [[com.unity.mathematics]] `1.3.2`
- [[com.unity.modules.uielements]] `1.0.0`
- [[com.unity.nuget.mono-cecil]] `1.11.4`
- [[com.unity.nuget.newtonsoft-json]] `3.2.1`
- [[com.unity.profiling.core]] `1.0.2`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Multiplayer Tools package
- Install
- Multiplayer Tools window
- Profiler
- Runtime Network Stats Monitor
- Network Simulator
- Network Scene Visualization
- Hierarchy Network Debug View
- Porting from client-hosted to Dedicated Game Server (DGS)
- Client-hosted vs DGS-hosted
- Game changes
- Optimizing server builds
- Hosting considerations

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Multiplayer.Tools.NetStatsMonitor`: 14 documented types
- `Unity.Multiplayer.Tools.NetStats`: 7 documented types
- `Unity.Multiplayer.Tools.Common`: 2 documented types
- `Unity.Multiplayer.Tools.Editor.MultiplayerToolsWindow`: 1 documented types
- `Unity.Multiplayer.Tools.MetricTypes`: 1 documented types
- `Unity.Multiplayer.Tools.NetStatsMonitor.Implementation`: 1 documented types
- `Unity.Multiplayer.Tools.NetVis.Configuration`: 1 documented types
- `Unity.Multiplayer.Tools.NetworkProfiler.Editor.NoDataView`: 1 documented types

## API Type Index
- `IRuntimeUpdater` in `Unity.Multiplayer.Tools.Common`
- `ProfilerScope` in `Unity.Multiplayer.Tools.Common`
- `MultiplayerToolsWindow` in `Unity.Multiplayer.Tools.Editor.MultiplayerToolsWindow`
- `DirectedMetricType` in `Unity.Multiplayer.Tools.MetricTypes`
- `AssemblyRequiresTypeRegistrationAttribute` in `Unity.Multiplayer.Tools.NetStats`
- `MetricId` in `Unity.Multiplayer.Tools.NetStats`
- `MetricIdTypeLibrary` in `Unity.Multiplayer.Tools.NetStats`
- `MetricKind` in `Unity.Multiplayer.Tools.NetStats`
- `MetricMetadataAttribute` in `Unity.Multiplayer.Tools.NetStats`
- `MetricTypeEnumAttribute` in `Unity.Multiplayer.Tools.NetStats`
- `Units` in `Unity.Multiplayer.Tools.NetStats`
- `AggregationMethod` in `Unity.Multiplayer.Tools.NetStatsMonitor`
- `CounterConfiguration` in `Unity.Multiplayer.Tools.NetStatsMonitor`
- `DisplayElementConfiguration` in `Unity.Multiplayer.Tools.NetStatsMonitor`
- `DisplayElementType` in `Unity.Multiplayer.Tools.NetStatsMonitor`
- `ExponentialMovingAverageParams` in `Unity.Multiplayer.Tools.NetStatsMonitor`

## API Type Details
### `ProfilerScope` (Struct)
- Namespace: `Unity.Multiplayer.Tools.Common`
- Summary: A Helper class for Profiler scoping
- Page: https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/api/Unity.Multiplayer.Tools.Common.ProfilerScope.html
- Methods:
- `BeginSample(string)`: Static method returning a new ProfilerScope object and starting the sampling
- `Dispose()`: Stopping the Profiler sampling on Disposal of the object (end of scope)
### `DirectedMetricType` (Enum)
- Namespace: `Unity.Multiplayer.Tools.MetricTypes`
- Summary: The built in set of metrics that can be displayed in the multiplayer tools, such as the Network Profiler and the Runtime Net Stats Monitor.
- Page: https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/api/Unity.Multiplayer.Tools.MetricTypes.DirectedMetricType.html
### `AssemblyRequiresTypeRegistrationAttribute` (Class)
- Namespace: `Unity.Multiplayer.Tools.NetStats`
- Summary: For internal use. This attribute is automatically added to assemblies that use types from the multiplayer tools package that require code generation to work correctly
- Page: https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/api/Unity.Multiplayer.Tools.NetStats.AssemblyRequiresTypeRegistrationAttribute.html

## Related Packages
- [[com.unity.dedicated-server]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.netcode.gameobjects]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.transport]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.multiplayer.center]]: shared signals #unity/multiplayer #unity/networking #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/multiplayer #unity/networking #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.multiplayer.tools@2.2/api/index.html
- Package index: [[Unity Package Docs Index]]
