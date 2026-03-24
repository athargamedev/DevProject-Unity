# Addressables package

Package: `com.unity.addressables`
Manifest version: `2.9.1`
Lock version: `2.9.1`
Docs lookup version: `2.9.1`
Docs stream: `2.9`
Resolved docs version: `2.9.1`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-addressables #unity/editor/6000-4 #unity/addressables #unity/assets #unity/content-management #unity/input #unity/gameplay

## Summary
The Addressables package provides a user interface in the Unity Editor to organize and manage the assets in your project. It also has an API that you can use to load and release assets at runtime.

## Package Graph
### Depends On
- [[com.unity.modules.assetbundle]] `1.0.0`
- [[com.unity.modules.imageconversion]] `1.0.0`
- [[com.unity.modules.jsonserialize]] `1.0.0`
- [[com.unity.modules.unitywebrequest]] `1.0.0`
- [[com.unity.modules.unitywebrequestassetbundle]] `1.0.0`
- [[com.unity.profiling.core]] `1.0.2`
- [[com.unity.scriptablebuildpipeline]] `2.6.1`
- [[com.unity.test-framework]] `1.4.5`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Addressables package
- Addressables package set up
- Install Addressables
- Convert existing projects to Addressables
- Introduction to converting existing projects to Addressables
- Assign scenes as Addressable
- Convert prefabs to use Addressables
- Move assets from the Resources system
- Convert AssetBundles to Addressables
- Addressables samples
- Addressables introduction
- Create and organize Addressable assets
- Introduction to creating and organizing Addressable assets
- Organize assets into groups
- Introduction to addressable asset groups
- Add assets to groups
- Label assets
- Define group settings

## API Overview
This section of the documentation contains details of the scripting API that Unity provides for the Addressables package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `AutoGroupGenerator`: 38 documented types
- `UnityEditor.AddressableAssets.Build.Layout`: 28 documented types
- `UnityEngine.ResourceManagement.ResourceProviders`: 27 documented types
- `UnityEditor.AddressableAssets.Build`: 26 documented types
- `UnityEditor.AddressableAssets.Settings`: 21 documented types
- `UnityEngine.AddressableAssets`: 16 documented types
- `UnityEditor.AddressableAssets.Settings.GroupSchemas`: 13 documented types
- `UnityEngine.ResourceManagement.Util`: 13 documented types
- `UnityEditor.AddressableAssets.Build.AnalyzeRules`: 11 documented types
- `UnityEditor.AddressableAssets.Build.DataBuilders`: 9 documented types
- `UnityEditor.AddressableAssets.BuildReportVisualizer`: 9 documented types
- `UnityEditor.AddressableAssets.Build.BuildPipelineTasks`: 6 documented types
- `UnityEngine.ResourceManagement.ResourceProviders.Simulation`: 6 documented types
- `UnityEngine.AddressableAssets.ResourceLocators`: 5 documented types
- `UnityEngine.ResourceManagement`: 5 documented types
- `UnityEngine.ResourceManagement.AsyncOperations`: 5 documented types
- `UnityEngine.ResourceManagement.Exceptions`: 5 documented types
- `UnityEngine.AddressableAssets.Initialization`: 4 documented types
- `UnityEngine.ResourceManagement.ResourceLocations`: 4 documented types
- `UnityEditor.AddressableAssets`: 2 documented types
- `UnityEngine`: 2 documented types
- `UnityEngine.AddressableAssets.ResourceProviders`: 2 documented types
- `UnityEngine.AddressableAssets.Utility`: 1 documented types
- `UnityEngine.ResourceManagement.Profiling`: 1 documented types

## API Type Index
- `AddressableUtil` in `AutoGroupGenerator`
- `AssetDatabaseUtil` in `AutoGroupGenerator`
- `AssetNode` in `AutoGroupGenerator`
- `AssetSelectionInputRule` in `AutoGroupGenerator`
- `AssetSelectionInputRuleEditor` in `AutoGroupGenerator`
- `AutoGroupGeneratorSettings` in `AutoGroupGenerator`
- `Command` in `AutoGroupGenerator`
- `CommandQueue` in `AutoGroupGenerator`
- `DataContainer` in `AutoGroupGenerator`
- `DefaultOutputRule` in `AutoGroupGenerator`
- `DependencyGraph` in `AutoGroupGenerator`
- `DependencyGraph.SerializableKeyValue` in `AutoGroupGenerator`
- `DependencyGraph.SerializedData` in `AutoGroupGenerator`
- `EditorPersistentValue` in `AutoGroupGenerator`
- `EditorUtil` in `AutoGroupGenerator`
- `ExclusionRule` in `AutoGroupGenerator`

## API Type Details
### `AddressableUtil` (Class)
- Namespace: `AutoGroupGenerator`
- Summary: Utility helpers for interacting with the Addressables configuration.
- Page: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/api/AutoGroupGenerator.AddressableUtil.html
### `AssetDatabaseUtil` (Class)
- Namespace: `AutoGroupGenerator`
- Summary: Helpers for querying the Unity asset database.
- Page: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/api/AutoGroupGenerator.AssetDatabaseUtil.html
### `AssetNode` (Class)
- Namespace: `AutoGroupGenerator`
- Summary: Represents an asset in the dependency graph via its GUID.
- Page: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/api/AutoGroupGenerator.AssetNode.html

## Related Packages
- [[com.unity.cinemachine]]: shared signals #unity/gameplay #unity/input
- [[com.unity.inputsystem]]: shared signals #unity/gameplay #unity/input
- [[com.unity.platformtoolkit]]: shared signals #unity/gameplay #unity/input
- [[com.unity.recorder]]: shared signals #unity/gameplay #unity/input
- [[com.unity.ugui]]: shared signals #unity/gameplay #unity/input

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/api/index.html
- Package index: [[Unity Package Docs Index]]
