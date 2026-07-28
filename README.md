### ABOUT THE MOD
**SALCO's ARMORY** creates customized variants of existing items by cloning and modifying them.
Weapons, ammo, attachments, armor and items are adjusted through stat changes, compatibility tweaks and configuration overrides.
It also adds hideout crafting recipes, extends weapon magazine/ammo compatibility, and injects additional stim buffs into the global game configuration.
3D models will be added in the future.
Every item this mod brings into the game is labeled with "SALCO's ARMORY:" to make it easy to recognize where items come from.

Starting with version 0.3.0, the optional **SALCO's Countermeasure Protocol** learns from recent PMC raids and equips a limited share of future PMCs with plausible countermeasures. It never controls bot spawns, personalities, movement, combat decisions, looting or extraction.

Starting with version 0.4.0, **Wayland** serves as SALCO's ARMORY's dedicated trader. His inventory is generated automatically from the mod's content and can be customized through a documented configuration file.

The mod is not final. Expect changes at any time.

---

**Another few words:**  
In the past, there were mods such as *SALCO’s ARMORY, SALCO’s ARSENAL, COMBAT ARSENAL and COMBAT ARSENAL: Legacy*.
The decisions weren’t always easy for you to understand, but they were necessary at times.
However, all these mods have one thing in common: they stemmed from my idea and my vision, even though I wasn’t involved in their development towards the end.

The recent events about my person – which I do not wish to elaborate on further at this time – were appalling and deeply shameful.
It was never a question of me giving up modding altogether, but due to these events, it was never clear when I would be able to carry on.

And now it’s clear: back to my roots. Back to where it all began: SALCO’s ARMORY.

### INSTALLATION
#### REQUIREMENTS

- **SPT 4.0.13**
- **WTT - Server CommonLib 2.0.23 or newer within the 2.x release line**
- **WTT - Content Backport 1.1.0 or newer within the 1.x release line**

Major dependency updates may contain breaking changes and require a matching SALCO's ARMORY update.

1. Backup your profile before every update!!!
2. Delete every previous version **completely**!!!
3. Clear your caches!!!
4. Download the mod!!!
5. Extract the ZIP!!!
6. Copy the **contents** of the ZIP into your **SPT root folder**!!!

This mod is **NOT** compatible with previous versions.
Only use this mod if:
- you are starting a completely new save file.
- you have deleted the old mod and all its items from your profile.

### UNINSTALLATION
1. Delete all items from the mod from your profile.
You can recognise them by the fact that they begin with "SALCO's ARMORY:".
Then close the game and the server.
2. Locate the file `core.json` in `...SPT\SPT_Data\configs\` and open it with a text editor of your choice.
3. Once you have opened the file, find the entry `"removeModItemsFromProfile": false,` and set the value to `true`. Then save the file.
4. Start the server and wait until the server has deleted all files that are part of the mod.
5. You can now close the server and change the value back from `true` to `false`.

### GAMEPLAY SYSTEMS

**WAYLAND**

- A dedicated trader who exclusively sells equipment from SALCO's ARMORY.
- Automatically includes supported SALCO items, including content added by future updates.
- Uses four loyalty levels with individual prices, stock limits and purchase limits.
- Buys the same item categories as Mechanic.
- Can show or hide his offers on the flea market.
- Can be configured in `config/wayland.jsonc`.

**B.O.N.E. VITAL SURGERY**

- B.O.N.E. can restore a blacked-out head or chest if the player is still alive.
- A blacked-out head is treated first, followed by a blacked-out chest.
- Each treatment consumes one normal B.O.N.E. charge and restores the body part at 1 HP.
- The existing random maximum-health penalty of 35-65% is applied after every treatment.
- Repeated treatments are possible, but their maximum-health penalties compound.
- B.O.N.E. cannot revive a dead player, and no other stim or surgical kit receives this ability.

**SALCO'S COUNTERMEASURE PROTOCOL**

- Learns from the five most recent PMC raids.
- Starts reacting after at least three recorded raids.
- Measures night activity, headshot ratio, kill distance, suppressor use, heavy armor use and survival rate.
- Applies countermeasures to 25-35% of eligible PMCs, depending on measured pressure.
- Applies no more than two countermeasures to one bot.
- Can add compatible night vision, face protection, long-range optics or hearing protection.
- Can moderately improve compatible ammunition against repeated heavy-armor use.
- Only changes the finished SPT bot inventory and does not patch SAIN, ORBIT, BigBrain or ABPS.
- Excludes Scavs, Player Scavs, bosses, guards and custom factions because only regular PMCs are eligible.
- Stores its learning state per profile in `data/countermeasure_protocol`.
- Can be configured in `config/countermeasure_protocol.jsonc`.
- Fika is currently not supported.

For configuration and compatibility details, see `docs/COUNTERMEASURE_PROTOCOL.md`.

### CONTENT
**AMMO**
- SALCO's ARMORY: 7.62x35mm (.300 Blackout) - Spartans
- SALCO's ARMORY: 6.8x51mm - Blutadler
- SALCO's ARMORY: 5.45x39mm - 7N39M
- SALCO's ARMORY: 5.56x45mm - M995A1
- SALCO's ARMORY: 7.62x39mm - 7N23M
- SALCO's ARMORY: 1143x23mm (.45 APC) - Warden  
<br>  
<br>  

**MEDICALS**
- SALCO's ARMORY: pTG-change regenerative stimulant injector
- SALCO's ARMORY: MSJ6 carry and escape stimulant injector
- SALCO's ARMORY: D.I.N.N.E.R-41 stimulant injector
- SALCO's ARMORY: B.O.N.E. stimulant injector
- SALCO's ARMORY: UFAK first aid kit
- SALCO's ARMORY: Surv12 field surgical kit  
<br>  
<br>  

**MAGAZINES**
- SALCO's ARMORY: AK-300 .300 Blackout 30-Round Magazine
- SALCO's ARMORY: AK 6L31 60-round multi-caliber Magazine
- SALCO's ARMORY: Unit-12 multi-caliber 20-round Magazine
- SALCO's ARMORY: Unit-12 multi-caliber 30-round Magazine  
<br>  
<br>  

**CONTAINERS**
- SALCO's ARMORY: Ammo Pouch
- SALCO's ARMORY: Item Pouch
- SALCO's ARMORY: Medic Pouch
- SALCO's ARMORY: Secure Container DELTA  
<br>  
<br>  

**WEAPONS**
- SALCO's ARMORY: M85 Revenant 7.62x35 (.300 Blackout) assault rifle
- SALCO's ARMORY: Kalashnikov AK-9 9x39 assault rifle
- SALCO's ARMORY: AK-300 .300 Blackout assault rifle
- SALCO's ARMORY: SCAR-68 6.8x51 assault rifle
- SALCO's ARMORY: UNIT-12 multi-caliber assault rifle
- SALCO's ARMORY: SCAR-300 7.62x35 (.300 Blackout) assault rifle  
<br>  
<br>  



### REPORT BUGS
There are two ways to report bugs:
1. My own thread on Discord: https://discord.com/channels/875684761291599922/1521957836928848032
2. In the ‘Issue’ tab on GitHub: https://github.com/S4LCO/SalcosArmory/issues

**IMPORTANT:**
1. I need a clean log.
2. A list of your installed mods.

**Bug reports without logs will be ignored.**
