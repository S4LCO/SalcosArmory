# SALCO's Countermeasure Protocol

Version 0.3.0 introduces an optional, server-side system that learns from recent PMC raids and adds equipment-based countermeasures to future PMCs.

## Compatibility boundaries

Countermeasure Protocol does not patch or call SAIN, ORBIT, BigBrain or ABPS code. It does not change:

- bot counts, waves, spawn points or spawn timing;
- SAIN personalities or combat decisions;
- ORBIT objectives, routing, looting or extraction;
- ABPS difficulty or placement settings.

The system reads official SPT raid data and runs as a postfix after SPT has finished generating a bot inventory. If a countermeasure cannot be applied safely, the original inventory is kept.

## Learning model

By default, the five newest PMC raids are retained. The newest raid has full weight and every older raid is multiplied by `historyDecay`. At least three raids are required before the system can activate.

Recorded signals:

- night or daytime raid;
- headshots and total kills;
- kill distances;
- suppressor equipped at raid start;
- armor class equipped at raid start;
- survived or failed raid.

Scav raids and map-to-map transit segments are not recorded.

## Countermeasures

Depending on the configured thresholds, eligible PMCs can receive:

- compatible night-vision equipment;
- compatible face protection;
- a compatible long-range optic when the weapon has no optic;
- hearing protection when the earpiece slot is empty;
- a bounded ammunition penetration upgrade that keeps the original caliber and magazine compatibility.

Only 25-35% of eligible PMCs are selected by default. The exact chance is scaled by measured pressure. A bot receives no more than two successful countermeasures.

## Safety behaviour

- Existing occupied slots are never overwritten by attachment countermeasures.
- Attachment paths are built only through SPT slot filters.
- Item conflict lists are respected.
- An incomplete attachment path is rolled back completely.
- Ammunition stays within the original caliber, configured penetration increase and configured cap.
- Errors are logged and skipped instead of aborting bot generation.
- Each countermeasure can be disabled independently.

## Configuration and reset

Main switch: `config/settings.json` -> `loadCountermeasureProtocol`.

Detailed settings: `config/countermeasure_protocol.jsonc`.

Learning state is stored per profile in `data/countermeasure_protocol`. Deleting the matching state file while the SPT server is stopped resets learning for that profile.

## Fika

Version 0.3.0 is designed for singleplayer SPT. Fika is deliberately excluded until per-player raid ownership and host/client state handling are implemented and tested.
