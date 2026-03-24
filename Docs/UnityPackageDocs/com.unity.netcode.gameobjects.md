# Netcode for GameObjects

Package: `com.unity.netcode.gameobjects`
Manifest version: `2.9.2`
Lock version: `file:com.unity.netcode.gameobjects`
Docs lookup version: `2.9.2`
Docs stream: `2.9`
Resolved docs version: `2.9.2`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-netcode-gameobjects #unity/editor/6000-4 #unity/netcode #unity/multiplayer #unity/networking #unity/transport #unity/server #unity/platform

## Summary
Netcode for GameObjects is a high-level networking library built for Unity for you to abstract networking logic. You can send GameObjects and world data across a networking session to many players at once. With Netcode for GameObjects, you can focus on building your game instead of low-level protocols and networking frameworks.

## Package Graph
### Depends On
- [[com.unity.nuget.mono-cecil]] `1.11.4`
- [[com.unity.transport]] `2.6.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Netcode for GameObjects package
- Install
- Get started
- Client-server quickstart
- Distributed authority quickstart
- Distributed authority general quickstart
- Distributed authority WebGL quickstart
- Networking concepts
- Authority
- Ownership
- Network topologies
- Client-server
- Listen server host architecture
- Distributed authority topologies
- Configuration
- Configuring connections
- Connection approval
- Max players

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Netcode`: 152 documented types
- `Unity.Netcode.Components`: 25 documented types
- `Unity.Netcode.TestHelpers.Runtime`: 23 documented types
- `Unity.Netcode.Editor`: 13 documented types
- `Unity.Netcode.Transports.UTP`: 8 documented types
- `Unity.Netcode.Editor.Configuration`: 2 documented types
- `Unity.Netcode.RuntimeTests`: 1 documented types
- `Unity.Netcode.Transports.SinglePlayer`: 1 documented types

## API Type Index
- `AnticipatedNetworkVariable` in `Unity.Netcode`
- `AnticipatedNetworkVariable .OnAuthoritativeValueChangedDelegate` in `Unity.Netcode`
- `AnticipatedNetworkVariable .SmoothDelegate` in `Unity.Netcode`
- `Arithmetic` in `Unity.Netcode`
- `BaseRpcTarget` in `Unity.Netcode`
- `BitCounter` in `Unity.Netcode`
- `BitReader` in `Unity.Netcode`
- `BitWriter` in `Unity.Netcode`
- `BufferSerializer` in `Unity.Netcode`
- `BufferedLinearInterpolatorFloat` in `Unity.Netcode`
- `BufferedLinearInterpolatorQuaternion` in `Unity.Netcode`
- `BufferedLinearInterpolatorVector3` in `Unity.Netcode`
- `BufferedLinearInterpolator` in `Unity.Netcode`
- `BufferedLinearInterpolator .BufferedItem` in `Unity.Netcode`
- `BytePacker` in `Unity.Netcode`
- `ByteUnpacker` in `Unity.Netcode`

## API Type Details
### `AnticipatedNetworkVariable<T>` (Class)
- Namespace: `Unity.Netcode`
- Summary: A variable that can be synchronized over the network. This version supports basic client anticipation - the client can set a value on the belief that the server will update it to reflect the same value in a future…
- Page: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.9/api/Unity.Netcode.AnticipatedNetworkVariable-1.html
- Constructors:
- `AnticipatedNetworkVariable(T, StaleDataHandling)`: Initializes a new instance of the AnticipatedNetworkVariable class
- Fields:
- `OnAuthoritativeValueChanged`: Invoked any time the authoritative value changes, even when the data is stale or has been changed locally.
- `StaleDataHandling`: Controls how this network variable handles authoritative updates that are older than the current anticipated state
- Properties:
- `AuthoritativeValue`: Retrieves or sets the underlying authoritative value. Note that only a client or server with write permissions to this variable may set…
- `PreviousAnticipatedValue`: Holds the most recent anticipated value, whatever was most recently set using Anticipate(T) . Unlike Value , this does not get overwritten…
- `ShouldReanticipate`: Indicates whether this variable currently needs reanticipation. If this is true, the anticipated value has been overwritten by the…
- `Value`: Retrieves the current value for the variable. This is the "display value" for this variable, and is affected by Anticipate(T) and…
- Methods:
- `Anticipate(T)`: Sets the current value of the variable on the expectation that the authority will set the variable to the same value within one network…
- `Dispose()`: Virtual IDisposable implementation
- `ExceedsDirtinessThreshold()`: Checks if the current value has changed enough from its last synchronized value to warrant a new network update
- `~AnticipatedNetworkVariable()`: Finalizer that ensures proper cleanup of network variable resources
- `IsDirty()`: Gets Whether or not the container is dirty
- Events:
- `CheckExceedsDirtinessThreshold`: Determines if the difference between the last serialized value and the current value is large enough to serialize it again.
### `AnticipatedNetworkVariable<T>.OnAuthoritativeValueChangedDelegate` (Delegate)
- Namespace: `Unity.Netcode`
- Summary: Delegate for handling changes in the authoritative value
- Page: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.9/api/Unity.Netcode.AnticipatedNetworkVariable-1.OnAuthoritativeValueChangedDelegate.html
### `Arithmetic` (Class)
- Namespace: `Unity.Netcode`
- Summary: Arithmetic helper class
- Page: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.9/api/Unity.Netcode.Arithmetic.html
- Methods:
- `VarIntSize(ulong)`: Gets the output size in bytes after VarInting a unsigned integer
- `ZigZagDecode(ulong)`: Decides a ZigZag encoded integer back to a signed integer
- `ZigZagEncode(long)`: ZigZag encodes a signed integer and maps it to a unsigned integer

## Related Packages
- [[com.unity.transport]]: shared signals #unity/multiplayer #unity/netcode #unity/networking #unity/dependency-graph
- [[com.unity.dedicated-server]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.multiplayer.tools]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.multiplayer.center]]: shared signals #unity/multiplayer #unity/networking
- [[com.unity.multiplayer.playmode]]: shared signals #unity/multiplayer #unity/networking

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.9/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.9/api/index.html
- Package index: [[Unity Package Docs Index]]
