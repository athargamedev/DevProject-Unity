# About the Multiplayer Center window

Package: `com.unity.multiplayer.center`
Manifest version: `1.0.1`
Lock version: `1.0.1`
Docs lookup version: `1.0.1`
Docs stream: `1.0`
Resolved docs version: `1.0.0-pre.3`
Source: `builtin`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-multiplayer-center #unity/editor/6000-4 #unity/multiplayer #unity/tooling #unity/networking #unity/gameplay

## Summary
The Multiplayer Center window generates a customized list of Unity packages and services for the type of multiplayer game you want to create. It also gives you access to samples and tutorials to help you use them in the Quickstart tab.

## Package Graph
### Depends On
- [[com.unity.modules.uielements]] `1.0.0`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Introduction

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `Unity.Multiplayer.Center.Common`: 13 documented types
- `Unity.Multiplayer.Center.Common.Analytics`: 2 documented types
- `Unity.Multiplayer.Center.Onboarding`: 1 documented types

## API Type Index
- `AnswerData` in `Unity.Multiplayer.Center.Common`
- `AnsweredQuestion` in `Unity.Multiplayer.Center.Common`
- `DisplayCondition` in `Unity.Multiplayer.Center.Common`
- `IOnboardingSection` in `Unity.Multiplayer.Center.Common`
- `ISectionDependingOnUserChoices` in `Unity.Multiplayer.Center.Common`
- `ISectionWithAnalytics` in `Unity.Multiplayer.Center.Common`
- `InfrastructureDependency` in `Unity.Multiplayer.Center.Common`
- `OnboardingSectionAttribute` in `Unity.Multiplayer.Center.Common`
- `OnboardingSectionCategory` in `Unity.Multiplayer.Center.Common`
- `Preset` in `Unity.Multiplayer.Center.Common`
- `SelectedSolutionsData` in `Unity.Multiplayer.Center.Common`
- `SelectedSolutionsData.HostingModel` in `Unity.Multiplayer.Center.Common`
- `SelectedSolutionsData.NetcodeSolution` in `Unity.Multiplayer.Center.Common`
- `IOnboardingSectionAnalyticsProvider` in `Unity.Multiplayer.Center.Common.Analytics`
- `InteractionDataType` in `Unity.Multiplayer.Center.Common.Analytics`
- `StyleConstants` in `Unity.Multiplayer.Center.Onboarding`

## API Type Details
### `AnswerData` (Class)
- Summary: Stores what the user answered in the GameSpecs questionnaire. The Preset is not included here.
- Page: https://docs.unity3d.com/Packages/com.unity.multiplayer.center@1.0/api/Unity.Multiplayer.Center.Common.AnswerData.html
- Fields:
- `Answers`: The list of answers the user has given so far.
- Methods:
- `Clone()`: Makes a deep copy of the object.
### `AnsweredQuestion` (Class)
- Summary: Answer to a single game spec question.
- Page: https://docs.unity3d.com/Packages/com.unity.multiplayer.center@1.0/api/Unity.Multiplayer.Center.Common.AnsweredQuestion.html
- Fields:
- `Answers`: The answers selected by the user (most often, it contains only one element).
- `QuestionId`: The question identifier as defined in the game spec questionnaire.
### `DisplayCondition` (Enum)
- Summary: A condition for a section to be displayed.
- Page: https://docs.unity3d.com/Packages/com.unity.multiplayer.center@1.0/api/Unity.Multiplayer.Center.Common.DisplayCondition.html

## Related Packages
- [[com.unity.multiplayer.playmode]]: shared signals #unity/gameplay #unity/multiplayer #unity/networking
- [[com.unity.dedicated-server]]: shared signals #unity/gameplay #unity/multiplayer #unity/networking
- [[com.unity.multiplayer.tools]]: shared signals #unity/multiplayer #unity/networking #unity/tooling
- [[com.unity.netcode.gameobjects]]: shared signals #unity/multiplayer #unity/networking
- [[com.unity.recorder]]: shared signals #unity/gameplay #unity/tooling

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.multiplayer.center@1.0/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.multiplayer.center@1.0/api/index.html
- Package index: [[Unity Package Docs Index]]
