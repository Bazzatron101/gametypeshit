# 22: Loot pools and duplicate selling

**What to build:** Each gate has a static loot pool. A clear grants a guaranteed reward drawn at random from the pool, with no duplicates until the whole pool is owned. After that, duplicates can be sold at a minimal hub shop. Loot is personal for each player.

**Blocked by:** 18: Handcrafted boss and gate clear reward, 19: Hub, 21: M3 feel review and kill-switch

**Status:** ready-for-agent

- [ ] Gate pools are data definitions, with a guaranteed reward on clear
- [ ] Draws exclude owned items until the pool is complete
- [ ] Duplicates sell for currency at a minimal hub shop
- [ ] In co-op every player receives their own drop
- [ ] Headless tests cover no-duplicate-until-complete and personal drops
