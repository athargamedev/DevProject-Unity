# Platform Toolkit package

Package: `com.unity.platformtoolkit`
Manifest version: `1.0.1`
Lock version: `1.0.1`
Docs lookup version: `1.0.1`
Docs stream: `1.0`
Resolved docs version: `1.0.1`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-platformtoolkit #unity/editor/6000-4 #unity/gameplay #unity/input

## Summary
The Platform Toolkit package provides a single API with support for the following common platform systems and services: By providing a single API, the package simplifies the integration of these common services into your application without rewriting your code for each platform. This allows you to target multiple…

## Package Graph
### Depends On
- [[com.unity.modules.jsonserialize]] `1.0.0`
- [[com.unity.sharp-zip-lib]] `1.4.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Platform Toolkit package
- Install and upgrade
- Install the Platform Toolkit package
- Install platform modules and packages
- Set up a project with the Platform Toolkit package
- Manage accounts
- Introducing the Platform Toolkit account system
- Retrieve account information
- Handle platform account systems
- Manage user identity and input
- Get started with attributes
- Manage achievements
- Introduction to achievements
- Configure achievements
- Unlock achievements
- Import achievement data
- Achievement Editor settings reference
- Save systems

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.PlatformToolkit`: 22 documented types
- `Unity.PlatformToolkit.Editor`: 6 documented types

## API Type Index
- `AccountState` in `Unity.PlatformToolkit`
- `CorruptedSaveException` in `Unity.PlatformToolkit`
- `DataStore` in `Unity.PlatformToolkit`
- `IAccount` in `Unity.PlatformToolkit`
- `IAccountPickerSystem` in `Unity.PlatformToolkit`
- `IAccountSystem` in `Unity.PlatformToolkit`
- `IAchievementSystem` in `Unity.PlatformToolkit`
- `ICapabilities` in `Unity.PlatformToolkit`
- `IInputDevice` in `Unity.PlatformToolkit`
- `IInputOwnershipSystem` in `Unity.PlatformToolkit`
- `IPlatformToolkit` in `Unity.PlatformToolkit`
- `IPrimaryAccountSystem` in `Unity.PlatformToolkit`
- `ISaveReadable` in `Unity.PlatformToolkit`
- `ISaveWritable` in `Unity.PlatformToolkit`
- `ISavingSystem` in `Unity.PlatformToolkit`
- `InvalidAccountException` in `Unity.PlatformToolkit`

## API Type Details
### `AccountState` (Enum)
- Namespace: `Unity.PlatformToolkit`
- Summary: Indicates if an IAccount object is signed in or signed out.
- Page: https://docs.unity3d.com/Packages/com.unity.platformtoolkit@1.0/api/Unity.PlatformToolkit.AccountState.html
### `CorruptedSaveException` (Class)
- Namespace: `Unity.PlatformToolkit`
- Summary: Thrown when a corrupted save has been found.
- Page: https://docs.unity3d.com/Packages/com.unity.platformtoolkit@1.0/api/Unity.PlatformToolkit.CorruptedSaveException.html
- Constructors:
- `CorruptedSaveException()`: Construct a CorruptedSaveException with no message.
- `CorruptedSaveException(string)`: Construct a CorruptedSaveException with a message.
### `DataStore` (Class)
- Namespace: `Unity.PlatformToolkit`
- Summary: DataStore stores string, float, and integer values that can be easily saved and loaded using the ISavingSystem .
- Page: https://docs.unity3d.com/Packages/com.unity.platformtoolkit@1.0/api/Unity.PlatformToolkit.DataStore.html
- Methods:
- `Create()`: Creates an empty DataStore object.
- `DeleteAll()`: Deletes all keys and values from the DataStore save.
- `DeleteKey(string)`: Deletes the specified key and value.
- `GetFloat(string)`: Retrieves a float value.
- `GetFloat(string, float)`: Retrieves a float value.

## Related Packages
- [[com.unity.addressables]]: shared signals #unity/gameplay #unity/input
- [[com.unity.cinemachine]]: shared signals #unity/gameplay #unity/input
- [[com.unity.inputsystem]]: shared signals #unity/gameplay #unity/input
- [[com.unity.recorder]]: shared signals #unity/gameplay #unity/input
- [[com.unity.ugui]]: shared signals #unity/gameplay #unity/input

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.platformtoolkit@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.platformtoolkit@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
