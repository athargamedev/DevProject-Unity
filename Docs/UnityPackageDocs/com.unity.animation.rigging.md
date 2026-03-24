# Animation Rigging

Package: `com.unity.animation.rigging`
Manifest version: `1.4.1`
Lock version: `1.4.1`
Docs lookup version: `1.4.1`
Docs stream: `1.4`
Resolved docs version: `1.4.1`
Source: `registry`
Depth: `0`
Discovered via: `manifest.json, packages-lock.json`
Unity editor: `6000.4.0b11`
Tags: #unity/package #unity/upm #unity/package/com-unity-animation-rigging #unity/editor/6000-4 #unity/animation #unity/rigging

## Summary
Use Animation Rigging to create and organize animation rigs , or sets of constraints for adding procedural motion to animated objects. Examples include:

## Package Graph
### Depends On
- [[com.unity.burst]] `1.4.1`
- [[com.unity.modules.animation]] `1.0.0`
- [[com.unity.test-framework]] `1.1.24`

### Required By
- No dependents were discovered in packages-lock.json

## Manual Map
- Animation Rigging
- Rigging workflow
- Animation Rigging Menu
- Bidirectional Motion Transfer
- Constraint components
- Blend Constraint
- Chain IK Constraint
- Damped Transform
- Multi-Aim Constraint
- Multi-Parent Constraint
- Multi-Position Constraint
- Multi-Referential Constraint
- Multi-Rotation Constraint
- Override Transform
- Twist Chain Constraint
- Twist Correction
- Two Bone IK Constraint

## API Overview
This is the documentation for the Scripting APIs of this package.

The API landing page is available, but it does not expose overview tables on the index page.

## API Namespaces
- `UnityEngine.Animations.Rigging`: 116 documented types
- `UnityEditor.Animations.Rigging`: 29 documented types
- `DocCodeExamples`: 2 documented types

## API Type Index
- `CustomPlayableGraphEvaluator` in `DocCodeExamples`
- `CustomRigBuilderEvaluator` in `DocCodeExamples`
- `BakeParametersAttribute` in `UnityEditor.Animations.Rigging`
- `BakeParameters` in `UnityEditor.Animations.Rigging`
- `BakeUtils` in `UnityEditor.Animations.Rigging`
- `EditorCurveBindingUtils` in `UnityEditor.Animations.Rigging`
- `IBakeParameters` in `UnityEditor.Animations.Rigging`
- `InverseRigConstraintAttribute` in `UnityEditor.Animations.Rigging`
- `MultiAimInverseConstraint` in `UnityEditor.Animations.Rigging`
- `MultiAimInverseConstraintJob` in `UnityEditor.Animations.Rigging`
- `MultiAimInverseConstraintJobBinder` in `UnityEditor.Animations.Rigging`
- `MultiParentInverseConstraint` in `UnityEditor.Animations.Rigging`
- `MultiParentInverseConstraintJob` in `UnityEditor.Animations.Rigging`
- `MultiParentInverseConstraintJobBinder` in `UnityEditor.Animations.Rigging`
- `MultiPositionInverseConstraint` in `UnityEditor.Animations.Rigging`
- `MultiPositionInverseConstraintJob` in `UnityEditor.Animations.Rigging`

## API Type Details
### `CustomPlayableGraphEvaluator` (Class)
- Namespace: `DocCodeExamples`
- Summary: Custom evaluator that manually evaluates the PlayableGraph in LateUpdate.
- Page: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/api/DocCodeExamples.CustomPlayableGraphEvaluator.html
### `CustomRigBuilderEvaluator` (Class)
- Namespace: `DocCodeExamples`
- Summary: Custom Evaluator that manually evaluates the RigBuilder in LateUpdate.
- Page: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/api/DocCodeExamples.CustomRigBuilderEvaluator.html
### `BakeParametersAttribute` (Class)
- Namespace: `UnityEditor.Animations.Rigging`
- Summary: Attribute that can be placed on BakeParameters. The attribute is used to declare to which RigConstraint the BakeParameters belong.
- Page: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/api/UnityEditor.Animations.Rigging.BakeParametersAttribute.html
- Constructors:
- `BakeParametersAttribute(Type)`: Constructor.
- Properties:
- `constraintType`: The RigConstraint to which the BakeParameters belong.

## Related Packages
- [[com.unity.burst]]: shared signals #unity/dependency-graph
- [[com.unity.recorder]]: shared signals #unity/animation
- [[com.unity.test-framework]]: shared signals #unity/dependency-graph
- [[com.unity.timeline]]: shared signals #unity/animation
- [[com.unity.ugui]]: shared signals #unity/animation

## Official References
- Manual: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/index.html
- API: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/api/index.html
- Package index: [[Unity Package Docs Index]]
