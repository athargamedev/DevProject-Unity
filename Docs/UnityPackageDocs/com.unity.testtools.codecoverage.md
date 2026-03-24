# About Code Coverage

Package: `com.unity.testtools.codecoverage`
Manifest version: `1.3.0`
Lock version: `1.3.0`
Docs lookup version: `1.3.0`
Docs stream: `1.3`
Resolved docs version: `1.3.0`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-testtools-codecoverage #unity/editor/6000-4 #unity/testing #unity/tooling

## Summary
Code Coverage is a measure of how much of your code has been executed. It is normally associated with automated tests, but you can gather coverage data in Unity at any time when the Editor is running.

## Package Graph
### Depends On
- [[com.unity.settings-manager]] `2.0.0`
- [[com.unity.test-framework]] `1.4.5`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- About Code Coverage
- What's new
- Upgrade guide
- Quickstart
- Installing Code Coverage
- Using Code Coverage
- Code Coverage window
- Using Code Coverage with Test Runner
- On demand coverage recording
- Using Code Coverage in batchmode
- How to interpret the results
- Technical details
- Document revision history
- Archive

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.TestTools.CodeCoverage`: 5 documented types

## API Type Index
- `CodeCoverage` in `UnityEditor.TestTools.CodeCoverage`
- `Events` in `UnityEditor.TestTools.CodeCoverage`
- `LogVerbosityLevel` in `UnityEditor.TestTools.CodeCoverage`
- `SessionEventInfo` in `UnityEditor.TestTools.CodeCoverage`
- `SessionMode` in `UnityEditor.TestTools.CodeCoverage`

## API Type Details
### `CodeCoverage` (Class)
- Namespace: `UnityEditor.TestTools.CodeCoverage`
- Summary: Utility class for the CodeCoverage API.
- Page: https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.3/api/UnityEditor.TestTools.CodeCoverage.CodeCoverage.html
- Properties:
- `VerbosityLevel`: Sets the verbosity level used in editor and console logs. The default level is Info .
- Methods:
- `PauseRecording()`: Call this to pause the recording on the current coverage recording session.
- `StartRecording()`: Call this to start a new coverage recording session.
- `StopRecording()`: Call this to end the current coverage recording session.
- `UnpauseRecording()`: Call this to continue recording on the current coverage recording session, after having paused the recording.
### `Events` (Class)
- Namespace: `UnityEditor.TestTools.CodeCoverage`
- Summary: Events invoked during a code coverage session. A code coverage session is the period between starting and finishing capturing code coverage data.
- Page: https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.3/api/UnityEditor.TestTools.CodeCoverage.Events.html
- Events:
- `onCoverageSessionFinished`: This event is invoked when a code coverage session is finished.
- `onCoverageSessionPaused`: This event is invoked when a code coverage session is paused.
- `onCoverageSessionStarted`: This event is invoked when a code coverage session is started.
- `onCoverageSessionUnpaused`: This event is invoked when a code coverage session is unpaused.
### `LogVerbosityLevel` (Enum)
- Namespace: `UnityEditor.TestTools.CodeCoverage`
- Summary: The verbosity level used in editor and console logs.
- Page: https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.3/api/UnityEditor.TestTools.CodeCoverage.LogVerbosityLevel.html

## Related Packages
- [[com.unity.test-framework]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.render-pipelines.core]]: shared signals #unity/testing #unity/tooling
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.3/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.testtools.codecoverage@1.3/api/index.html
- Package index: [[Unity Package Docs Index]]
