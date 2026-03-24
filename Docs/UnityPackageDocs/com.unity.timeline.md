# Unity's Timeline

Package: `com.unity.timeline`
Manifest version: `Not listed`
Lock version: `1.8.9`
Docs lookup version: `1.8.9`
Docs stream: `1.8`
Resolved docs version: `1.8.12`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-timeline #unity/editor/6000-4 #unity/animation

## Summary
Use Unity's Timeline to create cinematic content, gameplay sequences, audio sequences, and complex particle effects. Each cut-scene, cinematic, or gameplay sequence that you create with Unity's Timeline consists of a Timeline asset and a Timeline instance. The Timeline window creates and modifies Timeline assets and…

## Package Graph
### Depends On
- [[com.unity.modules.animation]] `1.0.0`
- [[com.unity.modules.audio]] `1.0.0`
- [[com.unity.modules.director]] `1.0.0`
- [[com.unity.modules.particlesystem]] `1.0.0`

### Required By
- [[com.unity.recorder]]

## Manual Map
- Unity's Timeline
- Install Timeline
- Timeline Samples
- Customization Samples
- Gameplay Sequence Demo
- Timeline Workflows
- Create a Timeline asset and Timeline instance
- Record basic animation
- Convert an Infinite clip to an Animation clip
- Animate a humanoid
- Override upper-body animation
- Create a Sub-Timeline instance
- Use markers and signals for footsteps
- Create a custom Notes marker
- Timeline assets and instances
- Timeline window
- Timeline Preview
- Timeline Playback Controls

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.Timeline`: 57 documented types
- `UnityEditor.Timeline`: 25 documented types
- `UnityEditor.Timeline.Actions`: 22 documented types

## API Type Index
- `AnimationTrackExtensions` in `UnityEditor.Timeline`
- `ClipBackgroundRegion` in `UnityEditor.Timeline`
- `ClipDrawOptions` in `UnityEditor.Timeline`
- `ClipEditor` in `UnityEditor.Timeline`
- `CustomTimelineEditorAttribute` in `UnityEditor.Timeline`
- `IInspectorChangeHandler` in `UnityEditor.Timeline`
- `MarkerDrawOptions` in `UnityEditor.Timeline`
- `MarkerEditor` in `UnityEditor.Timeline`
- `MarkerOverlayRegion` in `UnityEditor.Timeline`
- `MarkerUIStates` in `UnityEditor.Timeline`
- `PlaybackScrollMode` in `UnityEditor.Timeline`
- `RefreshReason` in `UnityEditor.Timeline`
- `SequenceContext` in `UnityEditor.Timeline`
- `TimeFormat` in `UnityEditor.Timeline`
- `TimelineEditor` in `UnityEditor.Timeline`
- `TimelineEditorWindow` in `UnityEditor.Timeline`

## API Type Details
### `ClipBackgroundRegion` (Struct)
- Namespace: `UnityEditor.Timeline`
- Summary: Description of the on-screen area where a clip is drawn
- Page: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEditor.Timeline.ClipBackgroundRegion.html
- Constructors:
- `ClipBackgroundRegion(Rect, double, double)`: Constructor
- Properties:
- `endTime`: The end time of the region, relative to the clip.
- `position`: The rectangle where the background of the clip is drawn.
- `startTime`: The start time of the region, relative to the clip.
- Methods:
- `Equals(object)`: Indicates whether this instance and a specified object are equal.
- `Equals(ClipBackgroundRegion)`: Compares this object with another ClipBackgroundRegion .
- `GetHashCode()`: Returns the hash code for this instance.
- Operators:
- `operator ==(ClipBackgroundRegion, ClipBackgroundRegion)`: Compares two ClipBackgroundRegion objects.
- `operator !=(ClipBackgroundRegion, ClipBackgroundRegion)`: Compares two ClipBackgroundRegion objects.
### `ClipDrawOptions` (Struct)
- Namespace: `UnityEditor.Timeline`
- Summary: The user-defined options for drawing a clip.
- Page: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEditor.Timeline.ClipDrawOptions.html
- Properties:
- `displayClipName`: Controls the display of the clip name.
- `errorText`: Text that indicates if the clip should display an error.
- `hideScaleIndicator`: Controls the display of the clip scale indicator.
- `highlightColor`: The color drawn under the clip. By default, the color is the same as the track color.
- `icons`: Icons to display on the clip.
- Methods:
- `Equals(object)`: Indicates whether this instance and a specified object are equal.
- `Equals(ClipDrawOptions)`: Compares this object with another ClipDrawOptions .
- `GetHashCode()`: Returns the hash code for this instance.
- Operators:
- `operator ==(ClipDrawOptions, ClipDrawOptions)`: Compares two ClipDrawOptions objects.
- `operator !=(ClipDrawOptions, ClipDrawOptions)`: Compares two ClipDrawOptions objects.
### `ClipEditor` (Class)
- Namespace: `UnityEditor.Timeline`
- Summary: Use this class to customize clip types in the TimelineEditor.
- Page: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/UnityEditor.Timeline.ClipEditor.html
- Constructors:
- `ClipEditor()`: Default constructor
- Methods:
- `DrawBackground(TimelineClip, ClipBackgroundRegion)`: Override this method to draw a background for a clip .
- `GetClipOptions(TimelineClip)`: Implement this method to override the default options for drawing a clip.
- `GetDefaultHighlightColor(TimelineClip)`: The color drawn under the clip. By default, the color is the same as the track color.
- `GetErrorText(TimelineClip)`: Gets the error text for the specified clip.
- `GetSubTimelines(TimelineClip, PlayableDirector, List<PlayableDirector>)`: Gets the sub-timelines for a specific clip. Implement this method if your clip supports playing nested timelines.

## Related Packages
- [[com.unity.recorder]]: shared signals #unity/animation #unity/dependency-graph
- [[com.unity.animation.rigging]]: shared signals #unity/animation
- [[com.unity.ugui]]: shared signals #unity/animation

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/api/index.html
- Package index: [[Unity Package Docs Index]]
