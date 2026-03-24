# Terrain Tools

Package: `com.unity.terrain-tools`
Manifest version: `5.3.2`
Lock version: `5.3.2`
Docs lookup version: `5.3.2`
Docs stream: `5.3`
Resolved docs version: `5.3.2`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-terrain-tools #unity/editor/6000-4 #unity/terrain #unity/tooling #unity/shaders #unity/rendering

## Summary
The Terrain Tools package improves the workflow for creating Terrain ecosystems in Unity. You can download this package through the Package Manager in 2019.1 and newer versions of Unity. However, be aware that older versions of the package no longer receive bug fixes and feature maintenance. To work with an actively supported version of Terrain Tools, use…

## Package Graph
### Depends On
- [[com.unity.modules.terrain]] `1.0.0`
- [[com.unity.modules.terrainphysics]] `1.0.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Terrain Tools
- What's new
- Upgrade guide
- Getting started with Terrain Tools
- Installing Terrain Tools and Sample Assets
- Create a custom Terrain tool
- Create your tool script
- Add UI for Brush controls
- Modify the Terrain heightmap
- Custom Terrain Tool shaders
- Filter Stacks, Filters, and procedural masks
- Shortcut handlers
- Paint Terrain
- Sculpt
- Bridge
- Clone
- Noise
- Terrace

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.TerrainTools`: 48 documented types

## API Type Index
- `ActiveRenderTextureScope` in `UnityEditor.TerrainTools`
- `BaseBrushUIGroup` in `UnityEditor.TerrainTools`
- `BaseBrushUIGroup.OnPaintOccurrence` in `UnityEditor.TerrainTools`
- `BaseBrushVariator` in `UnityEditor.TerrainTools`
- `BrushModifierKey` in `UnityEditor.TerrainTools`
- `BrushOverlaysGUIFlags` in `UnityEditor.TerrainTools`
- `BrushShortcutHandler` in `UnityEditor.TerrainTools`
- `BrushShortcutType` in `UnityEditor.TerrainTools`
- `DetailUtility` in `UnityEditor.TerrainTools`
- `Filter` in `UnityEditor.TerrainTools`
- `FilterContext` in `UnityEditor.TerrainTools`
- `FilterContext.Keywords` in `UnityEditor.TerrainTools`
- `FilterStack` in `UnityEditor.TerrainTools`
- `FilterStackView` in `UnityEditor.TerrainTools`
- `FilterUtility` in `UnityEditor.TerrainTools`
- `FilterUtility.BuiltinPasses` in `UnityEditor.TerrainTools`

## API Type Details
### `ActiveRenderTextureScope` (Struct)
- Namespace: `UnityEditor.TerrainTools`
- Summary: Provides methods for changing and restoring active RenderTexture s.
- Page: https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.3/api/UnityEditor.TerrainTools.ActiveRenderTextureScope.html
- Constructors:
- `ActiveRenderTextureScope(RenderTexture)`: Initializes and returns an instance of ActiveRenderTextureScope .
- Methods:
- `Dispose()`: Restores the previous RenderTexture .
### `BaseBrushUIGroup` (Class)
- Namespace: `UnityEditor.TerrainTools`
- Summary: Provides methods for altering brush data.
- Page: https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.3/api/UnityEditor.TerrainTools.BaseBrushUIGroup.html
- Constructors:
- `BaseBrushUIGroup(string, Func<IBrushParameter[]>)`: Initializes and returns an instance of BaseBrushUIGroup.
- Fields:
- `isRecording`: Checks if the brush strokes are being recorded.
- Properties:
- `InvertStrength`: Inverts the brush strength.
- `allowPaint`: Checks if painting is allowed.
- `brushMaskFilterStack`: Gets the brush mask's FilterStack .
- `brushMaskFilterStackView`: Gets the brush mask's FilterStackView .
- `brushName`: Returns the brush name.
- Methods:
- `AddController<TController>(TController)`: Adds a generic controller of type IBrushController to the brush's controller list.
- `AddModifierKeyController<TController>(TController)`: Adds a modifier key controller of type IBrushModifierKeyController to the brush's controller list.
- `AddRotationController<TController>(TController)`: Adds a rotation controller of type IBrushRotationController to the brush's controller list.
- `AddScatterController<TController>(TController)`: Adds a scatter controller of type IBrushScatterController to the brush's controller list.
- `AddSizeController<TController>(TController)`: Adds a size controller of type IBrushSizeController to the brush's controller list.
- Events:
- `brushInfoAccessed`: Event that is triggered when brush information is accessed.
### `BaseBrushVariator` (Class)
- Namespace: `UnityEditor.TerrainTools`
- Summary: Represents an base terrain tools variator class.
- Page: https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.3/api/UnityEditor.TerrainTools.BaseBrushVariator.html
- Constructors:
- `BaseBrushVariator(string, IBrushEventHandler, IBrushTerrainCache)`: Initializes and returns an instance of BaseBrushVariator.
- Properties:
- `canUpdateTerrainUnderCursor`: Checks if the cursor is currently locked and can not be updated.
- `isInUse`: Checks if the brush is in use.
- `isRaycastHitUnderCursorValid`: Gets and sets the value associated to whether there is a raycast hit detecting a terrain under the cursor.
- `raycastHitUnderCursor`: Gets and sets the raycast hit that was under the cursor's position.
- `s_SceneLabelStyle`: Gets the the GUIStyle of the scene's label.
- Methods:
- `AppendBrushInfo(Terrain, IOnSceneGUI, StringBuilder)`: Adds basic information to the selected brush.
- `CalculateMouseDelta(Event, float)`: Gets the mouses delta from the mouse event.
- `CalculateMouseDeltaFromInitialPosition(Event, float)`: Calculates the difference between the mouses initial and current position.
- `GetEditorPrefs(string, bool)`: Gets the editor preferences for boolean values.
- `GetEditorPrefs(string, float)`: Gets the editor preferences for float values.

## Related Packages
- [[com.unity.shadergraph]]: shared signals #unity/rendering #unity/shaders #unity/tooling
- [[com.unity.mathematics]]: shared signals #unity/rendering #unity/shaders
- [[com.unity.render-pipelines.core]]: shared signals #unity/rendering #unity/tooling
- [[com.unity.render-pipelines.universal]]: shared signals #unity/rendering #unity/shaders
- [[com.unity.bindings.openimageio]]: shared signals #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.3/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.3/api/index.html
- Package index: [[Unity Package Docs Index]]
