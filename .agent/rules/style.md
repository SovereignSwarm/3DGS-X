---
description: Code Style and Linting Rules
---

# Code Style and Linting Standards

As the Antigravity agent operating in this repository, you must adhere strictly to these code-style guidelines across the C# (Unity) and Python ecosystems.

## C# (Unity Ecosystem)
- **Naming Conventions:**
  - `PascalCase` for `public` fields, `Properties`, `Classes`, and `Methods`.
  - `camelCase` for `private` and `protected` fields (optionally prefixed with `_`).
  - `ALL_CAPS` for `const` values.
- **Linting & Formatting:** Follow standard Unity guidelines (e.g., Rider/ReSharper style). Keep `MonoBehaviour` methods (like `Update`, `Start`) free of heavy logic by deferring to dedicated service classes.
- **Regions:** Use `#region` and `#endregion` to encapsulate logical blocks (e.g., Unity Callbacks, Fields, Methods) when files exceed 200 lines.

## Python (Reconstruction Engine)
- **Formatting:** Use `black` syntax formatting. Maximum line length is 120 characters.
- **Linting:** Enforce `ruff` for linting. All new modules must clear `ruff check .` with no warnings.
- **Typing:** Strict Python type hints (`typing` module) are **mandatory** for all function signatures and complex variables.
- **Docstrings:** Use Google-style docstrings for every class and public function, describing `Args`, `Returns`, and `Raises`.

## General Constraints
- No "magic numbers." Extract all arbitrary values into named constants or configuration sets.
- Do not introduce trailing whitespaces. Ensure files end with a blank newline.
