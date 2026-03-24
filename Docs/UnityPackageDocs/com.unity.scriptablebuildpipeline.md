# Unity Scriptable Build Pipeline

Package: `com.unity.scriptablebuildpipeline`
Manifest version: `Not listed`
Lock version: `2.6.1`
Docs lookup version: `2.6.1`
Docs stream: `2.6`
Resolved docs version: `2.6.1`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-scriptablebuildpipeline #unity/editor/6000-4 #unity/server #unity/platform #unity/testing #unity/tooling

## Summary
The Scriptable Build Pipeline (SBP) package allows you to control how Unity builds content. The package moves the previously C++-only build pipeline code to a public C# package with a pre-defined build flow for building AssetBundles. The pre-defined AssetBundle build flow reduces build time, improves incremental build processing, and provides greater…

## Package Graph
### Depends On
- [[com.unity.modules.assetbundle]] `1.0.0`
- [[com.unity.test-framework]] `1.4.5`

### Required By
- [[com.unity.addressables]]
- [[com.unity.entities]]

## Manual Map
- Scriptable Build Pipeline
- Getting Started
- Terminology
- Usage Examples
- Upgrade Guide
- Unity Cache Server
- Build Log

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.Build.Pipeline.Interfaces`: 32 documented types
- `UnityEditor.Build.Pipeline.Tasks`: 28 documented types
- `UnityEditor.Build.Pipeline`: 25 documented types
- `UnityEditor.Build.Pipeline.Utilities`: 19 documented types
- `UnityEditor.Build.Pipeline.Tests`: 15 documented types
- `UnityEditor.Build.Pipeline.Tests.ContentLoad`: 7 documented types
- `AppendHashToAssetBundleNamePerPlatformTests`: 5 documented types
- `UnityEditor.Build.Pipeline.WriteTypes`: 5 documented types
- `UnityEditor.Build.Pipeline.Injector`: 2 documented types
- `UnityEditor.Build.Utilities`: 2 documented types
- `UnityEngine.Build.Pipeline`: 2 documented types
- `Unity.ScriptableBuildPipelineTests.Runtime.Tests`: 1 documented types
- `UnityEditor.Build.Pipeline.Editor.OptionalPackages.Tests`: 1 documented types

## API Type Index
- `AppendHashToAssetBundleNameTestsLinux` in `AppendHashToAssetBundleNamePerPlatformTests`
- `AppendHashToAssetBundleNameTestsTestsOSX` in `AppendHashToAssetBundleNamePerPlatformTests`
- `AppendHashToAssetBundleNameTestsWindows` in `AppendHashToAssetBundleNamePerPlatformTests`
- `AppendHashToAssetBundleNameTests` in `AppendHashToAssetBundleNamePerPlatformTests`
- `ArchiveAndCompressTestFixture` in `AppendHashToAssetBundleNamePerPlatformTests`
- `MonoBehaviourWithReference` in `Unity.ScriptableBuildPipelineTests.Runtime.Tests`
- `BuildCallbacks` in `UnityEditor.Build.Pipeline`
- `BuildContent` in `UnityEditor.Build.Pipeline`
- `BuildContext` in `UnityEditor.Build.Pipeline`
- `BuildDependencyData` in `UnityEditor.Build.Pipeline`
- `BuildExtendedAssetData` in `UnityEditor.Build.Pipeline`
- `BuildParameters` in `UnityEditor.Build.Pipeline`
- `BuildResults` in `UnityEditor.Build.Pipeline`
- `BuildSpriteData` in `UnityEditor.Build.Pipeline`
- `BuildTasksRunner` in `UnityEditor.Build.Pipeline`
- `BuildWriteData` in `UnityEditor.Build.Pipeline`

## API Type Details
### `AppendHashToAssetBundleNameTestsLinux` (Class)
- Namespace: `AppendHashToAssetBundleNamePerPlatformTests`
- Summary: Linux specific tests
- Page: https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@2.6/api/AppendHashToAssetBundleNamePerPlatformTests.AppendHashToAssetBundleNameTestsLinux.html
### `AppendHashToAssetBundleNameTestsTestsOSX` (Class)
- Namespace: `AppendHashToAssetBundleNamePerPlatformTests`
- Summary: OSX Specific tests
- Page: https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@2.6/api/AppendHashToAssetBundleNamePerPlatformTests.AppendHashToAssetBundleNameTestsTestsOSX.html
### `AppendHashToAssetBundleNameTestsWindows` (Class)
- Namespace: `AppendHashToAssetBundleNamePerPlatformTests`
- Summary: Windows specific tests
- Page: https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@2.6/api/AppendHashToAssetBundleNamePerPlatformTests.AppendHashToAssetBundleNameTestsWindows.html

## Related Packages
- [[com.unity.multiplayer.tools]]: shared signals #unity/platform #unity/server #unity/tooling
- [[com.unity.test-framework]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.web.stripping-tool]]: shared signals #unity/platform #unity/testing #unity/tooling
- [[com.unity.dedicated-server]]: shared signals #unity/platform #unity/server
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@2.6/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@2.6/api/index.html
- Package index: [[Unity Package Docs Index]]
