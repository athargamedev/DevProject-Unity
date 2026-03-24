# Code Editor Package for Visual Studio

Package: `com.unity.ide.visualstudio`
Manifest version: `2.0.27`
Lock version: `2.0.27`
Docs lookup version: `2.0.27`
Docs stream: `2.0`
Resolved docs version: `2.0.27`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-ide-visualstudio #unity/editor/6000-4 #unity/tooling

## Summary
The Visual Studio Editor package provides the Unity Editor with support for Unity-specific features from the Visual Studio Tools for Unity extension in Visual Studio and the Unity for Visual Studio Code extension in Visual Studio Code . These include IntelliSense auto-complete suggestions, C# editing, and debugging.

## Package Graph
### Depends On
- [[com.unity.test-framework]] `1.1.33`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- About Visual Studio Editor
- Using the Visual Studio Editor package

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Microsoft.Unity.VisualStudio.Editor`: 11 documented types

## API Type Index
- `AssemblyNameProvider` in `Microsoft.Unity.VisualStudio.Editor`
- `IAssemblyNameProvider` in `Microsoft.Unity.VisualStudio.Editor`
- `IFileIO` in `Microsoft.Unity.VisualStudio.Editor`
- `IGUIDGenerator` in `Microsoft.Unity.VisualStudio.Editor`
- `IGenerator` in `Microsoft.Unity.VisualStudio.Editor`
- `Image` in `Microsoft.Unity.VisualStudio.Editor`
- `ProjectGeneration` in `Microsoft.Unity.VisualStudio.Editor`
- `ProjectGenerationFlag` in `Microsoft.Unity.VisualStudio.Editor`
- `ScriptingLanguage` in `Microsoft.Unity.VisualStudio.Editor`
- `SolutionGuidGenerator` in `Microsoft.Unity.VisualStudio.Editor`
- `VisualStudioEditor` in `Microsoft.Unity.VisualStudio.Editor`

## API Type Details
### `AssemblyNameProvider` (Class)
- Namespace: `Microsoft.Unity.VisualStudio.Editor`
- Page: https://docs.unity3d.com/Packages/com.unity.ide.visualstudio@2.0/api/Microsoft.Unity.VisualStudio.Editor.AssemblyNameProvider.html
- Properties:
- `ProjectGenerationFlag`
- `ProjectGenerationRootNamespace`
- `ProjectSupportedExtensions`
- Methods:
- `FindForAssetPath(string)`
- `GetAllAssetPaths()`
- `GetAssemblies(Func<string, bool>)`
- `GetAssemblyName(string, string)`
- `GetAssemblyNameFromScriptPath(string)`
### `IAssemblyNameProvider` (Interface)
- Namespace: `Microsoft.Unity.VisualStudio.Editor`
- Page: https://docs.unity3d.com/Packages/com.unity.ide.visualstudio@2.0/api/Microsoft.Unity.VisualStudio.Editor.IAssemblyNameProvider.html
- Properties:
- `ProjectGenerationFlag`
- `ProjectGenerationRootNamespace`
- `ProjectSupportedExtensions`
- Methods:
- `FindForAssetPath(string)`
- `GetAllAssetPaths()`
- `GetAssemblies(Func<string, bool>)`
- `GetAssemblyName(string, string)`
- `GetAssemblyNameFromScriptPath(string)`
### `IFileIO` (Interface)
- Namespace: `Microsoft.Unity.VisualStudio.Editor`
- Page: https://docs.unity3d.com/Packages/com.unity.ide.visualstudio@2.0/api/Microsoft.Unity.VisualStudio.Editor.IFileIO.html
- Methods:
- `Exists(string)`
- `ReadAllText(string)`
- `WriteAllText(string, string)`

## Related Packages
- [[com.unity.test-framework]]: shared signals #unity/tooling #unity/dependency-graph
- [[com.unity.bindings.openimageio]]: shared signals #unity/tooling
- [[com.unity.ext.nunit]]: shared signals #unity/tooling
- [[com.unity.multiplayer.center]]: shared signals #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.ide.visualstudio@2.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.ide.visualstudio@2.0/api/index.html
- Package index: [[Unity Package Docs Index]]
