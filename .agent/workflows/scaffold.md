---
description: Standardized Component Generation
---

# Scaffold New Component

When asked to scaffold or create a new feature for PerseusXR, you must follow this exact branching workflow to ensure structural integrity across the stack.

## Step 1: Environment Selection
First, output a quick confirmation of intent: "Is this component for the Unity/Quest 3 environment (C#) or the Offline Processing Engine (Python)?"

## Step 2: Unity/C# Scaffolding
If the target is Unity:
1. **Namespace Wrapping:** All scripts must reside within `namespace PerseusXR.[Subsystem]`.
2. **Class Structure:**
   - Create the `public class`. If it interacts with the Unity lifecycle, inherit from `MonoBehaviour`.
   - Separate `#region Fields`, `#region Unity Callbacks`, and `#region Methods`.
3. **Memory Management:** Auto-generate `OnEnable`, `OnDisable`, and `OnDestroy` if the component subscribes to any global C# Events to prevent delegate leaks.
4. **Location:** Place the file correctly (e.g., `Assets/PerseusXR/Scripts/Managers/`).

## Step 3: Python Processing Scaffolding
If the target is the Offline Engine:
1. **File Skeleton:** Create the `.py` file with full `typing` imports (`List`, `Dict`, `Optional`, etc.).
2. **Class/Function Docs:** Write Google-style docstrings for every top-level node immediately.
3. **Testing Pair:** **Mandatory.** Automatically create the corresponding `test_[filename].py` inside the `scripts/tests/` directory. Import `pytest` and scaffold one placeholder test function that mocks a Quest session folder.
4. **Location:** Place the script appropriately within `scripts/processing/`, `scripts/utils/`, etc.
