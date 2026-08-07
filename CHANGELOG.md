# Changelog

## 0.6.1

- Added a narrowly scoped compatibility bridge for SVM custom PMC and Scav pockets.
- Registers only the active SVM custom-pocket templates before SPT runs `InvalidPocketFix`, preventing equipped Special Slot items from being moved to the sorting table during server startup.
- Leaves installations without SVM, as well as SVM presets with custom pockets disabled, completely unchanged.
- Allows SVM to overwrite the temporary bridge templates with its fully configured pocket definitions during its normal initialization.

## 0.6.0

- Added the E.F.-1 "REDLINE" Entry Fragger combat stimulant, sold by Wayland at loyalty level 4.
- REDLINE raises maximum health across all seven body parts by 15% for 45 seconds while preserving each part's current health percentage.
- Blacked-out body parts remain blacked out, the health buffer cannot stack or refresh, and expiry only clamps excess health instead of inflicting direct damage.
- Added a severe 120-second post-effect crash with reduced maximum stamina, impaired stamina recovery, hand tremors, tunnel vision and accelerated energy/hydration loss.
- Added safe cleanup when the duration expires, the player dies, the raid ends or the client plugin unloads.
- Updated B.O.N.E.'s medical-effect field lookup for the public SPT 4.1.2 member names while retaining compatibility fallbacks.

## 0.5.1

- Reworked all removable Soft Armor Inserts into distinct class roles instead of direct durability upgrades.
- Class 3 inserts are now light, durable and inexpensive, while Class 6 inserts offer extreme protection at the cost of weight, mobility, durability and price.
- Added position-scaled durability, weight, movement penalties and prices for front, back, side, groin, shoulder and collar inserts.
- Reduced high-class insert availability through lower static-loot weights and stricter Wayland purchase limits.
- Added a one-time profile migration that preserves the wear percentage of already-owned inserts while applying their new maximum durability.
- Added the documented `config/soft_armor_balance.jsonc` file so every balance value can be customized or the rebalance can be disabled.
- Soft Armor balancing is applied before WTT CommonLib creates the items, preserving CommonLib as the content-loading implementation.

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
