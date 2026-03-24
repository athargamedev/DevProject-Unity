# Get started with UI Test Framework

Package: `com.unity.ui.test-framework`
Manifest version: `6.4.0`
Lock version: `6.4.0`
Docs lookup version: `6.4.0`
Docs stream: `6.4`
Resolved docs version: `6.4.0`
Source: `builtin`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-ui-test-framework #unity/editor/6000-4 #unity/ui #unity/testing #unity/tooling

## Summary
The UI Test Framework package facilitates automated testing for UI Toolkit-based UI. It provides a set of APIs that help you simulate user interactions, manage UI state, and verify UI behavior.

## Package Graph
### Depends On
- [[com.unity.ext.nunit]] `2.0.3`
- [[com.unity.modules.imgui]] `1.0.0`
- [[com.unity.modules.uielements]] `1.0.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Get started
- Install and set up
- Create your first UI test
- Test with test fixtures
- Introduction to test fixtures
- Test in both Editor and runtime states
- Test with Editor window
- Test without Editor window
- Test in runtime
- Debug UI test fixtures
- Trigger and update UI
- Create multi-window tests
- Customize test fixtures
- Add styles during testing
- Clean up objects after tests
- Create your own reusable test components
- Simulate UI interactions
- Click on a visual element

## API Overview
This section of the documentation provides detailed information about the UI Test Framework scripting API. To effectively use this information, you should be familiar with the basic concepts and practices of scripting in Unity, as explained in the Scripting…

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.UIElements.TestFramework`: 13 documented types
- `UnityEditor.UIElements.TestFramework`: 6 documented types

## API Type Index
- `ContextMenuSimulator` in `UnityEditor.UIElements.TestFramework`
- `EditorPanelSimulator` in `UnityEditor.UIElements.TestFramework`
- `EditorWindowPanelSimulator` in `UnityEditor.UIElements.TestFramework`
- `EditorWindowUITestFixture` in `UnityEditor.UIElements.TestFramework`
- `InspectorTestUtility` in `UnityEditor.UIElements.TestFramework`
- `StylesApplicator` in `UnityEditor.UIElements.TestFramework`
- `AbstractUITestFixture` in `UnityEngine.UIElements.TestFramework`
- `CleanupUtil` in `UnityEngine.UIElements.TestFramework`
- `CommonUITestFixture` in `UnityEngine.UIElements.TestFramework`
- `MenuSimulator` in `UnityEngine.UIElements.TestFramework`
- `PanelSimulator` in `UnityEngine.UIElements.TestFramework`
- `PickingDirection` in `UnityEngine.UIElements.TestFramework`
- `PopupMenuSimulator` in `UnityEngine.UIElements.TestFramework`
- `RuntimePanelSimulator` in `UnityEngine.UIElements.TestFramework`
- `RuntimeUITestFixture` in `UnityEngine.UIElements.TestFramework`
- `UITestComponent` in `UnityEngine.UIElements.TestFramework`

## API Type Details
### `ContextMenuSimulator` (Class)
- Namespace: `UnityEditor.UIElements.TestFramework`
- Summary: Provides a testable wrapper for context menu interactions in UI Toolkit tests. Allows simulation and verification of context menu display, item selection, and menu content without invoking the native system menu.
- Page: https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.4/api/UnityEditor.UIElements.TestFramework.ContextMenuSimulator.html
- Methods:
- `AfterTest()`: Internal lifecycle method invoked automatically by the test fixture.
- `BeforeTest()`: Internal lifecycle method invoked automatically by the test fixture.
### `EditorPanelSimulator` (Class)
- Namespace: `UnityEditor.UIElements.TestFramework`
- Summary: A PanelSimulator with a default Editor panel.
- Page: https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.4/api/UnityEditor.UIElements.TestFramework.EditorPanelSimulator.html
- Constructors:
- `EditorPanelSimulator()`: Creates a new Editor panel.
- Properties:
- `panelSize`: The size of the rootVisualElement of the panel.
- Methods:
- `ApplyPanelSize()`: Applies the m_PanelSize to the panel.
- `CreatePanel()`: Creates the panel and initializes the rootVisualElement .
- `Dispose()`: Disposes of the panel and releases its resources.
- `FrameUpdate(double)`: Performs a frame update of the panel.
- `RecreatePanel()`: Recreates the panel.
### `EditorWindowPanelSimulator` (Class)
- Namespace: `UnityEditor.UIElements.TestFramework`
- Summary: A PanelSimulator accessing an EditorWindow 's panel. Allows for the simulation of time passing, sending events, and updating the panel in a synchronous manner.
- Page: https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.4/api/UnityEditor.UIElements.TestFramework.EditorWindowPanelSimulator.html
- Constructors:
- `EditorWindowPanelSimulator(EditorWindow)`: Sets up the provided window for simulation.
- Properties:
- `window`: EditorWindow tied to the panel.
- Methods:
- `FrameUpdate(double)`: Performs a frame update of the panel.
- `SetWindow(EditorWindow)`: Assigns the specified window to the EditorWindowPanelSimulator .

## Related Packages
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.render-pipelines.core]]: shared signals #unity/testing #unity/tooling
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/testing #unity/tooling
- [[com.unity.searcher]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.4/api/index.html
- Package index: [[Unity Package Docs Index]]
