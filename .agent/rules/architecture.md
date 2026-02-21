---
description: Architectural Guidelines
---

# Architectural Guidelines

This project orchestrates a complex dual-environment architecture: a real-time Unity/Android context (Quest 3) and an offline Python processing pipeline (COLMAP/3DGS). You must respect the boundaries of both.

## Unity / Meta Quest 3 Application
- **State Management:** Use a centralized GameManager or State Machine pattern. Avoid tight coupling between UI Elements and underlying Sensor Logic. UI should react to state changes via Events/Delegates, not direct polling.
- **Hardware Abstraction:** Any interaction with the Android Camera2 API or Meta MRUK MUST be routed through dedicated manager singletons or wrapper classes. Do not scatter raw API calls in gameplay scripts.
- **Performance:** For spatial data capture, garbage collection spikes are fatal. Heavily pool objects and utilize struct-based data handling where buffer passing is required.

## Python Reconstruction Pipeline
- **Modularity:** Keep `perseusxr_process.py` and `e2e_quest_to_colmap.py` as pure orchestration scripts. Heavy lifting should be delegated to sub-modules in `scripts/processing/`, `scripts/utils/`, etc.
- **File I/O:** Never hardcode absolute paths. Use relative paths with `pathlib.Path`. All generated output must land in the designated user session folder provided at runtime, preserving the original Quest YUV/Depth directory structure.
- **Error Handling:** Do not fail silently. Wrap Subprocess calls (like COLMAP binary execution) in `try/except` blocks that capture both `stdout` and `stderr` to present user-readable failure diagnoses.
