# About the Dedicated Server package

Package: `com.unity.dedicated-server`
Manifest version: `2.0.2`
Lock version: `2.0.2`
Docs lookup version: `2.0.2`
Docs stream: `2.0`
Resolved docs version: `2.0.2`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-dedicated-server #unity/editor/6000-4 #unity/server #unity/platform #unity/multiplayer #unity/networking #unity/gameplay

## Summary
Use the Dedicated Server package when you use the Dedicated Server build target to switch a project between the server and client role without the need to create another project. To do this, use Multiplayer roles to distribute GameObjects and components accross the client and server.

## Package Graph
### Depends On
- None listed in packages-lock.json

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- About the Dedicated Server package
- Set default command line arguments
- Multiplayer roles
- Use a script to access the active multiplayer role
- Control which GameObjects and components exist on the client or the server
- Automatically remove components from a Multiplayer Role
- Identify null references
- Multiplayes roles icons
- Multiplayer roles reference

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Multiplayer`: 4 documented types
- `Unity.Multiplayer.Editor`: 3 documented types

## API Type Index
- `MultiplayerRole` in `Unity.Multiplayer`
- `MultiplayerRoleFlags` in `Unity.Multiplayer`
- `MultiplayerRoleRestrictedAttribute` in `Unity.Multiplayer`
- `MultiplayerRolesManager` in `Unity.Multiplayer`
- `EditorMultiplayerRolesManager` in `Unity.Multiplayer.Editor`
- `EditorMultiplayerRolesManager.AutomaticSelection` in `Unity.Multiplayer.Editor`
- `EditorMultiplayerRolesManager.AutomaticSelection.Server` in `Unity.Multiplayer.Editor`

## API Type Details
### `MultiplayerRole` (Enum)
- Namespace: `Unity.Multiplayer`
- Summary: The role of the application in a multiplayer game.
- Page: https://docs.unity3d.com/Packages/com.unity.dedicated-server@2.0/api/Unity.Multiplayer.MultiplayerRole.html
### `EditorMultiplayerRolesManager` (Class)
- Namespace: `Unity.Multiplayer.Editor`
- Summary: Provides an api for managing multiplayer roles in the editor.
- Page: https://docs.unity3d.com/Packages/com.unity.dedicated-server@2.0/api/Unity.Multiplayer.Editor.EditorMultiplayerRolesManager.html
- Properties:
- `ActiveMultiplayerRoleMask`: Gets or sets the active multiplayer role mask.
- `EnableMultiplayerRoles`: Enables multiplayer roles for the project.
- `EnableSafetyChecks`: Enables safety checks for multiplayer roles. When entering play mode or building scenes, the editor will check and warn about any stripped…
- Methods:
- `GetMultiplayerRoleForBuildProfile(BuildProfile)`: Gets the multiplayer role that is going to be used for the provided build profile.
- `GetMultiplayerRoleForBuildTarget(NamedBuildTarget)`: Gets the multiplayer role mask that is going to be used for the provided build target.
- `GetMultiplayerRoleForClassicTarget(BuildTarget)`: Gets the multiplayer role that is going to be used for the provided build target.
- `GetMultiplayerRoleForClassicTarget(BuildTarget, StandaloneBuildSubtarget)`: Gets the multiplayer role that is going to be used for the provided build target and subtarget.
- `GetMultiplayerRoleMaskForComponent(Component)`: Gets the multiplayer role mask for a Component.
- Events:
- `ActiveMultiplayerRoleChanged`: Event that is invoked when the active multiplayer role mask changes.
### `MultiplayerRoleFlags` (Enum)
- Namespace: `Unity.Multiplayer`
- Summary: Flags for the role of the application in a multiplayer game.
- Page: https://docs.unity3d.com/Packages/com.unity.dedicated-server@2.0/api/Unity.Multiplayer.MultiplayerRoleFlags.html

## Related Packages
- [[com.unity.multiplayer.tools]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.netcode.gameobjects]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.transport]]: shared signals #unity/multiplayer #unity/networking #unity/platform
- [[com.unity.multiplayer.center]]: shared signals #unity/gameplay #unity/multiplayer #unity/networking
- [[com.unity.multiplayer.playmode]]: shared signals #unity/gameplay #unity/multiplayer #unity/networking

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.dedicated-server@2.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.dedicated-server@2.0/api/index.html
- Package index: [[Unity Package Docs Index]]
