# Visual Effects and Particle Systems

Package/System: `com.unity.modules.particlesystem`
Root topic: https://docs.unity3d.com/Manual/visual-effects.html
Manual version: `Unity 6.3 LTS`
Tags: #unity/manual #unity/visual-effects #unity/particles #unity/force-fields #unity/rendering #unity/package/com-unity-modules-particlesystem

## Summary
This section provides information on the visual effects available in Unity.

## Content Map
- Particle systems: https://docs.unity3d.com/Manual/ParticleSystems.html
- Decals: https://docs.unity3d.com/Manual/visual-effects-decals.html
- Lens flares: https://docs.unity3d.com/Manual/visual-effects-lens-flares.html
- Light halos: https://docs.unity3d.com/Manual/visual-effects-haloes.html
- Line and trail effects: https://docs.unity3d.com/Manual/visual-effects-lines-trails-billboards.html
- Visual Effect Graph package reference: https://docs.unity3d.com/Manual/VFXGraph.html

## Detailed Subtopics
### Particle System Force Field component reference
- Page: https://docs.unity3d.com/Manual/class-ParticleSystemForceField.html
- Summary: The Particle A small, simple image or mesh that is emitted by a particle system. A particle system can display and move particles in great numbers to represent a fluid or amorphous entity. The effect of all the particles together creates the impression of the complete entity, such as smoke. More info See in Glossary…
- Key sections:
- Unity Manual
- Shape
- Gravity
- Rotation
- Drag
- Vector Field
- Selected properties:
- `Shape`: Select the shape of the area of influence.
- `Start Range`: Set the value for the inner point within the shape where the area of influence begins.
- `End Range`: Set the value for the outer point of the shape where the area of influence ends.
- `Direction X, Y and Z`: Set a linear force to apply to particles along the x-axis, y-axis and z-axis. The higher the value, the greater the force. You can specify a constant force A simple component for adding a constant…
- `Strength`: Set the amount of attraction that particles have towards the focal point within the shape. The higher the value, the greater the strength. You can specify a constant strength or vary the strength…
- `Gravity Focus`: Set the focal point for gravity to pull particles towards. A value of 0 attracts particles to the center of the shape, and a value of 1 attracts particles to the outer edge of the shape.
- `Speed`: Set the speed for the Particle System to propel particles around the vortex, which is the center of the force field. The higher the value, the faster the speed. You can specify a constant speed or…
- `Attraction`: Set the strength that particles are dragged into the vortex motion. A value of 1 applies the maximum attraction, and a value of 0 applies no attraction. You can specify a constant attraction or vary…
- `Rotation Randomness`: Set a random axes of the shape to propel particles around. A value of 1 applies maximum randomness, and a value of 0 applies no randomness.
- `Strength`: Set the strength of the drag effect which slows particles down. The higher the value, the greater the strength. You can specify a constant strength or vary the strength over time. For more…

### Particle physics
- Page: https://docs.unity3d.com/Manual/particle-physics.html
- Summary: Resources for configuring simulated physics and collisions A collision occurs when the physics engine detects that the colliders of two GameObjects make contact or overlap, when at least one has a Rigidbody component and is in motion. More info See in Glossary on particles A small, simple image or mesh that is…
- Key sections:
- Unity Manual
- Additional resources
- Selected properties:
- `Particle physics forces`: Understand how the Particle System A component that simulates fluid entities such as liquids, clouds and flames by generating and animating large numbers of small 2D images in the scene. More info…
- `Apply forces to particles`: Use the Particle System Force Field to set up forces on the particles in a Particle System.
- `Particle collisions`: Understand how the Particle System can create and calculate collisions between particles and GameObjects The fundamental object in Unity scenes, which can represent characters, props, scenery,…
- `Particle triggers`: Understand how the Particle System can use particle colliders An invisible shape that is used to handle physical collisions for an object. A collider doesn’t need to be exactly the same shape as the…
- `Configure a particle trigger`: Create and configure a trigger on the particles of a Particle System.
- `Particle System Force Field component reference`: Explore the properties on the Particle System Force Field component, to apply forces to particles.

### Visual Effect Graph
- Page: https://docs.unity3d.com/Manual/VFXGraph.html
- Summary: The Visual Effect Graph is a package that you can use to create large-scale visual effects for your Unity Project. The Visual Effect Graph simulates particle A small, simple image or mesh that is emitted by a particle system. A particle system can display and move particles in great numbers to represent a fluid or…
- Key sections:
- Unity Manual

### Light halos
- Page: https://docs.unity3d.com/Manual/visual-effects-haloes.html
- Summary: Strategies for creating glowing halos around light sources, to give the impression of small dust particles A small, simple image or mesh that is emitted by a particle system. A particle system can display and move particles in great numbers to represent a fluid or amorphous entity. The effect of all the particles…
- Key sections:
- Unity Manual
- Render pipeline information
- Selected properties:
- `Create and configure a halo light effect`: Create and customize a halo around a light source.
- `Halo component reference`: Explore the properties for the Halo component to customize the appearance of a halo.
- `Feature name`: Universal Render Pipeline (URP)
- `Halos`: Yes Use a Lens Flare (SRP) Data Asset and a Lens Flare (SRP) component .

