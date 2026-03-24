# Input System

Package: `com.unity.inputsystem`
Manifest version: `1.19.0`
Lock version: `file:com.unity.inputsystem`
Docs lookup version: `1.19.0`
Docs stream: `1.19`
Resolved docs version: `1.19.0`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-inputsystem #unity/editor/6000-4 #unity/input #unity/gameplay

## Summary
The Input System allows your users to control your game or app using a device, touch, or gestures. The Input Actions Editor window, displaying some of the default actions that come pre-configured with the Input System package.

## Package Graph
### Depends On
- [[com.unity.modules.uielements]] `1.0.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Introduction
- Installation
- Quickstart Guide
- Videos
- Concepts
- Workflows
- Workflow - Actions
- Workflow - Actions & PlayerInput
- Workflow - Direct
- Project-Wide Actions
- Configuring Input
- Actions
- Responding to Actions
- Input Action Assets
- Input Bindings
- Interactions
- Devices
- Controls

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.InputSystem`: 105 documented types
- `UnityEngine.InputSystem.LowLevel`: 57 documented types
- `UnityEngine.InputSystem.Android`: 20 documented types
- `UnityEngine.InputSystem.Controls`: 19 documented types
- `UnityEngine.InputSystem.Composites`: 14 documented types
- `UnityEngine.InputSystem.HID`: 14 documented types
- `UnityEngine.InputSystem.Utilities`: 14 documented types
- `UnityEngine.InputSystem.Processors`: 13 documented types
- `UnityEngine.InputSystem.Layouts`: 10 documented types
- `UnityEngine.InputSystem.Editor`: 9 documented types
- `UnityEngine.InputSystem.UI`: 9 documented types
- `UnityEngine.InputSystem.XInput`: 9 documented types
- `UnityEngine.InputSystem.XR`: 8 documented types
- `UnityEngine.InputSystem.Interactions`: 6 documented types
- `UnityEngine.InputSystem.DualShock`: 5 documented types
- `UnityEngine.InputSystem.EnhancedTouch`: 5 documented types
- `UnityEngine.InputSystem.Users`: 5 documented types
- `UnityEngine.InputSystem.iOS`: 5 documented types
- `UnityEngine.InputSystem.Android.LowLevel`: 4 documented types
- `UnityEngine.InputSystem.OnScreen`: 4 documented types
- `UnityEngine.InputSystem.Haptics`: 2 documented types
- `UnityEngine.InputSystem.WebGL`: 2 documented types
- `UnityEngine.InputSystem.iOS.LowLevel`: 2 documented types
- `UnityEngine.InputSystem.DualShock.LowLevel`: 1 documented types
- `UnityEngine.InputSystem.OSX`: 1 documented types
- `UnityEngine.InputSystem.Switch`: 1 documented types

## API Type Index
- `Accelerometer` in `UnityEngine.InputSystem`
- `AmbientTemperatureSensor` in `UnityEngine.InputSystem`
- `AssetDatabaseUtils` in `UnityEngine.InputSystem`
- `AttitudeSensor` in `UnityEngine.InputSystem`
- `CommonUsages` in `UnityEngine.InputSystem`
- `DefaultInputActions` in `UnityEngine.InputSystem`
- `DefaultInputActions.IPlayerActions` in `UnityEngine.InputSystem`
- `DefaultInputActions.IUIActions` in `UnityEngine.InputSystem`
- `DefaultInputActions.PlayerActions` in `UnityEngine.InputSystem`
- `DefaultInputActions.UIActions` in `UnityEngine.InputSystem`
- `Gamepad` in `UnityEngine.InputSystem`
- `GravitySensor` in `UnityEngine.InputSystem`
- `Gyroscope` in `UnityEngine.InputSystem`
- `HingeAngle` in `UnityEngine.InputSystem`
- `HumiditySensor` in `UnityEngine.InputSystem`
- `IInputActionCollection` in `UnityEngine.InputSystem`

## API Type Details
### `Accelerometer` (Class)
- Namespace: `UnityEngine.InputSystem`
- Summary: Input device representing an accelerometer sensor.
- Page: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.Accelerometer.html
- Properties:
- `acceleration`
- `current`: The accelerometer that was last added or had activity last.
- Methods:
- `FinishSetup()`: Perform final initialization tasks after the control hierarchy has been put into place.
- `MakeCurrent()`: Make this the current device of its type.
- `OnRemoved()`: Called by the system when the device is removed from devices .
### `AmbientTemperatureSensor` (Class)
- Namespace: `UnityEngine.InputSystem`
- Summary: Input device representing the ambient air temperature measured by the device playing the content.
- Page: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.AmbientTemperatureSensor.html
- Properties:
- `ambientTemperature`: Temperature in degree Celsius.
- `current`: The ambient temperature sensor that was last added or had activity last.
- Methods:
- `FinishSetup()`: Perform final initialization tasks after the control hierarchy has been put into place.
- `MakeCurrent()`: Make this the current device of its type.
- `OnRemoved()`: Called by the system when the device is removed from devices .
### `AssetDatabaseUtils` (Class)
- Namespace: `UnityEngine.InputSystem`
- Page: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.AssetDatabaseUtils.html
- Methods:
- `CreateAsset<T>(string, string)`
- `CreateAsset<T>(string, string, string)`
- `CreateDirectory()`
- `ExternalDeleteFileOrDirectory(string)`
- `ExternalMoveFileOrDirectory(string, string)`

## Related Packages
- [[com.unity.addressables]]: shared signals #unity/gameplay #unity/input
- [[com.unity.cinemachine]]: shared signals #unity/gameplay #unity/input
- [[com.unity.platformtoolkit]]: shared signals #unity/gameplay #unity/input
- [[com.unity.recorder]]: shared signals #unity/gameplay #unity/input
- [[com.unity.ugui]]: shared signals #unity/gameplay #unity/input

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/index.html
- Package index: [[Unity Package Docs Index]]
