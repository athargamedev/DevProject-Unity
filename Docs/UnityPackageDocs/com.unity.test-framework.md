# Unity Test Framework scripting reference

Package: `com.unity.test-framework`
Manifest version: `1.6.0`
Lock version: `1.6.0`
Docs lookup version: `1.6.0`
Docs stream: `1.6`
Resolved docs version: `1.6.0`
Source: `builtin`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-test-framework #unity/editor/6000-4 #unity/testing #unity/tooling

## Summary
The scripting API documentation is available on this website. The user guide for Unity Test Framework in Unity 6.2+ is in the Unity Manual .

## Package Graph
### Depends On
- [[com.unity.ext.nunit]] `2.0.3`
- [[com.unity.modules.imgui]] `1.0.0`
- [[com.unity.modules.jsonserialize]] `1.0.0`

### Required By
- [[com.coplaydev.unity-mcp]]
- [[com.unity.addressables]]
- [[com.unity.animation.rigging]]
- [[com.unity.collections]]
- [[com.unity.ide.visualstudio]]
- [[com.unity.physics]]
- [[com.unity.scriptablebuildpipeline]]
- [[com.unity.test-framework.performance]]
- [[com.unity.testtools.codecoverage]]

## Manual Map
- The content has moved to the Unity Manual

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.TestTools`: 30 documented types
- `UnityEditor.TestTools.TestRunner.Api`: 11 documented types
- `UnityEngine.TestTools.Utils`: 10 documented types
- `UnityEditor.TestRunner.UnityTestProtocol`: 3 documented types
- `UnityEditor.TestTools`: 3 documented types
- `UnityEngine.TestTools.Constraints`: 3 documented types
- `UnityEngine.TestRunner`: 2 documented types
- `UnityEditor.TestTools.TestRunner`: 1 documented types

## API Type Index
- `ITestRunDataHolder` in `UnityEditor.TestRunner.UnityTestProtocol`
- `TestRunData` in `UnityEditor.TestRunner.UnityTestProtocol`
- `TestRunDataHolder` in `UnityEditor.TestRunner.UnityTestProtocol`
- `ITestPlayerBuildModifier` in `UnityEditor.TestTools`
- `RequirePlatformSupportAttribute` in `UnityEditor.TestTools`
- `TestPlayerBuildModifierAttribute` in `UnityEditor.TestTools`
- `TestRunnerWindow` in `UnityEditor.TestTools.TestRunner`
- `ExecutionSettings` in `UnityEditor.TestTools.TestRunner.Api`
- `Filter` in `UnityEditor.TestTools.TestRunner.Api`
- `ICallbacks` in `UnityEditor.TestTools.TestRunner.Api`
- `IErrorCallbacks` in `UnityEditor.TestTools.TestRunner.Api`
- `ITestAdaptor` in `UnityEditor.TestTools.TestRunner.Api`
- `ITestResultAdaptor` in `UnityEditor.TestTools.TestRunner.Api`
- `ITestRunSettings` in `UnityEditor.TestTools.TestRunner.Api`
- `RunState` in `UnityEditor.TestTools.TestRunner.Api`
- `TestMode` in `UnityEditor.TestTools.TestRunner.Api`

## API Type Details
### `TestRunData` (Class)
- Namespace: `UnityEditor.TestRunner.UnityTestProtocol`
- Summary: Represents the data for a test run.
- Page: https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/UnityEditor.TestRunner.UnityTestProtocol.TestRunData.html
- Fields:
- `OneTimeSetUpDuration`: The duration of the one-time setup.
- `OneTimeTearDownDuration`: The duration of the one-time teardown.
- `SuiteName`: The name of the test suite.
- `TestsInFixture`: The names of the tests in the fixture.
### `TestRunDataHolder` (Class)
- Namespace: `UnityEditor.TestRunner.UnityTestProtocol`
- Summary: No longer in use.
- Page: https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/UnityEditor.TestRunner.UnityTestProtocol.TestRunDataHolder.html
- Properties:
- `TestRunDataList`: Gets the list of test run data.
- Methods:
- `OnAfterDeserialize()`
- `OnBeforeSerialize()`
### `ITestRunDataHolder` (Interface)
- Namespace: `UnityEditor.TestRunner.UnityTestProtocol`
- Summary: No longer in use.
- Page: https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/UnityEditor.TestRunner.UnityTestProtocol.ITestRunDataHolder.html
- Properties:
- `TestRunDataList`: Gets the list of test run data.

## Related Packages
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.test-framework.performance]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.testtools.codecoverage]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.ide.visualstudio]]: shared signals #unity/tooling #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/index.html
- Package index: [[Unity Package Docs Index]]
