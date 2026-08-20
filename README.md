## Craigles

A simulation of [Boids](https://en.wikipedia.org/wiki/Boids) running inside Unity using [Graphics.DrawMeshInstanced](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Graphics.DrawMeshInstanced.html).

Up to a maximum of 1000 entites can be achieved, with performance dependeing on some "factors" still.

## How to Use

![](https://i.imgur.com/Ay6miKx.png)

The project should be running fine on play and should not require anything to adjust.

The object, Craigle Simulation contains all the code functionality including caluclations of bounds, the cube is used for visuals and help for clarity.

## Configs

Inside `CraigleSimulation`, `Conifg` needs a `SwarmConfig` attached to it.

<img src="https://i.imgur.com/MDfSyed.png" width="400">

In this project, inside the folder `Assets/Scripts/Simualtion/Swarm Options` contains an assortment of customized configs that can be applied inside the `CraigleSimulation` component, feel free to mess around!

### Orbital Camera

I added an orbital camera so you can move the camera around by dragging with your mouse on the screen.

### Performance

Each Boid has "perception", the default is `10` this is used to consider who's nearby as part of its "flock", the higher it is, the larger the group of the flock will be.

Perception is also used to make a spatial grid that is used to get the swarms of the flock, each cell using that perception size, because of this, there's 1000 cells inside the 100x100x100 cube and each boid will check neighboring cubes in 3x3x3 grid.

Additionally, based on the set configs, the perception can impact performance of the game, 10 is optimal to keep FPS consistent, but the _higher the perception_ the _less cells_ there are but _more to consider per cell_, which will dip performance, a perception of 100 should be O(n^2) of complexity because it will be a grid of one singular cell with all of Boids inside, to see a demo, check out "Swarm Default 1 Cell O(n^2)" inside `Assets/Scripts/Simualtion/Swarm Options` **(warning: Will lag to ~30 or less FPS.)**.

Overall, using the default set of `10` for perception has increased performance substantially.
