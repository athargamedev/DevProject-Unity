# Burst compiler

Package: `com.unity.burst`
Manifest version: `Not listed`
Lock version: `1.8.28`
Docs lookup version: `1.8.28`
Docs stream: `1.8`
Resolved docs version: `1.8.28`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-burst #unity/editor/6000-4

## Summary
Compile compatible sections of your C# code into highly-optimized native CPU code. Burst is a compiler that works on a subset of C# referred to in the Unity context as High-Performance C# (HPC#). Burst uses LLVM to translate .NET Intermediate Language (IL) to code that's optimized for performance on the target CPU…

## Package Graph
### Depends On
- [[com.unity.mathematics]] `1.2.1`
- [[com.unity.modules.jsonserialize]] `1.0.0`

### Required By
- [[com.unity.animation.rigging]]
- [[com.unity.collections]]
- [[com.unity.entities]]
- [[com.unity.multiplayer.tools]]
- [[com.unity.physics]]
- [[com.unity.render-pipelines.core]]
- [[com.unity.serialization]]
- [[com.unity.transport]]

## Manual Map
- Burst compiler
- Get started
- C# language support
- HPC# overview
- Static read-only fields and static constructor support
- String support
- Calling Burst-compiled code
- Function pointers
- C#/.NET type support
- C#/.NET System namespace support
- DllImport and internal calls
- SharedStatic struct
- Burst compilation
- Marking code for Burst compilation
- Excluding code from Burst compilation
- Defining Burst options for an assembly
- Burst compilation in Play mode
- Generic jobs

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Burst.Intrinsics`: 24 documented types
- `Unity.Burst`: 12 documented types
- `Unity.Burst.CompilerServices`: 6 documented types

## API Type Index
- `BurstCompileAttribute` in `Unity.Burst`
- `BurstCompiler` in `Unity.Burst`
- `BurstCompilerOptions` in `Unity.Burst`
- `BurstExecutionEnvironment` in `Unity.Burst`
- `BurstRuntime` in `Unity.Burst`
- `FloatMode` in `Unity.Burst`
- `FloatPrecision` in `Unity.Burst`
- `FunctionPointer` in `Unity.Burst`
- `IFunctionPointer` in `Unity.Burst`
- `NoAliasAttribute` in `Unity.Burst`
- `OptimizeFor` in `Unity.Burst`
- `SharedStatic` in `Unity.Burst`
- `Aliasing` in `Unity.Burst.CompilerServices`
- `AssumeRangeAttribute` in `Unity.Burst.CompilerServices`
- `Constant` in `Unity.Burst.CompilerServices`
- `Hint` in `Unity.Burst.CompilerServices`

## API Type Details
### `BurstCompileAttribute` (Class)
- Namespace: `Unity.Burst`
- Summary: This attribute is used to tag jobs or function-pointers as being Burst compiled, and optionally set compilation parameters.
- Page: https://docs.unity3d.com/Packages/com.unity.burst@1.8/api/Unity.Burst.BurstCompileAttribute.html
### `BurstCompiler` (Class)
- Namespace: `Unity.Burst`
- Summary: The burst compiler runtime frontend.
- Page: https://docs.unity3d.com/Packages/com.unity.burst@1.8/api/Unity.Burst.BurstCompiler.html
### `BurstCompilerOptions` (Class)
- Namespace: `Unity.Burst`
- Summary: Options available at Editor time and partially at runtime to control the behavior of the compilation and to enable/disable burst jobs.
- Page: https://docs.unity3d.com/Packages/com.unity.burst@1.8/api/Unity.Burst.BurstCompilerOptions.html

## Related Packages
- [[com.unity.animation.rigging]]: shared signals #unity/dependency-graph
- [[com.unity.collections]]: shared signals #unity/dependency-graph
- [[com.unity.entities]]: shared signals #unity/dependency-graph
- [[com.unity.mathematics]]: shared signals #unity/dependency-graph
- [[com.unity.multiplayer.tools]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.burst@1.8/api/index.html
- Package index: [[Unity Package Docs Index]]
