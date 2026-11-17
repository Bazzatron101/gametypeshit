# 87: The life round the buildings

**What to build:** The small things that make a place feel lived in: fences, market stalls, carts, crates and barrels, lamp posts, signs, washing lines, benches, wells. Split from ticket 82.

**Blocked by:** 83: Streets instead of rings (parent: 82)

**Status:** ready-for-agent

- [ ] A set of small props (placeholder meshes, labelled AI): fence runs, stalls, carts, crates, barrels, lamp posts, hanging signs, benches, washing lines
- [ ] Props are placed by rules: lamps along streets, crates and barrels by shops and warehouses, a sign over each inn and shop, fences round gardens and yards
- [ ] Props are for looks and do not block (except fences, which block like footprints, with gaps for gates)
- [ ] Drawn with instancing per kind, like the nature, so a full town stays cheap
- [ ] Tests: props never stand on a street's middle or in a doorway
