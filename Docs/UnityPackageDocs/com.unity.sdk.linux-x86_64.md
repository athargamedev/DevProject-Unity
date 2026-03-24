# com.unity.sdk.linux-x86_64

Package: `com.unity.sdk.linux-x86_64`
Manifest version: `1.0.2`
Lock version: `1.0.2`
Docs lookup version: `1.0.2`
Docs stream: `1.0`
Resolved docs version: `1.0.2`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-sdk-linux-x86-64 #unity/editor/6000-4 #unity/platform #unity/tooling

## Summary
This package provides the required 64-bit(x86_64) sysroot for building IL2CPP-based Unity players targeting both Linux and Embedded Linux platforms. It contains the system headers and libraries needed to compile and run Unity players, and is intended to be used in conjunction with a matching Unity-provided toolchain package on the host platform.

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
- `SysrootLinuxX86_64` in `UnityEditor.Il2Cpp`

## API Type Details
### `SysrootLinuxX86_64` (Class)
- Namespace: `UnityEditor.Il2Cpp`
- Summary: Sysroot package implementation for building Linux (x86_64) IL2CPP players.
- Page: https://docs.unity3d.com/Packages/com.unity.sdk.linux-x86_64@1.0/api/UnityEditor.Il2Cpp.SysrootLinuxX86_64.html
- Constructors:
- `SysrootLinuxX86_64()`: Initializes the package and registers its payload so it can be resolved on disk.
- Properties:
- `Name`: Human-readable package name as exposed to callers.
- `TargetArch`: The CPU architecture this sysroot targets.
- `TargetPlatform`: The OS this sysroot targets.
- Methods:
- `GetIl2CppCompilerFlags()`: Compiler flags that IL2CPP (Clang) must receive to target this sysroot.
- `GetIl2CppLinkerFlags()`: Linker flags that IL2CPP must receive to target this sysroot.
- `GetSysrootPath()`: Absolute path to the sysroot that IL2CPP should compile/link against (not applicable for this toolchain-only package).
- `GetToolchainPath()`: Absolute path to a toolchain payload (not applicable for this sysroot-only package).
- `PathToPayload()`: Gets the absolute path to the installed payload root directory.

## Related Packages
- [[com.unity.multiplayer.tools]]: shared signals #unity/platform #unity/tooling
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/platform #unity/tooling
- [[com.unity.toolchain.linux-x86_64-linux]]: shared signals #unity/platform #unity/tooling
- [[com.unity.web.stripping-tool]]: shared signals #unity/platform #unity/tooling
- [[com.unity.bindings.openimageio]]: shared signals #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.sdk.linux-x86_64@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.sdk.linux-x86_64@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
