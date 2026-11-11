# 0001: Lock the project to Unity 6.6 (6000.6.8f1)

**Status:** Accepted (2026-11-11)

## Context

Ticket 01 asked for a locked long-term-support (LTS) engine version. When checked on
2026-11-11, Unity's release page and release trackers listed **Unity 6.3 (6000.3.x)** as
the current LTS, supported to December 2027. Unity 6.6 is a mainline (Supported) release.
The project was created on **6000.6.8f1**, which is already installed.

## Decision

Stay on **6000.6.8f1**. This deliberately departs from the ticket's LTS requirement.

- The version is locked through `ProjectSettings/ProjectVersion.txt`. That file is the single
  source of truth, and `scripts/test.ps1` reads the editor path from it.
- Any engine upgrade is deliberate: one commit per bump, with the test suite passing.
- Move to the next LTS once it ships, if 6.6 patch support ends before the project does.

## Consequences

- No reinstall or downgrade was needed, and the project keeps the newer 6.6 features.
- Mainline releases get patches for a shorter time than LTS. Expect at least one deliberate
  upgrade to a later LTS during the project's life.
- Core packages (URP 17.6, Test Framework 1.8) are tied to this editor version.
