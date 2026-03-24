# About Splines

Package: `com.unity.splines`
Manifest version: `2.8.4`
Lock version: `2.8.4`
Docs lookup version: `2.8.4`
Docs stream: `2.8`
Resolved docs version: `2.8.4`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-splines #unity/editor/6000-4

## Summary
Work with curves and paths. Use the Splines package to generate objects and behaviors along paths, create trajectories, and draw shapes. The Splines package contains:

## Package Graph
### Depends On
- [[com.unity.mathematics]] `1.2.1`
- [[com.unity.modules.imgui]] `1.0.0`
- [[com.unity.settings-manager]] `1.0.3`

### Required By
- [[com.unity.cinemachine]]

## Manual Map
- About Splines
- Splines upgrade guide
- Create a spline
- Manipulate splines
- Reverse the flow of a spline
- Join splines
- Knots
- Link and unlink knots
- Split knots
- Tangents
- Tangent modes
- Select a tangent mode
- Select a default tangent mode
- Animate along a spline
- Animate a GameObject along a spline
- Change the alignment of an animated GameObject
- Configure the movement of a GameObject
- Spline Animate component reference

## API Overview
Splines are defined as implementing the ISpline interface. There are two default implementations: a mutable Spline class and an immutable NativeSpline .

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.Splines`: 57 documented types
- `UnityEditor.Splines`: 24 documented types
- `UnityEngine.Splines.Interpolators`: 13 documented types
- `UnityEngine.Splines.ExtrusionShapes`: 5 documented types

## API Type Index
- `EditorSplineUtility` in `UnityEditor.Splines`
- `EmbeddedSplineDataPropertyDrawer` in `UnityEditor.Splines`
- `HandleOrientation` in `UnityEditor.Splines`
- `ISelectableElement` in `UnityEditor.Splines`
- `SelectableKnot` in `UnityEditor.Splines`
- `SelectableTangent` in `UnityEditor.Splines`
- `SerializedPropertyUtility` in `UnityEditor.Splines`
- `SplineDataHandles` in `UnityEditor.Splines`
- `SplineExtrudeUtility` in `UnityEditor.Splines`
- `SplineGUI` in `UnityEditor.Splines`
- `SplineGUILayout` in `UnityEditor.Splines`
- `SplineGizmoUtility` in `UnityEditor.Splines`
- `SplineHandles` in `UnityEditor.Splines`
- `SplineHandles.SplineHandleScope` in `UnityEditor.Splines`
- `SplineIndexPropertyDrawer` in `UnityEditor.Splines`
- `SplineInfoPropertyDrawer` in `UnityEditor.Splines`

## API Type Details
### `EditorSplineUtility` (Class)
- Namespace: `UnityEditor.Splines`
- Summary: Editor utility functions for working with Spline and SplineData<T> .
- Page: https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEditor.Splines.EditorSplineUtility.html
- Properties:
- `DefaultTangentMode`: Represents the default TangentMode used to place or insert knots. If the user does not define tangent handles, then the tangent takes the…
- Methods:
- `CopySplineDataIfEmpty(ISplineContainer, int, int, EmbeddedSplineDataType, string)`: Copy an embedded SplineData<T> collection to a new Spline if the destination does not already contain an entry matching the type and key .
- `RegisterSplineDataChanged<T>(Action<SplineData<T>>)`: Use this function to register a callback that gets invoked once per-frame if any SplineData<T> changes occur.
- `SetKnotPlacementTool()`: Sets the current active context to the SplineToolContext and the current active tool to the Draw Splines Tool ( CreateSplineTool )
- `UnregisterSplineDataChanged<T>(Action<SplineData<T>>)`: Use this function to unregister SplineData<T> change callback.
- Events:
- `AfterSplineWasModified`: Invoked once per-frame if a spline property has been modified.
### `EmbeddedSplineDataPropertyDrawer` (Class)
- Namespace: `UnityEditor.Splines`
- Summary: Creates a property drawer for EmbeddedSplineData types.
- Page: https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEditor.Splines.EmbeddedSplineDataPropertyDrawer.html
- Methods:
- `GetPropertyHeight(SerializedProperty, GUIContent)`: Gets the height of a SerializedProperty in pixels.
- `OnGUI(Rect, SerializedProperty, GUIContent)`: Creates an interface for a SerializedProperty with an EmbeddedSplineData type.
### `HandleOrientation` (Enum)
- Namespace: `UnityEditor.Splines`
- Summary: Describes how the handles are oriented. Besides the default tool handle rotation settings, Global and Local, spline elements have the Parent and Element handle rotations. When elements are selected, a tool's handle…
- Page: https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEditor.Splines.HandleOrientation.html

## Related Packages
- [[com.unity.cinemachine]]: shared signals #unity/dependency-graph
- [[com.unity.mathematics]]: shared signals #unity/dependency-graph
- [[com.unity.settings-manager]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.splines@2.8/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/index.html
- Package index: [[Unity Package Docs Index]]
