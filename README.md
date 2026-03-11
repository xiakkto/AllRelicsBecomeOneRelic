# AllRelicsBecomeOneRelic

Replaces relic gains in Slay the Spire 2 so that every obtained relic becomes one chosen relic.

This project is intended to live under the game directory at:

```text
Slay the Spire 2/mod_projects/AllRelicsBecomeOneRelic
```

Press `F8` in game to open the configuration overlay.

## Current Behavior

- Runtime relic gains are redirected through `RelicCmd.Obtain`.
- Starting relics are always replaced on new runs.
- Many relic previews are also rewritten so rewards usually show the chosen relic before pickup.
- A small set of event/rest-site edge cases are patched so the run can continue normally under strict relic replacement.

## Config

The template config in this repo is:

```json
{
  "target_relic_id": "CIRCLET",
  "replace_starter_relics": true,
  "log_every_replacement": false,
  "preserve_relic_producers": false
}
```

The live config used by the game is:

```text
mods/AllRelicsBecomeOneRelic/AllRelicsBecomeOneRelic.json
```

Fields:

- `target_relic_id`: the relic ID to force. Examples: `CIRCLET`, `ICE_CREAM`, `BURNING_BLOOD`, `IVORY_TILE`
- `replace_starter_relics`: currently always `true` in code
- `log_every_replacement`: write each replacement to log output
- `preserve_relic_producers`: keep `WONGOS_MYSTERY_TICKET` unchanged instead of replacing it

## In-Game UI

Press `F8` to open the mod panel.

- Search relics by ID or localized name
- Pick the target relic from the list
- Toggle `保留特殊遗物`
- Save changes immediately without rebuilding

## Build

From the game root:

```powershell
.\mod_projects\AllRelicsBecomeOneRelic\build.ps1
```

The build script:

- builds the DLL
- copies it into `mods/AllRelicsBecomeOneRelic`
- creates the required `.pck` with the game's built-in Godot runtime
- copies the config template into the installed mod folder if it does not exist yet

To copy only the latest DLL:

```powershell
.\mod_projects\AllRelicsBecomeOneRelic\install_latest_build.ps1
```

## Repo Notes

- Do not commit game files or decompiled game source
- `bin/` and `obj/` are ignored on purpose
- The game only loads mods after you accept the in-game mod warning once
