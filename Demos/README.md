# Running the examples

No runnable scenes are shipped. Use the Assets installation described in the
root README, create a scene with a camera and lighting, and use one bootstrap
per scene. The examples are application samples, not a ready-made game.

| Example | Setup |
|---|---|
| RotateCubeDemo | Add `RotateCubeBootstrap` to an empty GameObject. It creates 100 cube GameObjects and syncs ECS transforms to them. Place the camera to see the area around the origin. |
| BoidsDemo | Add `BoidsBootstrap`; assign a Mesh and Material, and choose the boid count. Enable GPU instancing on the material. |
| CubeSculptureDemo | Add `CubeSculptureBootstrap`; assign cube Mesh/Material and configure count/scale. Enable GPU instancing on the material. |

Boids `Systems/BoidsSystems.cs` and sculpture `Render/` use
`Graphics.RenderMeshInstanced`; rendering runs on the main thread with managed
mesh/material resources. Read those examples when building a renderer.

The rotate/boids bootstraps explicitly reset global state, so run them separately
from other worlds. Core `WorldInstaller` itself does not reset other worlds.
Samples include historical registration styles; prefer `[System]` methods and
the current gameplay guide for new code. Hot reload requires its editor compiler
dependencies and is optional for learning the ECS API.
