# Performance Testing Package for Unity Test Framework

Package: `com.unity.test-framework.performance`
Manifest version: `Not listed`
Lock version: `3.2.0`
Docs lookup version: `3.2.0`
Docs stream: `3.2`
Resolved docs version: `3.2.1`
Source: `registry`
Depth: `2`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-test-framework-performance #unity/editor/6000-4 #unity/testing #unity/tooling #unity/profiling

## Summary
The Unity Performance Testing Package extends Unity Test Framework with performance testing capabilities. It provides an API and test case decorators for taking measurements/samples of Unity profiler markers, and other custom metrics, in the Unity Editor and built players. It also collects configuration metadata that is useful for comparing data across…

## Package Graph
### Depends On
- [[com.unity.modules.jsonserialize]] `1.0.0`
- [[com.unity.test-framework]] `1.1.33`

### Required By
- [[com.unity.collections]]
- [[com.unity.entities]]

## Manual Map
- Performance Testing Package overview
- Taking measurements
- Measure.Method
- Measure.Frames
- Measure.Scope
- Measure.ProfileMarkers
- Measure.Custom
- Writing tests
- Viewing results
- Classes
- Test attributes
- Command-line arguments

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.PerformanceTesting`: 7 documented types
- `Unity.PerformanceTesting.Data`: 7 documented types
- `Unity.PerformanceTesting.Measurements`: 5 documented types
- `Unity.PerformanceTesting.Statistics`: 2 documented types
- `Unity.PerformanceTesting.Editor`: 1 documented types
- `Unity.PerformanceTesting.Exceptions`: 1 documented types

## API Type Index
- `Measure` in `Unity.PerformanceTesting`
- `Metadata` in `Unity.PerformanceTesting`
- `PerformanceAttribute` in `Unity.PerformanceTesting`
- `PerformanceTest` in `Unity.PerformanceTesting`
- `SampleGroup` in `Unity.PerformanceTesting`
- `SampleUnit` in `Unity.PerformanceTesting`
- `VersionAttribute` in `Unity.PerformanceTesting`
- `Editor` in `Unity.PerformanceTesting.Data`
- `Hardware` in `Unity.PerformanceTesting.Data`
- `PerformanceTestResult` in `Unity.PerformanceTesting.Data`
- `Player` in `Unity.PerformanceTesting.Data`
- `Project` in `Unity.PerformanceTesting.Data`
- `Run` in `Unity.PerformanceTesting.Data`
- `SampleGroup` in `Unity.PerformanceTesting.Data`
- `TestResultXmlParser` in `Unity.PerformanceTesting.Editor`
- `PerformanceTestException` in `Unity.PerformanceTesting.Exceptions`

## API Type Details
### `Measure` (Class)
- Namespace: `Unity.PerformanceTesting`
- Summary: Enables measuring of performance metrics during a performance test.
- Page: https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/api/Unity.PerformanceTesting.Measure.html
- Methods:
- `Custom(string, double)`: Saves provided value as a performance measurement.
- `Custom(SampleGroup, double)`: Saves provided value as a performance measurement.
- `Frames()`: Measures frame times with given parameters.
- `Method(Action)`: Measures execution time for a method with given parameters.
- `ProfilerMarkers(params string[])`: Measures profiler markers for the given scope.
### `Metadata` (Class)
- Namespace: `Unity.PerformanceTesting`
- Summary: Helper class to retrieve metadata information about player settings and hardware.
- Page: https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/api/Unity.PerformanceTesting.Metadata.html
- Methods:
- `GetFromResources()`: Loads run from resources.
- `GetHardware()`: Gets hardware information.
- `SetPlayerSettings(Run)`: Sets player settings.
- `SetRuntimeSettings(Run)`: Sets runtime player settings on a run.
### `PerformanceAttribute` (Class)
- Namespace: `Unity.PerformanceTesting`
- Summary: Test attribute to specify a performance test. It will add category "Performance" to test properties.
- Page: https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/api/Unity.PerformanceTesting.PerformanceAttribute.html
- Constructors:
- `PerformanceAttribute()`: Adds performance attribute to a test method.
- Methods:
- `AfterTest(ITest)`: Executed after a test execution.
- `BeforeTest(ITest)`: Executed before a test execution.

## Related Packages
- [[com.unity.test-framework]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.tools]]: shared signals #unity/profiling #unity/tooling
- [[com.unity.performance.profile-analyzer]]: shared signals #unity/profiling #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/api/index.html
- Package index: [[Unity Package Docs Index]]
