# Recorder package

Package: `com.unity.recorder`
Manifest version: `5.1.5`
Lock version: `5.1.5`
Docs lookup version: `5.1.5`
Docs stream: `5.1`
Resolved docs version: `5.1.5`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-recorder #unity/editor/6000-4 #unity/recording #unity/tooling #unity/input #unity/animation #unity/gameplay

## Summary
To use the Recorder package, you must install it separately from the Unity Editor. For detailed information about package requirements and installation instructions, refer to Installation .

## Package Graph
### Depends On
- [[com.unity.bindings.openimageio]] `1.0.2`
- [[com.unity.collections]] `1.2.4`
- [[com.unity.timeline]] `1.8.7`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Recorder package
- Installation
- Recorder concepts and features
- Get started
- Record via a centralized recording session
- Recorder window interface
- Recording session properties
- Recorder list management
- Trigger recordings from Timeline
- Timeline Recorder Track and Clip interface
- Record videos and image sequences
- Record a video in Full HD MP4
- Record an animated GIF
- Record an image sequence in linear sRGB color space
- Record with alpha
- Record with Accumulation
- Understand sub-frame capture
- Accumulate motion blur

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.Recorder`: 41 documented types
- `UnityEditor.Recorder.Input`: 15 documented types
- `UnityEditor.Recorder.Encoder`: 14 documented types
- `UnityEditor.Recorder.Timeline`: 2 documented types
- `Unity.Media`: 1 documented types
- `UnityEditor.Recorder.FrameCapturer`: 1 documented types
- `UnityEngine.Recorder`: 1 documented types

## API Type Index
- `RefHandle` in `Unity.Media`
- `AOVRecorderSettings` in `UnityEditor.Recorder`
- `AOVType` in `UnityEditor.Recorder`
- `AnimationRecorderSettings` in `UnityEditor.Recorder`
- `AudioRecorderSettings` in `UnityEditor.Recorder`
- `BaseRenderTextureInput` in `UnityEditor.Recorder`
- `BaseTextureRecorder` in `UnityEditor.Recorder`
- `BindingManager` in `UnityEditor.Recorder`
- `CompressionUtility` in `UnityEditor.Recorder`
- `CompressionUtility.EXRCompressionType` in `UnityEditor.Recorder`
- `DefaultWildcard` in `UnityEditor.Recorder`
- `FileNameGenerator` in `UnityEditor.Recorder`
- `FrameRatePlayback` in `UnityEditor.Recorder`
- `GIFRecorderSettings` in `UnityEditor.Recorder`
- `GenericRecorder` in `UnityEditor.Recorder`
- `ImageInputSelector` in `UnityEditor.Recorder`

## API Type Details
### `RefHandle<T>` (Class)
- Namespace: `Unity.Media`
- Summary: A class that handles the allocation and disposal of an object. All the different encoders use it.
- Page: https://docs.unity3d.com/Packages/com.unity.recorder@5.1/api/Unity.Media.RefHandle-1.html
- Constructors:
- `RefHandle()`: The constructor of the handle.
- `RefHandle(T)`: The constructor of the handle.
- Properties:
- `IsCreated`: Specifies whether the handle has been allocated or not.
- `Target`: The target object of the handle.
- Methods:
- `Dispose()`: Cleans up the handle's resources.
- `Dispose(bool)`: Cleans up the handle's resources.
- `~RefHandle()`: The finalizer of the class.
### `AOVRecorderSettings` (Class)
- Namespace: `UnityEditor.Recorder`
- Summary: A class that represents the settings of an AOV Sequence Recorder.
- Page: https://docs.unity3d.com/Packages/com.unity.recorder@5.1/api/UnityEditor.Recorder.AOVRecorderSettings.html
- Constructors:
- `AOVRecorderSettings()`: Default constructor.
- Properties:
- `AOVSelection`: Indicates the selected AOV to render.
- `CaptureHDR`: Use this property to capture the frames in HDR (if the setup supports it).
- `EXRCompression`: Stores the data compression method to use to encode image files in the EXR format.
- `EXRCompressionLevel`: Stores the data compression level for compression methods that support it to encode image files in the EXR format.
- `Extension`: Stores the file extension used by this Recorder (without the dot).
- Methods:
- `GetAOVSelection()`: Indicates the selected AOVs to render.
- `GetErrors(List<string>)`: Add AOV related error description strings to the errors list.
- `OnUpgradeFromVersion()`: Defines how to handle the upgrade of Recorder Settings created in a previous version according to their type. Unity automatically calls…
- `SetAOVSelection(params AOVType[])`: Indicates the selected AOVs to render.
### `AOVType` (Enum)
- Namespace: `UnityEditor.Recorder`
- Summary: Available options AOV Types.
- Page: https://docs.unity3d.com/Packages/com.unity.recorder@5.1/api/UnityEditor.Recorder.AOVType.html

## Related Packages
- [[com.unity.bindings.openimageio]]: shared signals #unity/recording #unity/tooling #unity/dependency-graph
- [[com.unity.ugui]]: shared signals #unity/animation #unity/gameplay #unity/input
- [[com.unity.addressables]]: shared signals #unity/gameplay #unity/input
- [[com.unity.cinemachine]]: shared signals #unity/gameplay #unity/input
- [[com.unity.inputsystem]]: shared signals #unity/gameplay #unity/input

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.recorder@5.1/api/index.html
- Package index: [[Unity Package Docs Index]]
