# Introduction to Unity Serialization

Package: `com.unity.serialization`
Manifest version: `Not listed`
Lock version: `3.1.5`
Docs lookup version: `3.1.5`
Docs stream: `3.1`
Resolved docs version: `3.1.5`
Source: `registry`
Depth: `2`
Discovered via: `packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-serialization #unity/editor/6000-4

## Summary
Unity Serialization is a general purpose serialization library written entirely in C#. It currently supports JSON and Binary formats. Serialization makes use of the Unity.Properties package to efficiently traverse data containers at runtime in order to serialize and deserialize data.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.13`
- [[com.unity.collections]] `2.4.2`

### Required By
- [[com.unity.entities]]

## Manual Map
- Introduction

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Serialization.Json`: 40 documented types
- `Unity.Serialization.Binary`: 10 documented types
- `Unity.Serialization`: 4 documented types
- `Unity.Serialization.Editor`: 2 documented types
- `Unity.Serialization.Json.Unsafe`: 1 documented types

## API Type Index
- `DontSerializeAttribute` in `Unity.Serialization`
- `FormerNameAttribute` in `Unity.Serialization`
- `ParseErrorException` in `Unity.Serialization`
- `SerializationException` in `Unity.Serialization`
- `BinaryDeserializationContext` in `Unity.Serialization.Binary`
- `BinarySerialization` in `Unity.Serialization.Binary`
- `BinarySerializationContext` in `Unity.Serialization.Binary`
- `BinarySerializationParameters` in `Unity.Serialization.Binary`
- `BinarySerializationState` in `Unity.Serialization.Binary`
- `IBinaryAdapter` in `Unity.Serialization.Binary`
- `IBinaryAdapter` in `Unity.Serialization.Binary`
- `IBinaryDeserializationContext` in `Unity.Serialization.Binary`
- `IBinarySerializationContext` in `Unity.Serialization.Binary`
- `IContravariantBinaryAdapter` in `Unity.Serialization.Binary`
- `SessionState` in `Unity.Serialization.Editor`
- `UserSettings` in `Unity.Serialization.Editor`

## API Type Details
### `DontSerializeAttribute` (Class)
- Namespace: `Unity.Serialization`
- Summary: Use this attribute to flag a field or property to be ignored during serialization. This class cannot be inherited.
- Page: https://docs.unity3d.com/Packages/com.unity.serialization@3.1/api/Unity.Serialization.DontSerializeAttribute.html
### `FormerNameAttribute` (Class)
- Namespace: `Unity.Serialization`
- Summary: Use this attribute to rename a struct, class, field or property without losing its serialized value.
- Page: https://docs.unity3d.com/Packages/com.unity.serialization@3.1/api/Unity.Serialization.FormerNameAttribute.html
- Constructors:
- `FormerNameAttribute(string)`: Initializes a new instance of FormerNameAttribute with the specified name.
- Properties:
- `OldName`: The previous name of the member or type.
- Methods:
- `TryGetCurrentTypeName(string, out string)`: Gets the current name based on the previous name.
### `ParseErrorException` (Class)
- Namespace: `Unity.Serialization`
- Summary: The exception that is thrown when trying to parse a value as an actual type.
- Page: https://docs.unity3d.com/Packages/com.unity.serialization@3.1/api/Unity.Serialization.ParseErrorException.html
- Constructors:
- `ParseErrorException(SerializationInfo, StreamingContext)`: Initializes a new instance of the ParseErrorException class with serialized data.
- `ParseErrorException(string)`: Initialized a new instance of the ParseErrorException class with a specified error message.
- `ParseErrorException(string, Exception)`: Initializes a new instance of the ParseErrorException class with a specified error message and a reference to the inner exception that is…

## Related Packages
- [[com.unity.burst]]: shared signals #unity/dependency-graph
- [[com.unity.collections]]: shared signals #unity/dependency-graph
- [[com.unity.entities]]: shared signals #unity/dependency-graph

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.serialization@3.1/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.serialization@3.1/api/index.html
- Package index: [[Unity Package Docs Index]]
