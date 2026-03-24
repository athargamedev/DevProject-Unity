# Searcher

Package: `com.unity.searcher`
Manifest version: `Not listed`
Lock version: `4.9.4`
Docs lookup version: `4.9.4`
Docs stream: `4.9`
Resolved docs version: `4.9.4`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-searcher #unity/editor/6000-4 #unity/testing #unity/tooling

## Summary
Currently, the API for the Searcher is not intended for public use. Using the API in external custom scripts or modifying the API is not recommended or supported at this time.

## Package Graph
### Depends On
- None listed in packages-lock.json

### Required By
- [[com.unity.shadergraph]]

## Manual Map
- Introduction

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.Searcher`: 14 documented types
- `UnityEditor.Searcher.Tests`: 1 documented types

## API Type Index
- `ISearcherAdapter` in `UnityEditor.Searcher`
- `ItemExpanderState` in `UnityEditor.Searcher`
- `Searcher` in `UnityEditor.Searcher`
- `Searcher.AnalyticsEvent` in `UnityEditor.Searcher`
- `Searcher.AnalyticsEvent.EventType` in `UnityEditor.Searcher`
- `SearcherAdapter` in `UnityEditor.Searcher`
- `SearcherDatabase` in `UnityEditor.Searcher`
- `SearcherDatabaseBase` in `UnityEditor.Searcher`
- `SearcherItem` in `UnityEditor.Searcher`
- `SearcherTreeUtility` in `UnityEditor.Searcher`
- `SearcherWindow` in `UnityEditor.Searcher`
- `SearcherWindow.Alignment` in `UnityEditor.Searcher`
- `SearcherWindow.Alignment.Horizontal` in `UnityEditor.Searcher`
- `SearcherWindow.Alignment.Vertical` in `UnityEditor.Searcher`
- `MatchTests` in `UnityEditor.Searcher.Tests`

## API Type Details
### `Searcher` (Class)
- Namespace: `UnityEditor.Searcher`
- Page: https://docs.unity3d.com/Packages/com.unity.searcher@4.9/api/UnityEditor.Searcher.Searcher.html
- Constructors:
- `Searcher(IEnumerable<SearcherDatabaseBase>, string)`
- `Searcher(IEnumerable<SearcherDatabaseBase>, ISearcherAdapter)`
- `Searcher(SearcherDatabaseBase, string)`
- `Searcher(SearcherDatabaseBase, ISearcherAdapter)`
- Properties:
- `Adapter`
- `SortComparison`
- Methods:
- `BuildIndices()`
- `Search(string)`
### `ISearcherAdapter` (Interface)
- Namespace: `UnityEditor.Searcher`
- Page: https://docs.unity3d.com/Packages/com.unity.searcher@4.9/api/UnityEditor.Searcher.ISearcherAdapter.html
- Properties:
- `AddAllChildResults`
- `HasDetailsPanel`
- `InitialSplitterDetailRatio`
- `MultiSelectEnabled`
- `Title`
- Methods:
- `Bind(VisualElement, SearcherItem, ItemExpanderState, string)`
- `InitDetailsPanel(VisualElement)`
- `MakeItem()`
- `OnSearchResultsFilter(IEnumerable<SearcherItem>, string)`
- `OnSelectionChanged(IEnumerable<SearcherItem>)`
### `ItemExpanderState` (Enum)
- Namespace: `UnityEditor.Searcher`
- Page: https://docs.unity3d.com/Packages/com.unity.searcher@4.9/api/UnityEditor.Searcher.ItemExpanderState.html

## Related Packages
- [[com.unity.shadergraph]]: shared signals #unity/testing #unity/tooling #unity/dependency-graph
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.render-pipelines.core]]: shared signals #unity/testing #unity/tooling
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.searcher@4.9/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.searcher@4.9/api/index.html
- Package index: [[Unity Package Docs Index]]
