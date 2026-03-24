# OpenImageIO Bindings package

Package: `com.unity.bindings.openimageio`
Manifest version: `Not listed`
Lock version: `1.0.2`
Docs lookup version: `1.0.2`
Docs stream: `1.0`
Resolved docs version: `1.0.2`
Source: `registry`
Depth: `1`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-bindings-openimageio #unity/editor/6000-4 #unity/recording #unity/tooling

## Summary
The OpenImageIO Bindings package provides access to a subset of the OpenImageIO C++ library scripts. The OpenImageIO Bindings package exposes only a subset of the full API. That subset enables IO operations for image files, as used in the Recorder package. Unity does not recommend to use the OpenImageIO Bindings…

## Package Graph
### Depends On
- [[com.unity.collections]] `1.2.4`

### Required By
- [[com.unity.recorder]]

## Manual Map
- Introduction

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEditor.Bindings.OpenImageIO`: 4 documented types

## API Type Index
- `OiioWrapper` in `UnityEditor.Bindings.OpenImageIO`
- `OiioWrapper.Attribute` in `UnityEditor.Bindings.OpenImageIO`
- `OiioWrapper.ImageHeader` in `UnityEditor.Bindings.OpenImageIO`
- `OiioWrapper.SubImagesList` in `UnityEditor.Bindings.OpenImageIO`

## API Type Details
### `OiioWrapper` (Class)
- Namespace: `UnityEditor.Bindings.OpenImageIO`
- Summary: Class containing bindings for IO operations using OpenImageIO library
- Page: https://docs.unity3d.com/Packages/com.unity.bindings.openimageio@1.0/api/UnityEditor.Bindings.OpenImageIO.OiioWrapper.html
- Methods:
- `FreeAllocatedMemory(SubImagesList)`: Frees the memory allocated for the SubImagesList passed.
- `IntPtrToManagedArray<T>(IntPtr, uint)`: Reads an IntPtr into an Array of structs of length arrayCount.
- `ReadImage(FixedString4096Bytes)`: Reads an image from its file name.
- `Tex2DFromImageHeader(ImageHeader)`: Reads an ImageHeader and returns it as a Texture2D.
- `WriteImage(FixedString4096Bytes, uint, ImageHeader*)`: Writes an image to the specified file name.
### `OiioWrapper.Attribute` (Struct)
- Namespace: `UnityEditor.Bindings.OpenImageIO`
- Summary: A struct (key value pair) used to encode metadata in an image.
- Page: https://docs.unity3d.com/Packages/com.unity.bindings.openimageio@1.0/api/UnityEditor.Bindings.OpenImageIO.OiioWrapper.Attribute.html
- Fields:
- `key`: Attribute key (its unique name as expected by OIIO)
- `value`: Attribute value
### `OiioWrapper.ImageHeader` (Struct)
- Namespace: `UnityEditor.Bindings.OpenImageIO`
- Summary: A struct representing an Image Header
- Page: https://docs.unity3d.com/Packages/com.unity.bindings.openimageio@1.0/api/UnityEditor.Bindings.OpenImageIO.OiioWrapper.ImageHeader.html
- Fields:
- `attributes`: Pointer to attributes data structure
- `attributesCount`: Number of attributes
- `channelsCount`: Number of channels in the image
- `data`: Pointer to image data structure
- `height`: Image height

## Related Packages
- [[com.unity.recorder]]: shared signals #unity/recording #unity/tooling #unity/dependency-graph
- [[com.unity.collections]]: shared signals #unity/dependency-graph
- [[com.unity.ext.nunit]]: shared signals #unity/tooling
- [[com.unity.ide.visualstudio]]: shared signals #unity/tooling
- [[com.unity.multiplayer.center]]: shared signals #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.bindings.openimageio@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.bindings.openimageio@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
