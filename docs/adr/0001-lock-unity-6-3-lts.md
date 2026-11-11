# 0001: Lock the project to Unity 6.3 LTS

**Status:** Accepted (2026-11-11)

## Context

The project was created on Unity 6000.6.8f1 (Unity 6.6), which is a mainline release
and not long-term support. Unity's release page and release trackers checked on
2026-11-11 list **Unity 6.3 (6000.3.x)** as the current LTS, supported to December 2027
(extended to December 2028). Unity 6.0 LTS reached end of support in October 2026.

At roughly 5 hours a week, the project will live for years. It cannot absorb
breaking engine changes every few months.

## Decision

- Use the latest 6000.3.x patch and record it in `ProjectSettings/ProjectVersion.txt`.
  That file is the single source of truth, and `scripts/test.ps1` reads the editor path from it.
- Move to newer patches within 6000.3 deliberately, one commit per bump, with the test suite passing.
- Re-evaluate at the next LTS (expected around Unity 6.7 or 7.x), not before.

## Consequences

- The project was downgraded from 6.6 while it was still an empty template, so nothing was lost.
- Packages are pinned to the versions Unity 6.3 resolves. Core packages (URP, Test Framework)
  are tied to the editor version.
