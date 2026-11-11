# 13: Two-player Steam co-op

**What to build:** A host starts a session, a friend joins by Steam invite, and both fight the same enemy. The host is authoritative over enemies, damage, and loot, with prediction so each player's own character feels instant.

**Blocked by:** 11: Decide mid-gate joining, 12: Netcode spike

**Status:** ready-for-agent

- [ ] Invite and join flow works through Steam with no manual network setup
- [ ] Both players see and damage the same enemy, with the host authoritative
- [ ] Own-character movement and attacks feel immediate for the guest
- [ ] The mid-gate joining decision is honored
