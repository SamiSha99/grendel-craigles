## to-do

1. Make a struct that holds all the mentioned information in the PDF.
2. Create OrbitCamera that rotates around the point inside the cube.
3. Make a material that flips the rendring so we can be inside the cube (like the video shows)
4. Apply the above.
5. Make the Simulation

## Found sorces

1. It seems the algorithm is called Boids from researching, I'm guess Craigle is flavor? https://en.wikipedia.org/wiki/Boids
    - Oh wait its literally named after the guy ok lol.
2. Will be using DrawMeshInstanced, but it has a limit of 1023, courtesy of the docs. https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Graphics.DrawMeshInstanced.html

### Other

1. Half angle in random direction should be cone shaped.
2. It's not necessary for visuals, but I would like to keep it clear. Code a quick shader?
3. Should get different configs to see it in different tests.
