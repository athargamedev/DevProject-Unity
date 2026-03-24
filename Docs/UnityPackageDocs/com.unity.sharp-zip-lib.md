# com.unity.sharp-zip-lib

Package: `com.unity.sharp-zip-lib`
Manifest version: `Not listed`
Lock version: `1.4.1`
Docs lookup version: `1.4.1`
Docs stream: `1.4`
Resolved docs version: `1.4.1`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-sharp-zip-lib #unity/editor/6000-4 #unity/testing #unity/tooling

## Summary
com.unity.sharp-zip-lib is a package that wraps SharpZipLib to be used inside Unity, and provides various compression/uncompression utility functions.

## Package Graph
### Depends On
- None listed in packages-lock.json

### Required By
- [[com.unity.platformtoolkit]]

## Manual Map
- SharpZipLib
- Installation

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.SharpZipLib.Utils`: 1 documented types
- `Unity.SharpZipLib.Utils.EditorTests`: 1 documented types
- `Unity.SharpZipLib.Utils.Tests`: 1 documented types

## API Type Index
- `ZipUtility` in `Unity.SharpZipLib.Utils`
- `ZipUtilityEditorTests` in `Unity.SharpZipLib.Utils.EditorTests`
- `ZipUtilityTests` in `Unity.SharpZipLib.Utils.Tests`

## API Type Details
### `ZipUtility` (Class)
- Namespace: `Unity.SharpZipLib.Utils`
- Summary: Provides utility methods for compressing and decompressing zip files.
- Page: https://docs.unity3d.com/Packages/com.unity.sharp-zip-lib@1.4/api/Unity.SharpZipLib.Utils.ZipUtility.html
- Methods:
- `CompressFolderToZip(string, string, string)`: Creates a zip file on disk containing the contents of the nominated folder.
- `UncompressFromZip(string, string, string)`: Uncompress the contents of a zip file into the specified folder.
### `ZipUtilityEditorTests` (Class)
- Namespace: `Unity.SharpZipLib.Utils.EditorTests`
- Page: https://docs.unity3d.com/Packages/com.unity.sharp-zip-lib@1.4/api/Unity.SharpZipLib.Utils.EditorTests.ZipUtilityEditorTests.html
- Methods:
- `CompressAndDecompress()`
### `ZipUtilityTests` (Class)
- Namespace: `Unity.SharpZipLib.Utils.Tests`
- Page: https://docs.unity3d.com/Packages/com.unity.sharp-zip-lib@1.4/api/Unity.SharpZipLib.Utils.Tests.ZipUtilityTests.html
- Methods:
- `CompressAndUncompressFolder()`
- `CompressTemporaryCacheToPersistentData()`

## Related Packages
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.render-pipelines.core]]: shared signals #unity/testing #unity/tooling
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/testing #unity/tooling
- [[com.unity.searcher]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.sharp-zip-lib@1.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.sharp-zip-lib@1.4/api/index.html
- Package index: [[Unity Package Docs Index]]
