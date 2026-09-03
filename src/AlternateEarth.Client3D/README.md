# Client3D boundary

The first 3D milestone will be a Godot 4 C# observer that references `AlternateEarth.Shared`, connects to protocol v1, and maps the same logical entities to meshes. No simulation, persistence, inventory, or authoritative physics belongs here.

Acceptance demonstration:

1. Connect one 2D player and one 3D player to the same reality.
2. Render the same roads, footprints, elevation, players, and player structures.
3. Place an object in 2D and observe its mesh appear in 3D with the same entity ID.
4. Remove it in 3D and observe its sprite disappear in 2D.
