# 12: Netcode spike

**What to build:** The M1 slice running across two instances over Steam, tried with the front-runner library (Fish-Net) and at least one alternative, ending in a recorded library decision. Current library capabilities and versions are checked, not assumed.

**Blocked by:** 10: M1 feel review

**Status:** ready-for-agent

- [ ] Two instances play the same slice over Steam transport with a relay, using the front-runner library
- [ ] At least one alternative is tried or ruled out with reasons
- [ ] Prediction and reconciliation are demonstrated or their absence documented
- [ ] Library choice recorded, with the rationale and any version constraints

## Comments

**2026-11-13 (agent):** Requirement from ticket 11 / ADR 0003: the chosen library must support a client joining a running session and receiving the full gate state (late join), then predicting normally. Include a late-join test in the spike and weigh it in the library decision.
