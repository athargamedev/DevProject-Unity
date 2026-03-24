# Collections package

Package: `com.unity.collections`
Manifest version: `Not listed`
Lock version: `6.4.0`
Docs lookup version: `6.4.0`
Docs stream: `6.4`
Resolved docs version: `6.4.0`
Source: `builtin`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-collections #unity/editor/6000-4

## Summary
The Collections package provides unmanaged data structures that you can use in jobs and Burst-compiled code.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.23`
- [[com.unity.mathematics]] `1.3.2`
- [[com.unity.nuget.mono-cecil]] `1.11.5`
- [[com.unity.test-framework]] `1.4.6`
- [[com.unity.test-framework.performance]] `3.0.3`

### Required By
- [[com.unity.bindings.openimageio]]
- [[com.unity.entities]]
- [[com.unity.multiplayer.tools]]
- [[com.unity.physics]]
- [[com.unity.recorder]]
- [[com.unity.render-pipelines.core]]
- [[com.unity.serialization]]
- [[com.unity.transport]]

## Manual Map
- Collections package
- Known issues
- Collections overview
- Collection types
- Parallel readers and writers
- Use allocators to control unmanaged memory
- Allocator overview
- Aliasing allocators
- Rewindable allocator overview
- Custom allocator overview
- Use a custom allocator
- Performance comparisons
- Allocator benchmarks
- Allocator performance comparison
- Container performance comparison

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Collections`: 116 documented types
- `Unity.Collections.LowLevel.Unsafe`: 51 documented types
- `Unity.Jobs`: 11 documented types
- `Unity.Collections.LowLevel.Unsafe.NotBurstCompatible`: 1 documented types
- `Unity.Collections.NotBurstCompatible`: 1 documented types

## API Type Index
- `AllocatorHelper` in `Unity.Collections`
- `AllocatorManager` in `Unity.Collections`
- `AllocatorManager.AllocatorHandle` in `Unity.Collections`
- `AllocatorManager.Block` in `Unity.Collections`
- `AllocatorManager.IAllocator` in `Unity.Collections`
- `AllocatorManager.Range` in `Unity.Collections`
- `AllocatorManager.TryFunction` in `Unity.Collections`
- `BitField32` in `Unity.Collections`
- `BitField64` in `Unity.Collections`
- `BurstCompatibleAttribute` in `Unity.Collections`
- `CollectionHelper` in `Unity.Collections`
- `CollectionHelper.DummyJob` in `Unity.Collections`
- `ConversionError` in `Unity.Collections`
- `CopyError` in `Unity.Collections`
- `DataStreamReader` in `Unity.Collections`
- `DataStreamWriter` in `Unity.Collections`

## API Type Details
### `AllocatorHelper<T>` (Struct)
- Namespace: `Unity.Collections`
- Summary: Provides a wrapper for custom allocator.
- Page: https://docs.unity3d.com/Packages/com.unity.collections@6.4/api/Unity.Collections.AllocatorHelper-1.html
- Constructors:
- `AllocatorHelper(AllocatorHandle, bool, int)`: Allocate the custom allocator from backingAllocator and register it.
- Properties:
- `Allocator`: Get the custom allocator.
- Methods:
- `Dispose()`: Dispose the custom allocator backing memory and unregister it.
### `AllocatorManager` (Class)
- Namespace: `Unity.Collections`
- Summary: Manages custom memory allocators.
- Page: https://docs.unity3d.com/Packages/com.unity.collections@6.4/api/Unity.Collections.AllocatorManager.html
- Fields:
- `FirstUserIndex`: Index in the global function table of the first user-defined allocator.
- `Invalid`: Corresponds to Allocator.Invalid.
- `MaxNumCustomAllocators`: Maximum number of user-defined allocators.
- `None`: Corresponds to Allocator.None.
- `Persistent`: Corresponds to Allocator.Persistent.
- Methods:
- `Allocate(AllocatorHandle, int, int, int)`: Allocates memory from an allocator.
- `Allocate<T>(AllocatorHandle, int)`: Allocates enough memory for an unmanaged value of a given type.
- `Allocate<T>(ref T, int, int, int)`: Allocates memory directly from an allocator.
- `ConvertToAllocatorHandle(Allocator)`: Convert an Allocator to an AllocatorHandle, keeping the Version.
- `Free(AllocatorHandle, void*)`: Frees an allocation.
### `AllocatorManager.AllocatorHandle` (Struct)
- Namespace: `Unity.Collections`
- Summary: Represents the allocator function used within an allocator.
- Page: https://docs.unity3d.com/Packages/com.unity.collections@6.4/api/Unity.Collections.AllocatorManager.AllocatorHandle.html
- Fields:
- `Index`: This allocator's index into the global table of allocator functions.
- `InvalidChildAllocatorIndex`: For internal use only.
- `InvalidChildSafetyHandleIndex`: For internal use only.
- `Version`: This allocator's version number.
- Properties:
- `Handle`: This handle.
- `IsAutoDispose`: Check whether this allocator will automatically dispose allocations.
- `IsCustomAllocator`: Check whether this allocator is a custom allocator.
- `ToAllocator`: Retrieve the Allocator associated with this allocator handle.
- `Value`: The Index cast to int.
- Methods:
- `AllocateBlock<T>(int)`: Allocates a block with this allocator function.
- `CompareTo(AllocatorHandle)`: Compare this AllocatorManager.AllocatorHandle against a given one
- `Dispose()`: Dispose the allocator.
- `Equals(object)`: AllocatorManager.AllocatorHandle instances are equal if they refer to the same instance at the same version.
- `Equals(Allocator)`: AllocatorManager.AllocatorHandle instances are equal if they refer to the same instance at the same version.
- Operators:
- `operator ==(AllocatorHandle, AllocatorHandle)`: Evaluates if one AllocatorManager.AllocatorHandle is equal to the other.
- `operator >(AllocatorHandle, AllocatorHandle)`: Evaluates if one AllocatorManager.AllocatorHandle is greater than the other.
- `operator >=(AllocatorHandle, AllocatorHandle)`: Evaluates if one AllocatorManager.AllocatorHandle is greater than or equal to the other.
- `implicit operator AllocatorHandle(Allocator)`: Implicitly convert an Allocator to an AllocatorHandle with its Version being reset to 0.
- `operator !=(AllocatorHandle, AllocatorHandle)`: Evaluates if one AllocatorManager.AllocatorHandle is not equal to the other.

## Related Packages
- [[com.unity.bindings.openimageio]]: shared signals #unity/dependency-graph
- [[com.unity.burst]]: shared signals #unity/dependency-graph
- [[com.unity.entities]]: shared signals #unity/dependency-graph
- [[com.unity.mathematics]]: shared signals #unity/dependency-graph
- [[com.unity.multiplayer.tools]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.collections@6.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.collections@6.4/api/index.html
- Package index: [[Unity Package Docs Index]]
