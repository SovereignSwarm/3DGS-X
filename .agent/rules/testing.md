---
description: Testing Standards
---

# Testing Standards

Reliability is paramount for a spatial capture engine. As an agent, you must craft tests that validate core assumptions before user deployment.

## Unity (C#) Testing
- **Framework:** Use the Unity Test Framework (NUnit).
- **EditMode Tests:** Write fast, stateless tests for mathematical utilities, data serialization logic, and coordinate conversions (e.g., Quest to standard OpenGL/COLMAP spaces).
- **PlayMode Tests:** Use PlayMode tests to validate State Machine transitions and the event lifecycle during the "3-Pass Guided Capture" sequence.
- **Coverage Goal:** Attempt to maintain 80%+ coverage on core data logging buffers and synchronization scripts.

## Python (Reconstruction) Testing
- **Framework:** `pytest` is the mandatory framework.
- **Integration Tests:** Ensure end-to-end processing scripts can handle mock session folders (containing dummy YUV, JSON intrinsics, and pose data) without crashing.
- **Unit Tests:** Critical geometric and image processing functions (like CLAHE Tone Mapping or depth reprojection) MUST include unit tests validating deterministic outputs against known inputs.
- **Fixtures:** Heavy use of `pytest.fixture` is expected for generating temporary mock directories representing a "Quest 3 extracted session folder" to prevent littering local drives.
