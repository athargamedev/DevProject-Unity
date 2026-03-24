# Unity IL2CPP Build Support for Linux

Package: `com.unity.sysroot.base`
Manifest version: `Not listed`
Lock version: `1.0.2`
Docs lookup version: `1.0.2`
Docs stream: `1.0`
Resolved docs version: `1.0.2`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-sysroot-base #unity/editor/6000-4

## Summary
Support for Linux players using IL2CPP is available from 2019.4 onwards. Operating systems (OS) have their own build systems which vary from one another. If you build using the headers and libraries on a particular OS, this might result in the built player not running on a different one. To address this, Unity…

## Package Graph
### Depends On
- None listed in packages-lock.json

### Required By
- [[com.unity.sdk.linux-x86_64]]
- [[com.unity.toolchain.linux-x86_64-linux]]

## Manual Map
- Introduction

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `NiceIO.Sysroot`: 2 documented types
- `UnityEditor.Il2Cpp`: 2 documented types

## API Type Index
- `DeleteMode` in `NiceIO.Sysroot`
- `SlashMode` in `NiceIO.Sysroot`
- `PayloadDescriptor` in `UnityEditor.Il2Cpp`
- `SysrootPackage` in `UnityEditor.Il2Cpp`

## API Type Details
### `DeleteMode` (Enum)
- Namespace: `NiceIO.Sysroot`
- Summary: Specifies the way that directory deletion should be performed.
- Page: https://docs.unity3d.com/Packages/com.unity.sysroot.base@1.0/api/NiceIO.Sysroot.DeleteMode.html
### `SlashMode` (Enum)
- Namespace: `NiceIO.Sysroot`
- Summary: Describes the different kinds of path separators that can be used when converting NPaths back into strings.
- Page: https://docs.unity3d.com/Packages/com.unity.sysroot.base@1.0/api/NiceIO.Sysroot.SlashMode.html
### `PayloadDescriptor` (Struct)
- Namespace: `UnityEditor.Il2Cpp`
- Summary: Describes where a sysroot/toolchain payload is located and where it should be installed.
- Page: https://docs.unity3d.com/Packages/com.unity.sysroot.base@1.0/api/UnityEditor.Il2Cpp.PayloadDescriptor.html

## Related Packages
- [[com.unity.sdk.linux-x86_64]]: shared signals #unity/dependency-graph
- [[com.unity.toolchain.linux-x86_64-linux]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.sysroot.base@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.sysroot.base@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
