# Web Stripping Tool package

Package: `com.unity.web.stripping-tool`
Manifest version: `1.2.1`
Lock version: `1.2.1`
Docs lookup version: `1.2.1`
Docs stream: `1.2`
Resolved docs version: `1.2.1`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-web-stripping-tool #unity/editor/6000-4 #unity/platform #unity/tooling #unity/testing

## Summary
Unity’s Web Stripping Tool package allows you to optimize your Web application by reducing its build size. This package helps you analyze Unity engine code that's included in your build’s WebAssembly binary and remove any code that's not used. For example, if your application is a 2D game, you might use the tool to…

## Package Graph
### Depends On
- [[com.unity.nuget.newtonsoft-json]] `3.2.1`
- [[com.unity.settings-manager]] `2.0.1`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Web Stripping Tool package
- Install and upgrade
- Installation
- Upgrade guide
- Get started
- Understand submodules
- Identify unused submodules
- Submodule stripping workflow
- Configure submodule stripping
- Submodule profiling
- Strip submodules from a build
- Test the stripped build
- Optimize the stripped build
- Create multiple stripping settings
- Change the active stripping settings
- Backup and additional files
- C# scripts for submodule stripping
- Enable submodule stripping with scripting

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Web.Stripping.Editor`: 9 documented types

## API Type Index
- `MissingSubmoduleErrorHandlingType` in `Unity.Web.Stripping.Editor`
- `StrippingProjectSettings` in `Unity.Web.Stripping.Editor`
- `SubmoduleStrippingSettings` in `Unity.Web.Stripping.Editor`
- `WebBuildProcessor` in `Unity.Web.Stripping.Editor`
- `WebBuildReport` in `Unity.Web.Stripping.Editor`
- `WebBuildReportList` in `Unity.Web.Stripping.Editor`
- `WebBuildSettings` in `Unity.Web.Stripping.Editor`
- `WebPlayerSettings` in `Unity.Web.Stripping.Editor`
- `WebPlayerSettingsScope` in `Unity.Web.Stripping.Editor`

## API Type Details
### `MissingSubmoduleErrorHandlingType` (Enum)
- Namespace: `Unity.Web.Stripping.Editor`
- Summary: Controls what happens if a function of a stripped submodule is called.
- Page: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.2/api/Unity.Web.Stripping.Editor.MissingSubmoduleErrorHandlingType.html
### `StrippingProjectSettings` (Class)
- Namespace: `Unity.Web.Stripping.Editor`
- Summary: Holds the submodule stripping settings for the project. Creates a default submodule stripping settings asset for the project.
- Page: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.2/api/Unity.Web.Stripping.Editor.StrippingProjectSettings.html
- Properties:
- `ActiveSettings`: The active submodule stripping settings for the project.
- `StripAutomaticallyAfterBuild`: Enables a submodule stripping pass after a build has completed using the currently active settings, if they're set.
- Events:
- `SettingsChanged`: Raised when active settings change.
### `SubmoduleStrippingSettings` (Class)
- Namespace: `Unity.Web.Stripping.Editor`
- Summary: An asset for configuring submodule stripping settings.
- Page: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.2/api/Unity.Web.Stripping.Editor.SubmoduleStrippingSettings.html
- Fields:
- `MissingSubmoduleErrorHandling`: The error handling behavior when a stripped submodule is used.
- `OptimizeCodeAfterStripping`: Run code optimization to reduce final build size and improve performance. Increases the stripping time significantly. Use on release builds.
- `RootMenuName`: The root menu name used for various menu items.
- `SubmodulesToStrip`: The list of submodules to strip from a build.
- Properties:
- `RemoveDebugInformation`: Remove debug information after stripping. Debug symbols are required to identify functions during stripping but they increase the size of…
- Methods:
- `Create(string)`: Creates a settings asset.
- `Save()`: Save changes to the settings to disk.
- Events:
- `ValuesChanged`: Raised when the values of the settings are changed.

## Related Packages
- [[com.unity.scriptablebuildpipeline]]: shared signals #unity/platform #unity/testing #unity/tooling
- [[com.unity.ext.nunit]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.playmode]]: shared signals #unity/testing #unity/tooling
- [[com.unity.multiplayer.tools]]: shared signals #unity/platform #unity/tooling
- [[com.unity.render-pipelines.core]]: shared signals #unity/testing #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.2/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.web.stripping-tool@1.2/api/index.html
- Package index: [[Unity Package Docs Index]]
