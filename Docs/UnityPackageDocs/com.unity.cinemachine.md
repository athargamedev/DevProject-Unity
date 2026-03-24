# Cinemachine package

Package: `com.unity.cinemachine`
Manifest version: `3.1.6`
Lock version: `3.1.6`
Docs lookup version: `3.1.6`
Docs stream: `3.1`
Resolved docs version: `3.1.6`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-cinemachine #unity/editor/6000-4 #unity/camera #unity/cinematics #unity/input #unity/gameplay

## Summary
To use the Cinemachine package, you must install it separately from the Unity Editor. For detailed information about package requirements and installation instructions, refer to Installation .

## Package Graph
### Depends On
- [[com.unity.modules.imgui]] `1.0.0`
- [[com.unity.splines]] `2.0.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Cinemachine package
- What's new?
- Installation and upgrade
- Upgrade from Cinemachine 2.x
- Upgrade from the old Asset Store version
- Get started
- Discover Cinemachine concepts
- Cinemachine essential elements
- Camera control and transitions
- Procedural motion
- Cinemachine and Timeline
- Set up a basic Cinemachine environment
- Set up multiple Cinemachine Cameras and transitions
- Add procedural behavior to a Cinemachine Camera
- Set up Timeline with Cinemachine Cameras
- Use convenient tools and shortcuts
- Pre-built cameras
- Scene Handles

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Cinemachine`: 275 documented types
- `Unity.Cinemachine.Editor`: 10 documented types
- `Unity.Cinemachine.TargetTracking`: 4 documented types

## API Type Index
- `AxisBase` in `Unity.Cinemachine`
- `AxisState` in `Unity.Cinemachine`
- `AxisState.IInputAxisProvider` in `Unity.Cinemachine`
- `AxisState.IRequiresInput` in `Unity.Cinemachine`
- `AxisState.Recentering` in `Unity.Cinemachine`
- `AxisState.SpeedMode` in `Unity.Cinemachine`
- `CameraPipelineAttribute` in `Unity.Cinemachine`
- `CameraState` in `Unity.Cinemachine`
- `CameraState.BlendHints` in `Unity.Cinemachine`
- `CameraState.CustomBlendableItems` in `Unity.Cinemachine`
- `CameraState.CustomBlendableItems.Item` in `Unity.Cinemachine`
- `CameraStateExtensions` in `Unity.Cinemachine`
- `CameraTarget` in `Unity.Cinemachine`
- `ChildCameraPropertyAttribute` in `Unity.Cinemachine`
- `Cinemachine3OrbitRig` in `Unity.Cinemachine`
- `Cinemachine3OrbitRig.Orbit` in `Unity.Cinemachine`

## API Type Details
### `AxisBase` (Struct)
- Namespace: `Unity.Cinemachine`
- Summary: This is a deprecated component. Use InputAxis instead.
- Page: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.AxisBase.html
- Fields:
- `m_MaxValue`: The maximum value for the axis
- `m_MinValue`: The minimum value for the axis
- `m_Value`: The current value of the axis
- `m_Wrap`: If checked, then the axis will wrap around at the min/max values, forming a loop
- Methods:
- `Validate()`: Call this from OnValidate() to validate the fields of this structure (applies clamps, etc).
### `AxisState` (Struct)
- Namespace: `Unity.Cinemachine`
- Summary: AxisState is deprecated. Use InputAxis instead.
- Page: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.AxisState.html
- Constructors:
- `AxisState(float, float, bool, bool, float, float, float, string, bool)`: Constructor with specific values
- Fields:
- `Value`: The current value of the axis
- `m_AccelTime`: The amount of time in seconds it takes to accelerate to MaxSpeed with the supplied Axis at its maximum value
- `m_DecelTime`: The amount of time in seconds it takes to decelerate the axis to zero if the supplied axis is in a neutral position
- `m_InputAxisName`: The name of this axis as specified in Unity Input manager. Setting to an empty string will disable the automatic updating of this axis
- `m_InputAxisValue`: The value of the input axis. A value of 0 means no input You can drive this directly from a custom input system, or you can set the Axis…
- Properties:
- `HasInputProvider`: Returns true if this axis has an InputAxisProvider, in which case we ignore the input axis name
- `HasRecentering`: True if the Recentering member is valid (bcak-compatibility support: old versions had recentering in a separate structure)
- `ValueRangeLocked`: Value range is locked, i.e. not adjustable by the user (used by editor)
- Methods:
- `Reset()`: Cancel current input state and reset input to 0
- `SetInputAxisProvider(int, IInputAxisProvider)`: Set an input provider for this axis. If an input provider is set, the provider will be queried when user input is needed, and the Input…
- `Update(float)`: Updates the state of this axis based on the Input axis defined by AxisState.m_AxisName
- `Validate()`: Call from OnValidate: Make sure the fields are sensible
### `AxisState.IInputAxisProvider` (Interface)
- Namespace: `Unity.Cinemachine`
- Summary: This is an interface to override default querying of Unity's legacy Input system. If a befaviour implementing this interface is attached to a CinemachineCamera that requires input, that interface will be polled for…
- Page: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/Unity.Cinemachine.AxisState.IInputAxisProvider.html
- Methods:
- `GetAxisValue(int)`: Get the value of the input axis

## Related Packages
- [[com.unity.addressables]]: shared signals #unity/gameplay #unity/input
- [[com.unity.inputsystem]]: shared signals #unity/gameplay #unity/input
- [[com.unity.platformtoolkit]]: shared signals #unity/gameplay #unity/input
- [[com.unity.recorder]]: shared signals #unity/gameplay #unity/input
- [[com.unity.ugui]]: shared signals #unity/gameplay #unity/input

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/api/index.html
- Package index: [[Unity Package Docs Index]]
