---
name: prep_quest_session
description: Scaffold a mock Meta Quest 3 raw capture session folder structure.
---

# Skill: Prepare Quest 3 Capture Session

## Goal
The goal of this skill is to instantly generate a synthetic Meta Quest 3 capture session directory. This is crucial for safely testing the `perseusxr_process.py` pipeline (which relies on specific YUV streams, Depth maps, JSON intrinsics, and pose records) without requiring the user to don the headset and execute a manual scan for every small Python script change.

## Instructions
1. When asked to "mock a quest session" or "test the processing pipeline," trigger this skill.
2. Determine the target output directory (default: `./mock_session_data`).
3. Execute the underlying python script to orchestrate the creation of the mocked files:
   `python .agent/skills/prep_quest_session/scripts/mock_session_generator.py --output {output_dir}`
4. Alert the user that a mock dataset has been populated, specifying the number of synthetic frames generated.

## Constraints
- Do not overwrite existing folders if they look like genuine Quest captures (check for `session.json`). The script includes a safety check.
- Keep the generated file sizes small (e.g., small dummy arrays representing images/depth).
- This is a *synthetic* dataset intended for pipeline integration testing, not valid COLMAP photogrammetry.
