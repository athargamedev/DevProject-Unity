# Unity UI (uGUI)

Package: `com.unity.ugui`
Manifest version: `2.0.0`
Lock version: `file:com.unity.ugui`
Docs lookup version: `2.0.0`
Docs stream: `2.0`
Resolved docs version: `2.0.0`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-ugui #unity/editor/6000-4 #unity/ui #unity/input #unity/animation #unity/gameplay

## Summary
Unity UI (uGUI) is a GameObject-based UI system that you can use to develop user interfaces for games and applications. It uses Components and the Game view to arrange, position, and style user interfaces.

## Package Graph
### Depends On
- [[com.unity.modules.imgui]] `1.0.0`
- [[com.unity.modules.ui]] `1.0.0`

### Required By
- [[com.unity.render-pipelines.core]]

## Manual Map
- Unity UI
- Unity UI: Unity User Interface
- Canvas
- Basic Layout
- Visual Components
- Interaction Components
- Animation Integration
- Auto Layout
- Rich Text
- Events
- MessagingSystem
- InputModules
- SupportedEvents
- Raycasters
- Reference
- Rect Transform
- Canvas Components
- Canvas Scaler

## API Overview
This section of the documentation provides detailed information about the Unity UI scripting API. To effectively use this information, you should be familiar with the basic concepts and practices of scripting in Unity, as explained in the Scripting section…

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `TMPro`: 139 documented types
- `UnityEngine.UI`: 105 documented types
- `UnityEngine.EventSystems`: 50 documented types
- `TMPro.EditorUtilities`: 39 documented types
- `UnityEditor.UI`: 26 documented types
- `TMPro.SpriteAssetUtilities`: 7 documented types
- `UnityEditor.EventSystems`: 6 documented types
- `UnityEngine.UIElements`: 3 documented types
- `UnityEngine.TextCore`: 1 documented types

## API Type Index
- `AlternateSubstitutionRecord` in `TMPro`
- `AtlasPopulationMode` in `TMPro`
- `CaretInfo` in `TMPro`
- `CaretPosition` in `TMPro`
- `ColorMode` in `TMPro`
- `Compute_DT_EventArgs` in `TMPro`
- `Compute_DistanceTransform_EventTypes` in `TMPro`
- `Extents` in `TMPro`
- `FaceInfo_Legacy` in `TMPro`
- `FastAction` in `TMPro`
- `FastAction` in `TMPro`
- `FastAction` in `TMPro`
- `FastAction` in `TMPro`
- `FontAssetCreationSettings` in `TMPro`
- `FontFeatureLookupFlags` in `TMPro`
- `FontStyles` in `TMPro`

## API Type Details
### `AlternateSubstitutionRecord` (Struct)
- Namespace: `TMPro`
- Summary: The AlternateSubstitutionRecord defines the substitution of a single glyph by several potential alternative glyphs.
- Page: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/TMPro.AlternateSubstitutionRecord.html
### `AtlasPopulationMode` (Enum)
- Namespace: `TMPro`
- Summary: Atlas population modes which ultimately determines the type of font asset.
- Page: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/TMPro.AtlasPopulationMode.html
### `CaretInfo` (Struct)
- Namespace: `TMPro`
- Summary: Structure which contains the character index and position of caret relative to the character.
- Page: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/TMPro.CaretInfo.html
- Constructors:
- `CaretInfo(int, CaretPosition)`
- Fields:
- `index`
- `position`

## Related Packages
- [[com.unity.recorder]]: shared signals #unity/animation #unity/gameplay #unity/input
- [[com.unity.addressables]]: shared signals #unity/gameplay #unity/input
- [[com.unity.cinemachine]]: shared signals #unity/gameplay #unity/input
- [[com.unity.inputsystem]]: shared signals #unity/gameplay #unity/input
- [[com.unity.platformtoolkit]]: shared signals #unity/gameplay #unity/input

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/index.html
- Package index: [[Unity Package Docs Index]]
