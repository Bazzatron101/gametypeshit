# 0016: Duplicates sell at stores that pay differently

**Status:** Accepted (2026-11-15)

## Context

Ticket 22 asks for loot pools, no duplicates until a pool is owned, personal drops, and a minimal hub
shop that buys duplicates. Pools, personal draws and no repeats already came with the boss (ticket
18). Once a player owned the whole pool, a clear gave nothing. The developer chose the currency's
name, Gold. Where to sell, in the developer's words: "itll be different stores around the towns with
the guild being the easiest but gives you less money back".

## Decision

- **Duplicates.** A player who owns a gate's whole pool draws a duplicate on a clear, from the whole
  pool. The draw is deterministic, like any other draw. The duplicate is kept on the character, one
  entry per copy, to sell. It is not a second copy of the modifier. Loot stays personal: each
  player present draws their own.
- **Stores** are data. Each has a place in the world, a reach, and the share of an item's value it
  pays (1 to 100%). Every item has a sell value (100 by default). A sale pays the store's share,
  rounded down, and at least 1 gold.
- **The guild's store** (Guild Exchange) stands in the hub beside the gate board and pays 50%. It is
  the easiest to reach and pays the least, which content validation checks. Stores in the towns
  come with the overworld (ADR 0015, ticket 36): they are just more store data, paying more.
- **Selling** is a command, like any input, so the host decides. It works only in a store's reach and
  never inside a gate. Walk up, press E, and sell one copy per click.
- **Saves:** the character format is version 3, which adds gold and duplicates (by item id). A
  duplicate of an item no gate drops any more cannot be priced, so it is dropped when the character
  loads. Older saves load with no gold. Snapshots carry both.
- Gold buys nothing yet.

## Consequences

- Gold has no use until something sells (gear, ticket 23, or later). This is accepted for now: the
  spec says a player economy beyond selling duplicates is out of scope.
