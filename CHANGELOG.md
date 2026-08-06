# Changelog

## 0.5.0

- Expanded the player Pockets equipment from three to six Special Slots, displayed as two rows of three.
- Added the Field Armor Repair Kit, sold by Wayland at loyalty level 3.
- Added a narrowly scoped client patch that allows only the Field Armor Repair Kit to repair body armor and ballistic plates during a raid.
- Allowed the Field Armor Repair Kit to target armor carriers and repair their installed plates without removing them first.
- Restricted FARK-initiated repair windows to the carried FARK; trader repair and other repair kits are not offered.
- Kept the vanilla Body armor repair kit and weapon repair behavior unchanged.
- Reduced the field kit to 400 repair points and 6 kg to distinguish it from the full workshop kit.
- Prevented expected red CommonLib creation errors when optional Content Backport templates are unavailable.
- Moved custom-item, extended-Pockets and Wayland registration into SPT's preload phase so profile migrations recognize SALCO items and trader data.

## 0.4.2

- Updated the server and client modules for SPT 4.1.2.
- Updated the WTT CommonLib dependency to the compatible 3.x release line, starting with 3.0.3.
- Ported Medical Merge to SPT 4.1's public inventory-operation names and serialization registry.
- Made WTT Content Backport optional so unavailable backport templates no longer prevent the rest of SALCO's ARMORY from loading.
- Added a clear startup warning when Content Backport templates are unavailable; only dependent items are skipped.

## 0.4.1

- Added general AssetBundle support for custom items through CommonLib and SPT's native bundle system.
- Added custom KPYK mounts and handguards, IMI Defense front grips, and FAB Defense buttstocks.
- Reorganized Wayland's inventory tiers for the new attachments and the MSJ6, D.I.N.N.E.R-41, pTG-change, and B.O.N.E. stimulants.
- Added the "I'm Okay" awareness stimulant: a deliberately non-rewarding, in-raid-only symbolic exit carrying a mental-health support message.

## 0.4.0

- Added Wayland, a dedicated trader who exclusively sells content from SALCO's ARMORY.
- Wayland automatically discovers newly added SALCO items and assigns them configurable prices, stock limits and loyalty levels.
- Added four loyalty levels and a custom trader portrait for Wayland.
- Added the fully documented `config/wayland.jsonc` for pricing, stock, refresh times, flea visibility and per-item overrides.
- Wayland now buys every item category accepted by Mechanic and automatically follows future changes to Mechanic's buy rules.
- Rebalanced pTG-change and B.O.N.E. to retain their strong identities with more controlled drawbacks.

## 0.3.2

- Added B.O.N.E. vital surgery for blacked-out head and chest while the player is still alive.
- B.O.N.E. prioritizes a blacked-out head, followed by a blacked-out chest.
- Vital surgery uses the existing 35-65% maximum-health penalty and consumes one normal B.O.N.E. charge.
- Repeated vital surgery remains possible, with the maximum-health penalty compounding after every use.
- Added a fail-closed compatibility guard so normal stims and surgical kits retain their original behavior.

## 0.3.1

- Updated the WTT - Server CommonLib build dependency to 2.0.23.
- Updated the minimum supported WTT - Content Backport version to 1.1.0.
- Allowed future compatible CommonLib 2.x and Content Backport 1.x releases.
- Updated server and client version metadata to 0.3.1.
