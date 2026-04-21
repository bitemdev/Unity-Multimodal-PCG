# Unity Multimodal PCG Framework

[![Unity 6000.1+](https://img.shields.io/badge/unity-6000.1%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Unity Multimodal PCG Framework is a modular procedural generation package for Unity focused on three things working together:

- procedural environments
- procedural entity spawning with pooling
- procedural music driven by the generated map

It also includes a save/load pipeline, runtime navmesh baking, benchmark utilities, and a one-click editor window that builds a ready-to-test demo scene for package users.

## What The Package Does

At runtime, the framework generates a map, converts it into meshes, bakes a navmesh, analyzes the result to find valid spawn points, then uses those spawn points to:

- place the player and exit
- spawn pooled enemies and objects
- generate a procedural music pattern for the current map

The whole flow is orchestrated by `EnvironmentManager`, which emits an `OnLevelGenerated` event after generation. Other systems subscribe to that event instead of being hard-coupled to the generator.

Core runtime flow:

1. `EnvironmentManager.GenerateLevel()` picks the selected algorithm and seed.
2. The algorithm creates logical map data.
3. `ProceduralMeshBuilder` turns the map into floor and wall meshes.
4. `RuntimeNavMeshBuilder` bakes navigation data from the generated floor.
5. `MapAnalyzer` computes valid spawn points.
6. `EntityManager` consumes spawn points and places entities.
7. `MusicGenerator` consumes the same generation event and builds a procedural sequence.

## Features

- Maze generation using iterative backtracker
- Dungeon generation using BSP
- Burst/Jobs-based mesh building path
- Runtime navmesh baking for generated levels
- Entity pooling for enemies and objects
- Procedural audio using a simple synth + sequencer model
- Save/load support for generation metadata and dynamic entity state
- Massive benchmark helper for repeated automated runs
- One-click editor setup via `Window > Multimodal PCG`

## Installation

This package is distributed as a UPM package.

1. Open Unity 6000.1 or newer.
2. Open `Window > Package Manager`.
3. Click `+`.
4. Choose `Add package from git URL...`
5. Paste:

```text
https://github.com/bitemdev/Unity-Multimodal-PCG.git
```

The package declares `com.unity.ai.navigation` as a dependency. Unity should install it automatically.

## Fast Start

The fastest way to test the package is the built-in setup window.

1. Open `Window > Multimodal PCG`.
2. Click `Create Demo Setup`.
3. Press Play.
4. Click the runtime `Generate Level` button.

That creates a full working hierarchy in the current scene, including:

- environment generation objects
- entity pools
- player/exit prefab references
- procedural audio objects
- a local writable config asset
- a simple runtime UI button
- event system
- camera if the scene did not already have one
- directional light if the scene did not already have one

The window is safe by default:

- it appends to the current scene instead of wiping it
- it refuses to create duplicate package roots in the same scene
- it creates a writable config in the user project instead of editing package assets

## The New Window

Menu:

```text
Window > Multimodal PCG
```

The window exposes three practical workflows:

### 1. Create Demo Setup

Creates a package-controlled root hierarchy in the current scene and wires everything automatically.

The created root is:

```text
---- PCG ----
```

Under it, the package creates:

- `---- ENVIRONMENT ----`
- `---- ENTITIES ----`
- `---- AUDIO ----`
- `Main Canvas`

### 2. Select Existing Setup

If the scene already contains the package root, this selects and pings it instead of creating duplicates.

### 3. Generate Level Now

This calls `EnvironmentManager.GenerateLevel()` from the editor window so you can trigger a generation without entering Play mode.

### 4. Ping Local Config

The setup window creates a local writable asset at:

```text
Assets/MultimodalPCG/Generated/MultimodalPCG-DemoConfig.asset
```

This button highlights it so you can tune generation values immediately.

## What Gets Created In The Scene

The auto-generated scene structure is based on the sample scene included with the package.

### Environment

The environment branch contains:

- `EnvironmentManager`
- `RuntimeNavMeshBuilder`
- `NavMeshSurface`
- `MassiveBenchmarkRunner`
- `Floor`
- `Walls`

Responsibilities:

- generate a maze or dungeon
- build meshes
- assign materials/colliders
- bake navmesh
- save and load the world state

### Entities

The entities branch contains:

- `EnemyPool`
- `ObjectPool`
- `EntityManager`

Responsibilities:

- prewarm pooled enemy and object instances
- place player and exit
- react to generated spawn points
- capture and restore dynamic entity state for the save system

### Audio

The audio branch contains:

- `Synth`
- `MusicManager`

Responsibilities:

- synthesize note playback through `ProceduralAudioSource`
- build a 256-step pattern after a level is generated
- derive note timing from config BPM

### Runtime UI

The setup creates a simple screen-space canvas with a `Generate Level` button.

Its only job is to make first-time testing obvious. Press Play, click the button, and the whole package runs.

## Main Runtime Components

### `EnvironmentManager`

This is the central runtime controller.

Important responsibilities:

- chooses the generation algorithm
- generates the map
- builds floor and wall meshes
- bakes navmesh
- computes spawn points
- raises `OnLevelGenerated`
- supports save/load
- exports benchmark data for each generation pass

Most important public methods:

- `GenerateLevel()`
- `GenerateLevelDeterministic(int forcedSeed)`
- `SaveCurrentGame()`
- `LoadLastGame()`
- `ClearMemory()`

If you are integrating the framework manually, this is the first component to understand.

### `PCGConfiguration`

This ScriptableObject controls the main generation parameters.

Fields exposed through the asset:

- seed
- map width
- map height
- initial enemy count
- initial object count
- music BPM
- pentatonic scale frequencies

Important behavior:

- width and height are clamped in `OnValidate()`
- enemy/object counts are clamped to estimated map capacity
- BPM is clamped
- an empty scale is automatically repaired with a minimal fallback

### `EntityManager`

This subscribes to `EnvironmentManager.OnLevelGenerated`.

When a new map is ready, it:

- clears active pooled entities
- places the player
- places the exit
- spawns enemies from the enemy pool
- spawns objects from the object pool
- assigns runtime `EntityIdentifier` data for save/load tracking

It also provides:

- `FillSaveData(SaveData data)`
- `LoadSaveData(SaveData data)`

### `EntityPool`

Each pool pre-creates instances from a prefab based on values in `PCGConfiguration`.

Behavior:

- enemies pool uses `InitialEnemyCount`
- objects pool uses `InitialObjectCount`
- `Get()` activates the next pooled object
- `DeactivateAll()` returns active instances to the pool

If you want different enemy or object prefabs, change the pool prefab reference.

### `MusicGenerator`

This subscribes to the generation event and turns map output into music.

What it uses:

- the config BPM
- the config pentatonic scale
- the current map seed plus spawn point count

What it does:

- creates a 256-step pattern
- biases note density around stronger beats
- distributes notes across bass, melody, and higher ranges
- sends note playback to `ProceduralAudioSource`

### `ProceduralAudioSource`

This is a small synth that generates audio directly in `OnAudioFilterRead`.

Current sound model:

- sine wave tone generation
- simple attack/decay style volume shaping
- stereo duplication when two channels are present

If you want a different timbre, this is the most direct place to experiment.

### `RuntimeNavMeshBuilder`

This wraps `NavMeshSurface` and rebuilds navmesh after procedural mesh generation.

Current defaults:

- collects geometry from physics colliders
- uses agent type 0
- enables a smaller voxel size for tighter spaces
- accepts the floor layer mask configured in the scene

## Manual Setup

If you do not want to use the one-click window, the minimum manual scene requires:

1. An `EnvironmentManager`
2. A `RuntimeNavMeshBuilder` and `NavMeshSurface` on the same object
3. `Floor` and `Walls` objects with `MeshFilter`, `MeshRenderer`, and `MeshCollider`
4. A `PCGConfiguration` asset
5. An `EntityManager`
6. Enemy and object pools wired to prefabs and config
7. Player and exit prefab references
8. A `MusicGenerator`
9. A `ProceduralAudioSource` on a synth object

The editor window exists because this manual flow is tedious and easy to misconfigure.

## Using The Generated Demo Setup

After the window creates the scene:

1. Select the generated config asset.
2. Tune the values you care about.
3. Enter Play mode.
4. Press `Generate Level`.

From there you can iterate on:

- map size
- algorithm choice
- enemy count
- object count
- BPM
- note scale

The generated setup is intended as a starting point. You can replace references with your own prefabs, materials, and settings.

## Changing The Generation Algorithm

`EnvironmentManager` supports:

- `Maze_Backtracker`
- `Dungeon_BSP`

To switch:

1. Select `EnvironmentManager` in the scene.
2. Change the `Algorithm Type` field.
3. Generate again.

Use `Maze_Backtracker` when you want:

- more corridor-driven layouts
- classic maze structure

Use `Dungeon_BSP` when you want:

- room-and-corridor dungeon layouts
- chunkier spatial separation

## Tuning The Config Asset

The config asset is the main user-facing control surface.

### Seed

- same seed + same config + same algorithm gives deterministic generation
- use this when you want reproducible layouts

### Width / Height

- expressed in cells
- larger maps increase runtime work and mesh size
- the asset clamps them to safe limits

### Initial Enemy Count / Initial Object Count

- these values determine pool prewarming and spawn target counts
- they are clamped in `OnValidate()` to reduce overflow risk

### BPM

- controls music speed
- affects sixteenth-note step duration

### Pentatonic Scale

- array of frequencies used by the music generator
- changing this changes the musical palette of the generated music

## Save / Load System

The package includes a JSON save system.

### What Gets Saved

`EnvironmentManager.SaveCurrentGame()` stores:

- generated seed
- selected algorithm
- player position
- player rotation
- per-entity runtime state

For entities, the package stores:

- runtime ID
- type
- whether it is still active
- position and rotation if still active

### What Gets Restored

`EnvironmentManager.LoadLastGame()`:

1. loads the JSON file
2. restores the algorithm value
3. regenerates the exact level using the saved seed
4. asks `EntityManager` to restore player/entity state

This means the static environment is rebuilt deterministically, then dynamic state is re-applied on top.

### Save Location

The save system writes to:

```text
Application.persistentDataPath/PCG_Saves/QuickSave.json
```

The default file name used by `EnvironmentManager` is currently `QuickSave` through `SaveSystem.SaveGame(...)`.

### How To Use It

You can trigger save/load directly from the `EnvironmentManager` context menu:

- `Save Game`
- `Load Game`

Or call the methods from your own game code:

```csharp
environmentManager.SaveCurrentGame();
environmentManager.LoadLastGame();
```

### Important Limitation

The save system is built around:

- deterministic regeneration of the level
- runtime IDs assigned by `EntityManager`

If you replace the spawning pipeline heavily, you should keep entity identity stable or update the save/load logic accordingly.

## Working With Entities

The package expects entities to come from spawn points computed after map analysis.

Entity categories:

- start/player spawn
- exit
- enemies
- objects

The `EntityManager` uses:

- direct instantiation for player and exit
- pooled reuse for enemies and objects

### Replacing Prefabs

To use your own content:

1. Replace the player prefab reference in `EntityManager`
2. Replace the exit prefab reference in `EntityManager`
3. Replace the enemy pool prefab
4. Replace the object pool prefab

Keep in mind:

- entities should have a collider for proper ground adjustment
- AI enemies that need state restore should use `NavMeshAgent` if you want warp-based repositioning on load
- pooled prefabs benefit from having `EntityIdentifier`, but the manager can add it at runtime if missing

### Ground Placement

The manager computes a collider-based vertical offset so spawned objects rest on the floor mesh instead of intersecting it.

If your prefab floats or clips:

- check its collider setup
- adjust `Global Height Offset` in `EntityManager`

## Working With Audio

The default audio side is intentionally simple and hackable.

If you want to manipulate the music behavior, start with:

- `MusicGenerator` for sequencing logic
- `ProceduralAudioSource` for waveform/timbre behavior
- `PCGConfiguration` for BPM and scale frequencies

Easy experiments:

- change BPM for pacing
- shrink or expand the scale array
- bias note range selection differently
- replace the sine wave with a square or triangle wave

## Benchmarking

Every generation pass already creates a benchmark report through `PCGBenchmark`.

In addition, `MassiveBenchmarkRunner` can automate repeated runs across map sizes and algorithms.

### `PCGBenchmark`

Tracks:

- total generation time
- GC memory delta
- phase timings for:
  - algorithm logic
  - mesh building
  - mesh assignment
  - navmesh baking
  - map analysis
  - entity spawning and events

CSV output path:

```text
Application.persistentDataPath/PCG_Benchmarks/GenerationBenchmark.csv
```

### `MassiveBenchmarkRunner`

Use the `Run Massive Benchmark` context menu on the object that contains `EnvironmentManager`.

It loops through:

- both generation algorithms
- a set of configured map sizes
- a configured number of iterations per size

This is intended for profiling and academic/statistical comparison, not normal gameplay.

## Included Sample

The package includes a sample under `Samples~`.

After importing from Package Manager, import the sample if you want a reference scene showing the original package setup.

The new window exists so users no longer need to reconstruct that sample structure manually.

## Common Workflows

### I want a playable test scene as fast as possible

Use `Window > Multimodal PCG`, click `Create Demo Setup`, then Play and press `Generate Level`.

### I want to iterate on map density

Edit:

- width
- height
- initial enemy count
- initial object count

Then regenerate.

### I want different music

Edit:

- BPM
- pentatonic scale values

Or modify `MusicGenerator`.

### I want to replace the visual style

Replace:

- floor material
- walls material
- entity prefabs

### I want to integrate this into my own scene

Start from the generated setup, then:

- swap references to your own assets
- keep `EnvironmentManager` as the central orchestrator
- keep the event-driven relationship between environment, entities, and audio

## Troubleshooting

### The window says a setup already exists

The tool is intentionally single-root per scene. Use `Select Existing Setup` instead of trying to create another one.

### The runtime button works but enemies do not move

Check:

- `RuntimeNavMeshBuilder` exists
- `NavMeshSurface` exists
- generation completed successfully
- enemy prefab has movement logic if you expect actual behavior beyond spawning

The package bakes navmesh, but it does not by itself implement full enemy AI behavior.

### Save does nothing

`EnvironmentManager.SaveCurrentGame()` requires a generated map first. Generate a level before saving.

### Music does not play

Check:

- `MusicManager` references
- synth object and `AudioSource`
- BPM and scale values in config
- that generation has happened, because the music starts composing after the generation event

### Spawned entities float or clip

Check:

- prefab collider shape
- `EntityManager` global height offset

## Design Notes

This framework is intentionally separated by responsibility:

- environment generation
- entity management
- audio generation
- saving/loading

That separation is what makes the one-click setup window possible. It creates the wiring once, and after that you can replace parts independently.

## License

MIT