### Particle System component reference
- Page: https://docs.unity3d.com/Manual/class-ParticleSystem.html
- Summary: A Particle System component simulates fluid entities such as liquids, clouds and flames by generating and animating large numbers of small 2D images in the scene A Scene contains the environments and menus of your game. Think of each unique Scene file as a unique level. In each Scene, you place your environments,…
- Key sections:
- Unity Manual
- Additional resources
- Selected properties:
- `Simulate Layers`: Allows you to preview Particle Systems that are not selected. By default, only selected Particle Systems play in the Scene View An interactive view into the world you are creating. You use the Scene…
- `Resimulate`: When this property is enabled, the Particle System immediately applies property changes to particles it has already generated. When disabled, the Particle System leaves existing particles as they…
- `Show Bounds`: When this property is enabled, Unity displays the bounding volume A closed shape representing the edges and faces of a collider or trigger. See in Glossary around the selected Particle Systems.…
- `Show Only Selected`: When this property is enabled, Unity hides all non-selected Particle Systems, allowing you to focus on producing a single effect.

### Configuring particles
- Page: https://docs.unity3d.com/Manual/configuring-particles.html
- Summary: Techniques and resources for configuring how Particle A small, simple image or mesh that is emitted by a particle system. A particle system can display and move particles in great numbers to represent a fluid or amorphous entity. The effect of all the particles together creates the impression of the complete entity,…
- Key sections:
- Unity Manual
- Selected properties:
- `Particle emissions and emitters`: Understand particle emissions, and how Unity manages them via Particle System modules.
- `Configuring global particle properties`: Understand the Particle System properties that configure all particles, including the initial state of new particles.
- `Particle movement`: Resources for configuring particle movement.
- `Particle appearance`: Resources for configuring particle appearance and particle rendering.
- `Particle physics`: Resources for configuring simulated physics and collisions A collision occurs when the physics engine detects that the colliders of two GameObjects make contact or overlap, when at least one has a…

### Lines and trails
- Page: https://docs.unity3d.com/Manual/visual-effects-lines-trails-billboards.html
- Summary: Unity uses specialized components to configure and render lines, trails, and billboards A textured 2D object that rotates so that it always faces the Camera. More info See in Glossary .
- Key sections:
- Unity Manual
- Render pipeline information
- Additional resources
- Selected properties:
- `Rendering lines`: Techniques for rendering individual lines in 3D space, and applying materials to those lines.
- `Rendering trails`: Techniques for rendering trails that appear behind moving GameObjects The fundamental object in Unity scenes, which can represent characters, props, scenery, cameras, waypoints, and more. A…
- `Feature name`: Universal Render Pipeline (URP)
- `Line Renderer A component that takes an array of two or more points in 3D space and draws a straight line between each one. You can use a single Line Renderer component to draw anything from a simple straight line to a complex spiral. More info See in Glossary component`: Yes.
- `Trail Renderer component`: Yes. You can also use VFX Graph to create a custom trail effect.

### Decals
- Page: https://docs.unity3d.com/Manual/visual-effects-decals.html
- Summary: Resources and techniques for projecting materials to act as decals that decorate the surface of other materials.
- Key sections:
- Unity Manual
- Render pipeline information
- Selected properties:
- `Introduction to decals and projection`: Understand what decals are, how Unity uses projection to create them, and what you could use projection for.
- `Decals in the Universal Render Pipeline`: Techniques for using a Decal Renderer Feature or a Decal Projector in the Universal Render Pipeline A series of operations that take the contents of a Scene, and displays them on a screen. Unity…
- `Decals in the Built-In Render Pipeline`: Techniques for using a Projector component in the Built-In Render Pipeline.
- `Feature name`: Built-in Render Pipeline
- `Decal and projectors`: Yes Use the Projector component .

### Lens flares
- Page: https://docs.unity3d.com/Manual/visual-effects-lens-flares.html
- Summary: Resources and techniques for creating lens flares lighting effects, which can add atmosphere to your scene A Scene contains the environments and menus of your game. Think of each unique Scene file as a unique level. In each Scene, you place your environments, obstacles, and decorations, essentially designing and…
- Key sections:
- Unity Manual
- Render pipeline information
- Selected properties:
- `Introduction to lens flare effects`: Understand how Unity manages lens flares, which simulate the effect of lights refracting inside a camera A component which creates an image of a particular viewpoint in your scene. The output is…
- `Lens flares in URP`: Resources and techniques for creating and configuring lens flares in the Universal Render Pipeline A series of operations that take the contents of a Scene, and displays them on a screen. Unity lets…
- `Lens flares in the Built-In Render Pipeline`: Resources and techniques for creating lens flares lighting effects in the Built-In Render Pipeline.
- `Feature name`: Universal Render Pipeline (URP)
- `Lens flares`: Yes Use a Lens Flare (SRP) Data Asset and a Lens Flare (SRP) component , or a Screen Space Lens Flare override .

### Particle effects
- Page: https://docs.unity3d.com/Manual/ParticleSystems.html
- Summary: A particle A small, simple image or mesh that is emitted by a particle system. A particle system can display and move particles in great numbers to represent a fluid or amorphous entity. The effect of all the particles together creates the impression of the complete entity, such as smoke. More info See in Glossary…
- Key sections:
- Unity Manual

## Related Packages
- [[com.unity.render-pipelines.core]]
- [[com.unity.render-pipelines.universal]]
- [[com.unity.shadergraph]]
- [[com.unity.recorder]]

## Official References
- Manual topic: https://docs.unity3d.com/Manual/visual-effects.html
- Package index: [[Unity Package Docs Index]]
