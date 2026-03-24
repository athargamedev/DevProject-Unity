# Shader Graph

Package: `com.unity.shadergraph`
Manifest version: `17.3.1`
Lock version: `17.4.0`
Docs lookup version: `17.4.0`
Docs stream: `17.4`
Resolved docs version: `17.4.0`
Source: `builtin`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-shadergraph #unity/editor/6000-4 #unity/shadergraph #unity/rendering #unity/shaders #unity/testing #unity/tooling

## Summary
Shader Graph enables you to build shaders visually. Instead of writing code, you create and connect nodes in a graph framework. Shader Graph gives instant feedback that reflects your changes, and it’s simple enough for users who are new to shader creation.

## Package Graph
### Depends On
- [[com.unity.render-pipelines.core]] `17.4.0`
- [[com.unity.searcher]] `4.9.3`

### Required By
- [[com.unity.render-pipelines.universal]]

## Manual Map
- About Shader Graph
- What's new in Shader Graph
- Install and upgrade
- Install Shader Graph
- Upgrade to Shader Graph 10.0.x
- Get started with Shader Graph
- Create a shader graph asset
- Add and connect nodes
- Create a shader graph and use it with a material
- Shader Graph UI reference
- Shader Graph template browser
- Shader Graph Window
- Master Stack
- Main Preview
- Blackboard
- Graph Inspector
- Graph Settings Tab
- Shader Graph Preferences

## API Overview
This is the documentation for the scripting APIs of the Shader Graph package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.ShaderGraph.Internal`: 40 documented types
- `UnityEditor.ShaderGraph`: 18 documented types
- `UnityEditor.Rendering.BuiltIn.ShaderGraph`: 10 documented types
- `UnityEditor.ShaderGraph.Drawing`: 8 documented types
- `UnityEditor.ShaderGraph.Drawing.Inspector.PropertyDrawers`: 7 documented types
- `UnityEditor.Rendering.Canvas.ShaderGraph`: 4 documented types
- `UnityEditor.Rendering.Fullscreen.ShaderGraph`: 3 documented types
- `UnityEditor.ShaderGraph.Serialization`: 3 documented types
- `UnityEditor.Rendering.BuiltIn`: 2 documented types
- `UnityEditor.ShaderGraph.Drawing.Inspector`: 2 documented types
- `UnityEditor.Graphing.IntegrationTests`: 1 documented types
- `UnityEditor.Graphing.UnitTests`: 1 documented types
- `UnityEditor.Rendering.CustomRenderTexture.ShaderGraph`: 1 documented types
- `UnityEditor.Rendering.UITK.ShaderGraph`: 1 documented types
- `UnityEditor.ShaderGraph.Configuration`: 1 documented types
- `UnityEditor.ShaderGraph.Drawing.Colors`: 1 documented types
- `UnityEditor.ShaderGraph.Legacy`: 1 documented types

## API Type Index
- `SerializationTests` in `UnityEditor.Graphing.IntegrationTests`
- `BaseMaterialGraphTests` in `UnityEditor.Graphing.UnitTests`
- `ShaderKeywordStrings` in `UnityEditor.Rendering.BuiltIn`
- `ShaderUtils` in `UnityEditor.Rendering.BuiltIn`
- `BuiltInBaseShaderGUI` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInBaseShaderGUI.BlendMode` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInBaseShaderGUI.Expandable` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInBaseShaderGUI.QueueControl` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInBaseShaderGUI.RenderFace` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInBaseShaderGUI.Styles` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInBaseShaderGUI.SurfaceType` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInLitGUI` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `BuiltInUnlitGUI` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `RenderFace` in `UnityEditor.Rendering.BuiltIn.ShaderGraph`
- `CanvasMetaData` in `UnityEditor.Rendering.Canvas.ShaderGraph`
- `CanvasShaderGUI` in `UnityEditor.Rendering.Canvas.ShaderGraph`

## API Type Details
### `SerializationTests` (Class)
- Namespace: `UnityEditor.Graphing.IntegrationTests`
- Page: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/api/UnityEditor.Graphing.IntegrationTests.SerializationTests.html
- Methods:
- `TestPolymorphicSerializationPreservesTypesViaBaseClass()`
- `TestPolymorphicSerializationPreservesTypesViaInterface()`
- `TestSerializableSlotCanSerialize()`
- `TestSerializationHelperCanSerializeThenDeserialize()`
- `TestSerializationHelperElementCanSerialize()`
### `BaseMaterialGraphTests` (Class)
- Namespace: `UnityEditor.Graphing.UnitTests`
- Page: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/api/UnityEditor.Graphing.UnitTests.BaseMaterialGraphTests.html
- Methods:
- `RunBeforeAnyTests()`
- `TestCanAddNodeToBaseMaterialGraph()`
- `TestCanAddSlotToTestNode()`
- `TestCanConnectAndTraverseThreeNodesOnBaseMaterialGraph()`
- `TestCanConnectAndTraverseTwoNodesOnBaseMaterialGraph()`
### `ShaderKeywordStrings` (Class)
- Namespace: `UnityEditor.Rendering.BuiltIn`
- Page: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/api/UnityEditor.Rendering.BuiltIn.ShaderKeywordStrings.html
- Fields:
- `AdditionalLightShadows`
- `AdditionalLightsPixel`
- `AdditionalLightsVertex`
- `CastingPunctualLightShadow`
- `DIRLIGHTMAP_COMBINED`

## Related Packages
- [[com.unity.render-pipelines.core]]: shared signals #unity/rendering #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.render-pipelines.universal]]: shared signals #unity/rendering #unity/shadergraph #unity/shaders #unity/dependency-graph
- [[com.unity.searcher]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.terrain-tools]]: shared signals #unity/rendering #unity/shaders #unity/tooling
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/api/index.html
- Package index: [[Unity Package Docs Index]]
