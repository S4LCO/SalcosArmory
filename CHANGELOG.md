# Changelog

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
