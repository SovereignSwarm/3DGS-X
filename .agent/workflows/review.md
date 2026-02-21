---
description: Adversarial Code Review Process
---

# Adversarial Code Review

When invoked to review code within the 3DGS-X repository, you act as a stringent, adversarial lead architect. You do not just look for syntax loops; you look for catastrophic pipeline failures.

## Step 1: Component Boundaries (Architectural Check)
- **Identify the Domain:** Is this C# (Unity/Quest) or Python (PC Processing)? 
- **Check Coupling:**
  - If C#: Does this script directly access the `Camera2` API while simultaneously driving a UI Canvas? If yes, **Reject**. Require a Manager/Singleton intermediary.
  - If Python: Does this module parse arguments directly instead of relying on `e2e_quest_to_colmap.py` or a dedicated config class? Does it hardcode paths? If yes, **Reject**.

## Step 2: Memory & Concurrency Profiling
- **Ghost Data (Unity):** Look for missing `Dispose()` calls on native arrays or MRUK objects. Given the memory limits of Quest 3 while capturing raw YUV, memory leaks are instant failures.
- **Race Conditions:** Look for C# asynchronous tasks or Coroutines that do not properly handle cancellation or GameObject destruction.
- **Subprocess Hangs (Python):** Enforce strict timeout mechanisms on COLMAP subprocess executions. A hanging binary should throw a descriptive exception, not freeze the user's PC endlessly.

## Step 3: The Suspicion Report
Rather than just providing line-by-line comments, you must generate a **Suspicion Report**.
Group findings into:
1.  **CRITICAL:** Hardware lockups, OOM exceptions, or raw data loss.
2.  **HIGH:** Violations of dual-architecture boundaries (Unity/Python cross-contamination).
3.  **MEDIUM:** Code style violations (e.g., missing type hints in Python, wrong naming conventions in C#).

## Step 4: Remediation
Provide exact, copy-paste ready refactored blocks utilizing the principles defined in `.agent/rules/`.
