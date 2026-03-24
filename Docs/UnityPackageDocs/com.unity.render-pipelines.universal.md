# Introduction to the Universal Render Pipeline

Package: `com.unity.render-pipelines.universal`
Manifest version: `17.3.1`
Lock version: `file:com.unity.render-pipelines.universal`
Docs lookup version: `17.3.1`
Docs stream: `17.3`
Resolved docs version: `17.3.1`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-render-pipelines-universal #unity/editor/6000-4 #unity/urp #unity/rendering #unity/shadergraph #unity/shaders

## Summary
The Universal Render Pipeline A series of operations that take the contents of a Scene, and displays them on a screen. Unity lets you choose from pre-built render pipelines, or write your own. More info See in Glossary (URP) is a prebuilt Scriptable Render Pipeline, made by Unity. URP provides artist-friendly workflows that let you quickly and easily…

## Package Graph
### Depends On
- [[com.unity.render-pipelines.core]] `17.4.0`
- [[com.unity.render-pipelines.universal-config]] `17.4.0`
- [[com.unity.shadergraph]] `17.4.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- The content is moved to the Unity Manual

## API Overview
This is the documentation for the scripting APIs of the Universal Render Pipeline (URP) package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.Rendering.Universal`: 193 documented types
- `UnityEditor.Rendering.Universal`: 26 documented types
- `UnityEditor.Rendering.Universal.ShaderGUI`: 16 documented types
- `UnityEngine.Rendering.Universal.Internal`: 11 documented types
- `UnityEditor`: 9 documented types
- `UnityEditor.Rendering.Universal.ShaderGraph`: 2 documented types
- `UnityEngine.Rendering`: 2 documented types
- `Unity.Rendering.Universal`: 1 documented types
- `UnityEditor.Rendering.Universal.Internal`: 1 documented types

## API Type Index
- `ShaderUtils` in `Unity.Rendering.Universal`
- `BaseShaderGUI` in `UnityEditor`
- `BaseShaderGUI.BlendMode` in `UnityEditor`
- `BaseShaderGUI.Expandable` in `UnityEditor`
- `BaseShaderGUI.QueueControl` in `UnityEditor`
- `BaseShaderGUI.RenderFace` in `UnityEditor`
- `BaseShaderGUI.SmoothnessSource` in `UnityEditor`
- `BaseShaderGUI.Styles` in `UnityEditor`
- `BaseShaderGUI.SurfaceType` in `UnityEditor`
- `LightExplorer` in `UnityEditor`
- `AutodeskInteractiveUpgrader` in `UnityEditor.Rendering.Universal`
- `ConverterContainerId` in `UnityEditor.Rendering.Universal`
- `ConverterFilter` in `UnityEditor.Rendering.Universal`
- `ConverterId` in `UnityEditor.Rendering.Universal`
- `Converters` in `UnityEditor.Rendering.Universal`
- `DecalMeshDepthBiasType` in `UnityEditor.Rendering.Universal`

## API Type Details
### `ShaderUtils` (Class)
- Namespace: `Unity.Rendering.Universal`
- Summary: Various utility functions for shaders in URP.
- Page: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/api/Unity.Rendering.Universal.ShaderUtils.html
### `BaseShaderGUI` (Class)
- Namespace: `UnityEditor`
- Summary: The base class for shader GUI in URP.
- Page: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/api/UnityEditor.BaseShaderGUI.html
- Fields:
- `m_FirstTimeApply`: Used to sure that needed setup (ie keywords/render queue) are set up when switching some existing material to a universal shader.
- Properties:
- `addPrecomputedVelocityProp`: The MaterialProperty for pre-computed motion vectors (for Alembic).
- `alphaClipProp`: The MaterialProperty for alpha clip.
- `alphaCutoffProp`: The MaterialProperty for alpha cutoff.
- `baseColorProp`: The MaterialProperty for base color.
- `baseMapProp`: The MaterialProperty for base map.
- Methods:
- `AssignNewShaderToMaterial(Material, Shader, Shader)`: Assigns a new shader to the material.
- `DoPopup(GUIContent, MaterialProperty, string[])`: Helper function to draw a popup.
- `DrawAdvancedOptions(Material)`: Draws the advanced options GUI.
- `DrawBaseProperties(Material)`: Draws the base properties GUI.
- `DrawEmissionProperties(Material, bool)`: Draws the emission properties.
### `BaseShaderGUI.BlendMode` (Enum)
- Namespace: `UnityEditor`
- Summary: The blend mode for your material.
- Page: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/api/UnityEditor.BaseShaderGUI.BlendMode.html

## Related Packages
- [[com.unity.shadergraph]]: shared signals #unity/rendering #unity/shadergraph #unity/shaders #unity/dependency-graph
- [[com.unity.mathematics]]: shared signals #unity/rendering #unity/shaders
- [[com.unity.render-pipelines.core]]: shared signals #unity/rendering #unity/dependency-graph
- [[com.unity.terrain-tools]]: shared signals #unity/rendering #unity/shaders

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/api/index.html
- Package index: [[Unity Package Docs Index]]
