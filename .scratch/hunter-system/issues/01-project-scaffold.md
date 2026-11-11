# 01: Project scaffold

**What to build:** A Unity project that builds and runs headless tests, with version control and backups in place, so every later ticket starts from a safe, repeatable base.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] Project is on a single locked long-term-support engine version, with the current version number checked against Unity's release page rather than assumed
- [x] Render pipeline chosen and set up for flat-shaded low-poly art
- [ ] Version control with large-file support and a remote is configured, and an offline backup routine is documented
- [x] A trivial headless test runs and passes from the command line

## Comments

**2026-11-11 (agent):**
- Engine: locked to 6000.6.8f1 (Unity 6.6). This is not LTS: Unity 6.3 (6000.3.x) was the current LTS when checked on 2026-11-11. The user chose to stay on 6.6 anyway, as a deliberate departure from the LTS requirement (ADR 0001).
- Render pipeline: URP, with flat shading forced at import for `Assets/Art/Models/` (ADR 0002, `Assets/Editor/LowPolyImportRules.cs`).
- Version control: git and Git LFS set up locally, and the offline backup routine is in `docs/backup.md`. The user deferred the remote (no GitHub CLI available), so that checkbox stays open.
- Headless tests: `powershell -File scripts/test.ps1` runs EditMode tests in batch mode. 7 of 7 passed on 6000.6.8f1.
