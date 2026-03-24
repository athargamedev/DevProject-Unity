# About Unity Transport

Package: `com.unity.transport`
Manifest version: `2.6.0`
Lock version: `file:com.unity.transport`
Docs lookup version: `2.6.0`
Docs stream: `2.6`
Resolved docs version: `2.6.0`
Source: `embedded`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-transport #unity/editor/6000-4 #unity/transport #unity/networking #unity/multiplayer #unity/netcode #unity/server #unity/platform

## Summary
The Unity Transport package ( com.unity.transport ) is a low-level networking library geared towards multiplayer games development. It is used as the backbone of both Unity Netcode solutions: Netcode for GameObjects and Netcode for Entities but it can also be used with a custom solution.

## Package Graph
### Depends On
- [[com.unity.burst]] `1.8.24`
- [[com.unity.collections]] `2.2.1`
- [[com.unity.mathematics]] `1.3.2`

### Required By
- [[com.unity.netcode.gameobjects]]

## Manual Map
- Installation
- Using sample projects
- Simple client and server
- Using pipelines
- Jobified client and server
- Encrypted communications
- Cross-play support
- WebGL support
- FAQ
- Migrating from 1.X

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Networking.Transport`: 57 documented types
- `Unity.Networking.Transport.Utilities`: 13 documented types
- `Unity.Networking.Transport.Relay`: 8 documented types
- `Unity.Networking.Transport.TLS`: 4 documented types
- `Unity.Networking.Transport.Error`: 2 documented types
- `Unity.Networking.Transport.Logging`: 2 documented types

## API Type Index
- `BaselibNetworkInterface` in `Unity.Networking.Transport`
- `BaselibNetworkParameter` in `Unity.Networking.Transport`
- `BaselibNetworkParameterExtensions` in `Unity.Networking.Transport`
- `CommonNetworkParametersExtensions` in `Unity.Networking.Transport`
- `FragmentationPipelineStage` in `Unity.Networking.Transport`
- `INetworkInterface` in `Unity.Networking.Transport`
- `INetworkParameter` in `Unity.Networking.Transport`
- `INetworkPipelineStage` in `Unity.Networking.Transport`
- `IPCNetworkInterface` in `Unity.Networking.Transport`
- `InboundRecvBuffer` in `Unity.Networking.Transport`
- `InboundSendBuffer` in `Unity.Networking.Transport`
- `ManagedNetworkInterfaceExtensions` in `Unity.Networking.Transport`
- `MultiNetworkDriver` in `Unity.Networking.Transport`
- `MultiNetworkDriver.Concurrent` in `Unity.Networking.Transport`
- `NetworkConfigParameter` in `Unity.Networking.Transport`
- `NetworkConnection` in `Unity.Networking.Transport`

## API Type Details
### `NetworkConnection` (Struct)
- Namespace: `Unity.Networking.Transport`
- Summary: Public representation of a connection. This is obtained by calling Accept(out NativeArray<byte>) (on servers) or Connect(NetworkEndpoint, NativeArray<byte>) (on clients) and acts as a handle to the communication…
- Page: https://docs.unity3d.com/Packages/com.unity.transport@2.6/api/Unity.Networking.Transport.NetworkConnection.html
- Properties:
- `IsCreated`: Whether the connection was correctly obtained from a call to Accept(out NativeArray<byte>) or Connect(NetworkEndpoint, NativeArray<byte>) .
- Methods:
- `Close(NetworkDriver)`: Close an active connection. Strictly identical to Disconnect(NetworkDriver) .
- `Disconnect(NetworkDriver)`: Close an active connection. Strictly identical to Close(NetworkDriver) .
- `Equals(object)`
- `Equals(NetworkConnection)`
- `GetHashCode()`
- Operators:
- `operator ==(NetworkConnection, NetworkConnection)`
- `operator !=(NetworkConnection, NetworkConnection)`
### `NetworkDriver` (Struct)
- Namespace: `Unity.Networking.Transport`
- Summary: The NetworkDriver is the main API with which users interact with the Unity Transport package. It can be thought of as a socket with extra features. Refer to the manual for examples of how to use this API.
- Page: https://docs.unity3d.com/Packages/com.unity.transport@2.6/api/Unity.Networking.Transport.NetworkDriver.html
- Constructors:
- `NetworkDriver(INetworkInterface)`: Use Create(NetworkSettings) to construct NetworkDriver instances.
- `NetworkDriver(INetworkInterface, NetworkSettings)`: Use Create(NetworkSettings) to construct NetworkDriver instances.
- Properties:
- `Bound`: Whether the driver has been bound to an endpoint with the Bind(NetworkEndpoint) method. Binding to an endpoint is a prerequisite to…
- `CurrentSettings`: Current settings used by the driver.
- `IsCreated`: Whether the driver is been correctly created.
- `Listening`: Whether the driver is listening for new connections (e.g. acting like a server). Use the Listen() method to start listening for new…
- `ReceiveErrorCode`: Error code raised by the last receive job, if any.
- Methods:
- `AbortSend(DataStreamWriter)`: Aborts a send started with BeginSend(NetworkPipeline, NetworkConnection, out DataStreamWriter, int) .
- `Accept()`: Accept any new incoming connections. Connections must be accepted before data can be sent on them. It's also the only way to obtain the…
- `Accept(out NativeArray<byte>)`: Accept any new incoming connections. Connections must be accepted before data can be sent on them. It's also the only way to obtain the…
- `BeginSend(NetworkConnection, out DataStreamWriter, int)`: Begin sending data on the given connection (default pipeline).
- `BeginSend(NetworkPipeline, NetworkConnection, out DataStreamWriter, int)`: Begin sending data on the given connection and pipeline.
### `NetworkEndpoint` (Struct)
- Namespace: `Unity.Networking.Transport`
- Summary: Representation of an endpoint on the network. Typically, this means an IP address and a port number, and the API provides means to make working with this kind of endpoint easier. Analoguous to a sockaddr structure in…
- Page: https://docs.unity3d.com/Packages/com.unity.transport@2.6/api/Unity.Networking.Transport.NetworkEndpoint.html
- Fields:
- `Transferrable`: Raw representation of the address. This value is only useful if implementing your own INetworkInterface and you need access to the…
- Properties:
- `Address`: String representation of the endpoint. Same as ToString() .
- `AnyIpv4`: Shortcut for the wildcard IPv4 address (0.0.0.0).
- `AnyIpv6`: Shortcut for the wildcard IPv6 address (::).
- `Family`: Get or set the family of the endpoint.
- `IsAny`: Whether the endpoint is for a wildcard address.
- Methods:
- `Equals(object)`
- `Equals(NetworkEndpoint)`
- `GetHashCode()`
- `GetRawAddressBytes()`: Get the raw representation of the endpoint's address. This is only useful for low-level code that must interface with native libraries,…
- `Parse(string, ushort, NetworkFamily)`: Parse the provided IP address and port. Prefer this method when parsing IP addresses and ports that are known to be good (e.g. hardcoded…
- Operators:
- `operator ==(NetworkEndpoint, NetworkEndpoint)`
- `operator !=(NetworkEndpoint, NetworkEndpoint)`

## Related Packages
- [[com.unity.netcode.gameobjects]]: shared signals #unity/multiplayer #unity/netcode #unity/networking #unity/dependency-graph
- [[com.unity.dedicated-server]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.multiplayer.tools]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.multiplayer.center]]: shared signals #unity/multiplayer #unity/networking
- [[com.unity.multiplayer.playmode]]: shared signals #unity/multiplayer #unity/networking

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.transport@2.6/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.transport@2.6/api/index.html
- Package index: [[Unity Package Docs Index]]
