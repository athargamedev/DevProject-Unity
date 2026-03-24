# com.unity.toolchain.linux-x86_64-linux

Package: `com.unity.toolchain.linux-x86_64-linux`
Manifest version: `1.0.2`
Lock version: `1.0.2`
Docs lookup version: `1.0.2`
Docs stream: `1.0`
Resolved docs version: `1.0.2`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-toolchain-linux-x86-64-linux #unity/editor/6000-4 #unity/platform #unity/tooling

## Summary
The com.unity.toolchain.linux-x86_64-linux package provides a toolchain for building IL2CPP players targeting both Linux and Embedded Linux platforms on a Linux 64-bit(x86_64) host. It depends on the following package:

## Package Graph
### Depends On
- [[com.unity.sysroot.base]] `1.0.2`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Introduction

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.Il2Cpp`: 1 documented types

## API Type Index
- `ToolchainLinuxX86_64` in `UnityEditor.Il2Cpp`

## API Type Details
### `ToolchainLinuxX86_64` (Class)
- Namespace: `UnityEditor.Il2Cpp`
- Summary: Toolchain package implementation for building Linux players on a Linux x86_64 host.
- Page: https://docs.unity3d.com/Packages/com.unity.toolchain.linux-x86_64-linux@1.0/api/UnityEditor.Il2Cpp.ToolchainLinuxX86_64.html
- Constructors:
- `ToolchainLinuxX86_64()`: Initializes the package and registers its toolchain payload so it can be resolved on disk.
- Properties:
- `HostArch`: Host CPU architecture this toolchain runs on.
- `HostPlatform`: Host operating system this toolchain runs on.
- `Name`: Human-readable package name as exposed to callers.
- `TargetPlatform`: Target operating system this toolchain is intended to produce binaries for.
- Methods:
- `GetIl2CppCompilerFlags()`: Additional compiler flags to pass to IL2CPP/Clang (none required by this package).
- `GetIl2CppLinkerFlags()`: Additional linker flags to ensure IL2CPP uses this package's linker.
- `GetSysrootPath()`: Absolute path to a sysroot for IL2CPP (not applicable for this toolchain-only package).
- `GetToolchainPath()`: Absolute path to the installed toolchain that IL2CPP should use.
- `PathToPayload()`: Gets the absolute path to the installed toolchain payload root directory.

## Related Packages
- [[com.unity.multiplayer.tools]]: shared signals #unity/platform #unity/tooling
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/platform #unity/tooling
- [[com.unity.sdk.linux-x86_64]]: shared signals #unity/platform #unity/tooling
- [[com.unity.web.stripping-tool]]: shared signals #unity/platform #unity/tooling
- [[com.unity.bindings.openimageio]]: shared signals #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.toolchain.linux-x86_64-linux@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.toolchain.linux-x86_64-linux@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
