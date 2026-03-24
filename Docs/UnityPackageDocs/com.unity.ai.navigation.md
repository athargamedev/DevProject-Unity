# AI Navigation

Package: `com.unity.ai.navigation`
Manifest version: `2.0.12`
Lock version: `2.0.12`
Docs lookup version: `2.0.12`
Docs stream: `2.0`
Resolved docs version: `2.0.12`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-ai-navigation #unity/editor/6000-4 #unity/navigation #unity/ai

## Summary
The navigation system allows you to create characters that can intelligently move around the game world. These characters use navigation meshes that are created automatically from your Scene geometry. Dynamic obstacles allow you to alter the navigation of the characters at runtime, while NavMesh links let you build specific actions like opening doors or…

## Package Graph
### Depends On
- [[com.unity.modules.ai]] `1.0.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Navigation and Pathfinding
- What's new
- Install and Upgrade
- Install AI Navigation
- Upgrade
- Navigation System in Unity
- Inner Workings of the Navigation System
- About Agents
- About Obstacles
- Navigation Areas and Costs
- Navigation overview
- Create a NavMesh
- Create a NavMesh agent
- Create a NavMesh obstacle
- Create a NavMesh link
- Use NavMesh Agents with other components
- Build a HeightMesh for Accurate Character Placement
- Advanced navigation how-tos

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.AI.Navigation`: 5 documented types
- `Unity.AI.Navigation.Editor`: 2 documented types

## API Type Index
- `CollectObjects` in `Unity.AI.Navigation`
- `NavMeshLink` in `Unity.AI.Navigation`
- `NavMeshModifier` in `Unity.AI.Navigation`
- `NavMeshModifierVolume` in `Unity.AI.Navigation`
- `NavMeshSurface` in `Unity.AI.Navigation`
- `NavMeshAssetManager` in `Unity.AI.Navigation.Editor`
- `NavMeshComponentsGUIUtility` in `Unity.AI.Navigation.Editor`

## API Type Details
### `CollectObjects` (Enum)
- Namespace: `Unity.AI.Navigation`
- Summary: Sets the method for filtering the objects retrieved when baking the NavMesh.
- Page: https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/api/Unity.AI.Navigation.CollectObjects.html
### `NavMeshLink` (Class)
- Namespace: `Unity.AI.Navigation`
- Summary: Component used to create a navigable link between two NavMesh locations.
- Page: https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/api/Unity.AI.Navigation.NavMeshLink.html
- Properties:
- `activated`: Gets or sets whether the link can be traversed by agents.
- `agentTypeID`: Gets or sets the type of agent that can use the link.
- `area`: The area type of the link.
- `autoUpdate`: Gets or sets whether the world positions of the link's edges update whenever the GameObject transform, the startTransform or the…
- `autoUpdatePositions`: Gets or sets whether the world positions of the link's edges update whenever the GameObject transform, the start transform or the end…
- Methods:
- `UpdateLink()`: Replaces the link with a new one using the current settings.
- `UpdatePositions()`: Replaces the link with a new one using the current settings.
### `NavMeshModifier` (Class)
- Namespace: `Unity.AI.Navigation`
- Summary: Component that modifies the properties of the GameObjects used for building a NavMesh.
- Page: https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/api/Unity.AI.Navigation.NavMeshModifier.html
- Properties:
- `activeModifiers`: Gets the list of all the NavMeshModifier components that are currently active in the scene. Copies the returned list from an internal…
- `applyToChildren`: Gets or sets whether this GameObject's children also use the modifier settings.
- `area`: Gets or sets the area type applied by this GameObject.
- `generateLinks`: Gets or sets whether this object is included in the link generation process.
- `ignoreFromBuild`: Gets or sets whether the NavMesh building process ignores this GameObject and its children.
- Methods:
- `AffectsAgentType(int)`: Verifies whether this modifier can affect in any way the generation of a NavMesh for a given agent type.

## Related Packages
- No strong related-package links were inferred.

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/api/index.html
- Package index: [[Unity Package Docs Index]]
