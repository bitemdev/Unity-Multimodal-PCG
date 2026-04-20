# Unity Multimodal PCG Framework

[![Unity 6000.1+](https://img.shields.io/badge/unity-6000.1%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A high-performance, modular toolkit designed for rapid prototyping of procedural environments, entities, and audio in Unity. Developed as a Final Degree Project.

## Key Features

* **Environment Module:** Hybrid procedural generation using Binary Space Partitioning (Dungeons) and Iterative Backtracker (Mazes) via the Strategy Pattern.
* **High Performance:** Built with **Data-Oriented Design (DOD)** principles, the Unity Job System, and Burst Compiler to ensure minimal memory footprint and **Zero Garbage Collection (GC)** spikes during runtime execution.
* **Multimodal Generation:** Fully integrated Procedural Audio Sequencer (infinite generative melodies) and context-aware Entity Pooling & Spawning.
* **Serialisation System:** Robust Save/Load functionality for seeds, generation parameters, and dynamic world states.

## Installation (Unity Package Manager)

This tool is distributed as a standard UPM package. To install it in your Unity project:

1. Open Unity (Requires 6000.1 or newer).
2. Go to **Window > Package Manager**.
3. Click the **+** button in the top-left corner and select **Add package from git URL...**
4. Paste the following URL and click Add:
   `https://github.com/bitemdev/Unity-Multimodal-PCG.git`

## Architecture Overview

The framework is strictly decoupled. It separates environment logic from entity management and audio generation, ensuring clean architecture. Heavy grid analysis and mesh building are offloaded to asynchronous Burst-compiled Jobs, guaranteeing a smooth frame rate even on large map generations.