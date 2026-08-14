# ArchipelaWoW Quest Extractor

[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Generates the `data/locations/quests.json` file consumed by
[ArchipelaWoW](https://github.com/r-o-b-o-t-o/archipelawow), a custom APWorld for the
[Archipelago](https://archipelago.gg) randomizer framework.

The tool reads an [AzerothCore](https://www.azerothcore.org) world database and a 3.3.5a client's DBC
files, filters the quest list down to the ones that make sense as randomizer locations, and writes the
result as JSON.

## Contents

- [Requirements](#requirements)
- [Preparing the world database](#preparing-the-world-database)
- [Configuration](#configuration)
- [Running](#running)
- [What gets filtered out](#what-gets-filtered-out)
- [Output](#output)
- [Regenerating the entity model](#regenerating-the-entity-model)
- [Third-party data](#third-party-data)
- [License](#license)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A MySQL AzerothCore world database, reachable and already populated
- A 3.3.5a `dbc` directory, such as the one AzerothCore needs at `data/dbc`

## Preparing the world database

Quest zones are read from the `zoneId` column of the `creature` and `gameobject` spawn tables.
AzerothCore leaves those columns at `0` until worldserver has been run once with the following options
enabled in `worldserver.conf`:

```ini
Calculate.Creature.Zone.Area.Data = 1
Calculate.Gameobject.Zone.Area.Data = 1
```

Start worldserver once with those set, let it finish loading, then shut it down. The columns are
written back to the database and stay populated, so this only has to be done once per world database
(and again after any change that adds spawns). You can turn the options back off afterwards.

Without this step most quests are extracted with empty `startZones` and `endZones`. The extractor logs
a warning when it detects that, but it will not stop.

The extractor only reads from the database.

## Configuration

Configuration comes from environment variables, which are also read from a `.env` file. Copy
[`.env.example`](.env.example) to `.env` and edit it:

```sh
cp .env.example .env
```

The file is looked up by walking up from the working directory, then from the build output, so it
works from both `dotnet run` and Visual Studio.

| Variable            | Required | Default          | Description                                                                       |
| ------------------- | -------- | ---------------- | --------------------------------------------------------------------------------- |
| `OUT_DIR`           | yes      | —                | Directory `quests.json` is written to. Point it at ArchipelaWoW's `data/locations`. |
| `DBC_DIRECTORY`     | yes      | —                | Directory holding the extracted `.dbc` files.                                       |
| `WORLD_DB_HOST`     | no       | `localhost`      | World database host.                                                                |
| `WORLD_DB_PORT`     | no       | `3306`           | World database port.                                                                |
| `WORLD_DB_USER`     | no       | `acore`          | World database user.                                                                |
| `WORLD_DB_PASSWORD` | no       | `acore`          | World database password.                                                            |
| `WORLD_DB_DATABASE` | no       | `acore_world`    | World database name.                                                                |
| `DBC_BUILD`         | no       | `3.3.5.12340`    | Client build the DBC files come from, used to pick the right column layout.         |
| `DBC_LOCALE`        | no       | `enUS`           | Locale to read localised strings in.                                                |

## Running

```sh
dotnet run
```

The tool logs every quest it drops along with the reason, then writes `quests.json` to `OUT_DIR`,
creating the directory if needed.

## What gets filtered out

A quest only becomes an Archipelago location if a player can reliably complete it once, on their own,
on any character the run allows. Quests are dropped when they are:

- listed in the `disables` table
- daily, weekly, monthly, repeatable, seasonal or tied to a world event
- flagged as raid, PvP or unavailable, or sorted under a raid/heroic quest info
- part of a profession, reputation or PvP-kill requirement
- pooled, so that only some of them are available at a time
- missing a quest starter or a quest ender
- unavailable to every playable race, including through a quest giver hostile on sight
- named like a deprecated or test entry (`<`, `[`, `deprecated`, `unused`, `temp `)
- requiring a level above the level cap
- part of a positive `ExclusiveGroup`, where completing one quest locks the others out — any of them
  could have been a location the player needed
- one of a few hardcoded sets: collector's edition rewards, the reputation cloth donation turn-ins, and the riding-skill quests

Finally, quests whose prerequisites did not survive the filters are dropped too, repeatedly, until the
set is stable.

Dungeon quests are the one group that is kept despite needing a group, because whether they belong in a
run is a player's call rather than the extractor's. They are marked with `isDungeon` instead, for
ArchipelaWoW to include or exclude per option. Note that the quest data almost never sets
`SuggestedGroupNum` on them, so `suggestedGroupSize` is `null` on virtually every one and is no
substitute for the flag.

## Output

`quests.json` is an array of quest objects sorted by id:

```json
{
  "id": 176,
  "title": "Wanted:  \"Hogger\"",
  "displayTitle": "Wanted:  \"Hogger\"",
  "minLevel": 5,
  "recommendedLevel": 9,
  "suggestedGroupSize": null,
  "races": [1, 3, 4, 7, 11],
  "classes": null,
  "questSortArea": { "id": 12, "name": "Elwynn Forest" },
  "isBreadcrumb": false,
  "isDungeon": false,
  "requiresAny": [],
  "requiresAll": [],
  "startZones": [{ "id": 12, "name": "Elwynn Forest" }],
  "endZones": [{ "id": 12, "name": "Elwynn Forest" }],
  "objectiveZones": [{ "id": 12, "name": "Elwynn Forest" }]
}
```

`races` and `classes` are `null` when the quest carries no restriction, and a list of DBC ids
otherwise.

### Prerequisites

`requiresAll` holds prerequisites that are all needed, `requiresAny` prerequisites of which any single
one is enough. Both only ever reference quests that are themselves in the file, so a chain can be
walked without hitting a dead end.

"The Hunt Completed" closes the Ashenvale trophy chain and only opens once all three trophies have been
handed in, so its three prerequisites land in `requiresAll`:

```json
{
  "id": 247,
  "title": "The Hunt Completed",
  "requiresAny": [],
  "requiresAll": [2, 23, 24]
}
```

"Crocolisk Mastery: The Trial" is the opposite case. Hemet Nesingwary's hunting lines in Sholazar
Basin are open to every race and run in parallel, and both the rhino and the dreadsaber line lead into
the crocolisk one, so whichever the player worked through first opens it:

```json
{
  "id": 12551,
  "title": "Crocolisk Mastery: The Trial",
  "requiresAny": [12520, 12549],
  "requiresAll": []
}
```

Be careful reading `requiresAny` as a player-facing choice, though. Most entries with more than one
alternative — 18 of the 26 in the current output — list the Alliance and the Horde version of the same
quest, which no single character can pick between. The list means "any one of these unlocks it", not
"the player gets to decide".

A quest can carry both lists at once, in which case it needs every entry of `requiresAll` and at least
one entry of `requiresAny`. Prerequisites are resolved after filtering: a quest is dropped when its
whole `requiresAny` list or any of its `requiresAll` entries did not survive, which is why the chains
that remain are always completable.

## Regenerating the entity model

Everything under [`Entities/`](Entities) is scaffolded from the world database and should not be edited
by hand; the hand-written navigations and keys live in [`EntityExtensions/`](EntityExtensions) as
partial classes instead. Only the 16 tables the extractor actually reads are generated. From the Visual
Studio Package Manager Console:

```powershell
Scaffold-DbContext 'Host=localhost;User=root;Password=root;Database=acore_world' Microting.EntityFrameworkCore.MySql `
  -Context WorldDbContext -NoOnConfiguring -DataAnnotations -Force `
  -ContextDir Entities -ContextNamespace ArchipelaWoW.QuestExtractor.Entities `
  -OutputDir Entities/World -Namespace ArchipelaWoW.QuestExtractor.Entities.World `
  -Tables quest_template,quest_template_addon,quest_poi,creature,creature_template,creature_queststarter,creature_questender,gameobject,gameobject_template,gameobject_queststarter,gameobject_questender,item_template,game_event_creature_quest,game_event_gameobject_quest,pool_quest,disables
```

Two things to know before running it:

- `-Force` overwrites the listed tables but never deletes anything, so dropping a table from the list
  leaves its entity file behind. Delete the orphan by hand afterwards rather than emptying
  `Entities/World` first — the command builds the project before it generates, so a missing entity file
  fails the build before any scaffolding happens.
- Scaffolding the whole database instead of a table list does not compile: the `version` table
  generates an entity that collides with `System.Version` under this project's implicit usings. Keeping
  the list narrow avoids that, along with roughly 18,000 lines of unused code.

Adding a table means listing it above and wiring its relationships into
[`EntityExtensions/WorldDbContext.cs`](EntityExtensions/WorldDbContext.cs).

## Third-party data

The `.dbd` files under [`DBCDefinitions/`](DBCDefinitions) come from
[wowdev/WoWDBDefs](https://github.com/wowdev/WoWDBDefs) and are licensed under
[CC BY-SA 4.0](DBCDefinitions/LICENSE.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
