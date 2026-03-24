# SRP Core

Package: `com.unity.render-pipelines.core`
Manifest version: `17.3.1`
Lock version: `17.4.0`
Docs lookup version: `17.4.0`
Docs stream: `17.4`
Resolved docs version: `17.4.0`
Source: `builtin`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-render-pipelines-core #unity/editor/6000-4 #unity/rendering #unity/camera #unity/testing #unity/tooling

## Summary
The Scriptable Render Pipeline (SRP) is a Unity feature that allows you to write C# scripts to control the way Unity renders each frame. SRP Core is a package that makes it easier to create or customize an SRP.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.14`
- [[com.unity.collections]] `2.4.3`
- [[com.unity.mathematics]] `1.3.2`
- [[com.unity.modules.jsonserialize]] `1.0.0`
- [[com.unity.modules.terrain]] `1.0.0`
- [[com.unity.ugui]] `2.0.0`

### Required By
- [[com.unity.render-pipelines.universal]]
- [[com.unity.render-pipelines.universal-config]]
- [[com.unity.shadergraph]]

## Manual Map
- SRP Core
- What's new
- 12
- 13
- 17
- Creating a custom render pipeline
- Create a custom Scriptable Render Pipeline
- Create a Render Pipeline Asset and Render Pipeline Instance in a custom render pipeline
- Create a simple render loop in a custom render pipeline
- Execute rendering commands in a custom render pipeline
- Scriptable Render Pipeline callbacks reference
- Creating a custom render pipeline using the render graph system
- Render graph system
- Write a render pipeline with render graph
- Write a render pass using the render graph system
- Resources in the render graph system
- Introduction to resources in the render graph system
- Blit using the render graph system

## API Overview
This is the documentation for the scripting APIs of the Scriptable Render Pipeline (SRP) Core package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.Rendering`: 358 documented types
- `UnityEditor.Rendering`: 199 documented types
- `UnityEngine.Rendering.RenderGraphModule`: 35 documented types
- `UnityEngine.Rendering.UI`: 35 documented types
- `UnityEditor.Rendering.LookDev`: 19 documented types
- `UnityEngine.Rendering.UnifiedRayTracing`: 12 documented types
- `UnityEngine`: 7 documented types
- `UnityEngine.Experimental.Rendering`: 6 documented types
- `UnityEngine.Rendering.RenderGraphModule.Util`: 4 documented types
- `UnityEditor`: 3 documented types
- `UnityEngine.Rendering.LookDev`: 3 documented types
- `UnityEditor.Rendering.Utilities`: 1 documented types
- `UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler`: 1 documented types
- `UnityEngine.Rendering.Tests`: 1 documented types
- `UnityEngine.Rendering.Universal`: 1 documented types

## API Type Index
- `LightAnchorEditor` in `UnityEditor`
- `LightAnchorEditorTool` in `UnityEditor`
- `LightAnchorHandles` in `UnityEditor`
- `AdditionalPropertiesStateBase` in `UnityEditor.Rendering`
- `AdditionalPropertiesStateList` in `UnityEditor.Rendering`
- `AdditionalPropertiesState` in `UnityEditor.Rendering`
- `AdvancedProperties` in `UnityEditor.Rendering`
- `AnalyticsUtils` in `UnityEditor.Rendering`
- `AssetDatabaseHelper` in `UnityEditor.Rendering`
- `AssetReimportUtils` in `UnityEditor.Rendering`
- `BuildTargetExtensions` in `UnityEditor.Rendering`
- `CameraEditorUtils` in `UnityEditor.Rendering`
- `CameraEditorUtils.GetPreviewCamera` in `UnityEditor.Rendering`
- `CameraUI` in `UnityEditor.Rendering`
- `CameraUI.Environment` in `UnityEditor.Rendering`
- `CameraUI.Environment.Styles` in `UnityEditor.Rendering`

## API Type Details
### `LightAnchorEditor` (Class)
- Namespace: `UnityEditor`
- Summary: LightAnchorEditor represent the inspector for the LightAnchor
- Page: https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.4/api/UnityEditor.LightAnchorEditor.html
- Methods:
- `OnInspectorGUI()`: Calls the methods in its invocation list when show the Inspector
### `LightAnchorEditorTool` (Class)
- Namespace: `UnityEditor`
- Summary: LightAnchorEditorTool
- Page: https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.4/api/UnityEditor.LightAnchorEditorTool.html
- Properties:
- `toolbarIcon`: Icon for LightAnchor Tool
- Methods:
- `IsAvailable()`: Checks whether the custom editor tool is available based on the state of the editor.
- `OnToolGUI(EditorWindow)`: Use this method to implement a custom editor tool.
### `LightAnchorHandles` (Class)
- Namespace: `UnityEditor`
- Summary: LightAnchorHandles describes the Handles for the LightAnchorEditorTool
- Page: https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.4/api/UnityEditor.LightAnchorHandles.html
- Constructors:
- `LightAnchorHandles(LightAnchor)`: Initializes and returns an instance of LightAnchorHandles
- Properties:
- `anchorPosition`: The anchor position
- `lightPosition`: The light position
- Methods:
- `OnGUI()`: On GUI

## Related Packages
- [[com.unity.shadergraph]]: shared signals #unity/rendering #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling
- [[com.unity.mathematics]]: shared signals #unity/rendering #unity/dependency-graph
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.render-pipelines.universal]]: shared signals #unity/rendering #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.4/api/index.html
- Package index: [[Unity Package Docs Index]]
