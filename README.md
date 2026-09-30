# ArchipelaWoW Data Extractor

![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/r-o-b-o-t-o/archipelawow-data-extractor/build.yml?branch=master)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Generates the `data/quests.json` and `data/spells.json` files consumed by
[ArchipelaWoW](https://github.com/r-o-b-o-t-o/archipelawow), a custom APWorld for the
[Archipelago](https://archipelago.gg) randomizer framework.

The tool reads an [AzerothCore](https://www.azerothcore.org) world database and a 3.3.5a client's DBC
files, works out which quests and which trainable spells make sense as randomizer locations, and
writes each list as JSON. Both extracts are produced in one run.

## Contents

- [Requirements](#requirements)
- [Preparing the world database](#preparing-the-world-database)
- [Configuration](#configuration)
- [Running](#running)
- [Quests](#quests)
- [Spells](#spells)
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
a warning when it detects that, but it will not stop. Only the quest extract needs this; the spell
extract does not read the spawn zones.

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
| `OUT_DIR`           | yes      | —                | Directory the extracts are written to. Point it at ArchipelaWoW's `data`.           |
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

The tool logs every quest it drops along with the reason, then writes `quests.json` and `spells.json`
to `OUT_DIR`, creating the directory if needed.

## Quests

### What gets filtered out

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
- one of a few hardcoded sets: collector's edition rewards, the reputation cloth donation turn-ins, the
  riding-skill quests, quests whose giver only a profession brings out, quests no player can get (or
  only during a window that never comes back), and failsafe quests that only give back what a player
  lost or missed earlier in a chain, such as "Replacement Phial"

Finally, quests whose prerequisites did not survive the filters are dropped too, repeatedly, until the
set is stable.

Dungeon quests are the one group that is kept despite needing a group, because whether they belong in a
run is a player's call rather than the extractor's. They are marked with `isDungeon` instead, for
ArchipelaWoW to include or exclude per option. Note that the quest data almost never sets
`SuggestedGroupNum` on them, so `suggestedGroupSize` is `null` on virtually every one and is no
substitute for the flag.

### Output

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
  "questSort": null,
  "isMissable": false,
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

`suggestedGroupSize` is `null` on quests meant for one player. A quest the quest log tags as Group
without saying how many players it wants, such as "Wanted: Gath'Ilzogg", counts as 2, the smallest
group.

`questSortArea` is the zone the quest log files the quest under. Quests filed under a category
instead, such as a class, carry it in `questSort`, as in `{ "id": 161, "name": "Mage" }`, where `id` is a
`QuestSort.dbc` id. At most one of the two is set.

`isMissable` marks quests a player can lose for good by getting to another quest first, which makes them
unreliable locations. The core takes a quest away in three ways, each of which only counts when a
character who could take both quests can reach the other one without it:

- breadcrumbs, set with `BreadcrumbForQuestId`, once their target is started. "The Hermit" is not marked,
  as "Supplies from Darkshire" cannot be taken without it
- quests whose `RewardNextQuest` is taken first: the core refuses a quest while that follow-up is in the
  quest log, and for good once it is turned in, so taking "Super Reaper 6000" straight from Ziz Fizziks
  and turning it in loses "Ziz Fizziks"
- quests whose `conditions` stop holding once another quest is turned in, such as "Guarded Thunderbrew
  Barrel", only offered while "Bitter Rivals" is complete and not yet turned in

Quests held back by a repeatable quest are left alone, as a repeatable quest stops blocking anything once
it is turned in. Breadcrumbs are the exception: the core remembers their target was turned in all the same.

A quest that can only be reached through a missable one is lost along with it, so it is marked too: "The
Engraved Ring" and the two quests after it only open through "Rot Hide Clues".

#### Prerequisites

`requiresAll` holds prerequisites that are all needed, `requiresAny` prerequisites of which any single
one is enough. Both only ever reference quests that are themselves in the file, so a chain can be
walked without hitting a dead end.

They are read from `PrevQuestID`, `NextQuestID` and `ExclusiveGroup` in `quest_template_addon`, and from
the quest availability rows of `conditions` that ask for another quest to be rewarded, taken or complete.
Condition rows sharing an `ElseGroup` must all hold and any one group is enough, so what every group asks
for lands in `requiresAll` and the rest in `requiresAny`. "Teron Gorefiend, I am..." (10639) is only tied
to the three Divination quests before it through `conditions`.

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
alternative — 21 of the 40 in the current output — pair an Alliance quest with its Horde counterpart,
which no single character can pick between. The list means "any one of these unlocks it", not
"the player gets to decide".

A quest can carry both lists at once, in which case it needs every entry of `requiresAll` and at least
one entry of `requiresAny`. Prerequisites are resolved after filtering: a quest is dropped when its
whole `requiresAny` list or any of its `requiresAll` entries did not survive, which is why the chains
that remain are always completable.

## Spells

Training a class ability is a location in ArchipelaWoW, and the ability itself is an item, so the
extract has to say which spells a class can train and at what level. Neither half of that answer lives
in one place: the trainer lists come from the world database, the names, ranks and effects from
`Spell.dbc`, and which abilities a character is created holding from `SkillLineAbility.dbc`.

### What becomes an entry

- **Class spells** (`"kind": "class"`) — the first rank of everything a class trainer sells. Only first
  ranks: shuffling every rank would multiply the pool several times over for no added variety, and a
  character handed the first rank trains the rest from its trainer as usual.
- **Riding ranks** (`"riding"`) — every rank a mount trainer sells, because here the ranks *are* the
  progression: a character handed Journeyman Riding has no trainer path to Artisan unless Artisan is in
  the pool too. The four ranks are told apart from the rest by replacing one another in turn; Cold
  Weather Flying replaces nothing and is replaced by nothing, so it comes out as `"mount"` and stays off
  that ladder.
- **Weapon skills** (`"weapon"`) — the weapon proficiencies a weapon master sells. A weapon master is
  keyed by no class at all, so these carry `classRaces` rather than `classId`: who may buy one is read
  off `SkillLineAbility.dbc` and `SkillRaceClassInfo.dbc` the way `Player::IsSpellFitByClassAndRace`
  does, and whoever is created already holding the skill is left out. Both halves matter — Thrown names
  no class in its skill line entry, and only `SkillRaceClassInfo.dbc` says it belongs to warriors,
  hunters and rogues — and so does race: a dwarf hunter starts with Guns and a troll one with Bows, so
  each is sold what the other started with.
- **Starter abilities** (`"starter"`) — what a character is created knowing, worked out the way the core
  does it, by walking the default skills of its race and class. These are resolved first and then kept
  out of the trainer sweep, so a realm with ArchipelaWoW's own update applied — which puts the starting
  abilities on the class trainers so their checks can be bought — extracts identically to one without it.

Dropped along the way: anything past rank 1 on a class trainer, spells flagged
`SPELL_ATTR0_DO_NOT_DISPLAY` (the bookkeeping entries a player never sees, like "Maelstrom Ready!"),
Dual Wield and the armor proficiencies, a mage's `Teleport:` and `Portal:` spells, and a hunter's Auto Shot.

Death knights are left out entirely, since ArchipelaWoW does not offer the class: their trainers are
skipped, their starting kit is not collected, and they are named by no weapon skill's `classRaces`.
The classes that *are* extracted are listed in `RANDOMIZED_CLASS_IDS` in
[`SpellExtractorService`](Services/SpellExtractorService.cs), the one place to change if that ever moves.

### Output

`spells.json` is an array sorted by class, then by required level, then by id:

```json
{
  "id": 403,
  "name": "Lightning Bolt",
  "classId": 7,
  "classRaces": {},
  "reqLevel": 1,
  "reqSkillRank": 0,
  "taughtSpells": [],
  "raceMask": 0,
  "factions": 0,
  "expansion": 0,
  "kind": "starter"
}
```

| Field          | Meaning                                                                                    |
| -------------- | ------------------------------------------------------------------------------------------ |
| `classId`      | The class whose trainer teaches it, or `0` for riding ranks and weapon skills.               |
| `classRaces`   | The races of each class that may buy it, for the entries no single class owns. Empty otherwise. |
| `reqLevel`     | Lowest level any trainer will sell it at.                                                    |
| `reqSkillRank` | Skill the trainer asks for first, which is how the riding ranks gate each other.             |
| `taughtSpells` | What the entry teaches when cast, for the few that wrap a spell rather than being one.        |
| `raceMask`     | Races that can learn it, `0` when every race can. Weapon skills carry theirs in `classRaces`. |
| `factions`     | `1` Alliance, `2` Horde, `0` when both teams' trainers teach it.                              |
| `expansion`    | `0` classic, `1` Outland, `2` Northrend: how far a seed must reach for the trainer.           |

A handful of entries wrap another spell rather than being one — a paladin's Judgement, the class
mounts, Flight Form. The trainer *casts* those instead of teaching them, so what comes out the other
side is listed in `taughtSpells` for the server module to hold back until its own item arrives.

Some names are shared by several spells: a mage and a druid both have a Remove Curse, and a couple of
paladin spells come in one copy per faction. Archipelago keys items and locations by name, so the
extractor logs every clash it finds and ArchipelaWoW qualifies those names on its side.

## Regenerating the entity model

Everything under [`Entities/`](Entities) is scaffolded from the world database and should not be edited
by hand; the hand-written navigations and keys live in [`EntityExtensions/`](EntityExtensions) as
partial classes instead. Only the 22 tables the extractor actually reads are generated. From the Visual
Studio Package Manager Console:

```powershell
Scaffold-DbContext 'Host=localhost;User=root;Password=root;Database=acore_world' Microting.EntityFrameworkCore.MySql `
  -Context WorldDbContext -NoOnConfiguring -DataAnnotations -Force `
  -ContextDir Entities -ContextNamespace ArchipelaWoW.DataExtractor.Entities `
  -OutputDir Entities/World -Namespace ArchipelaWoW.DataExtractor.Entities.World `
  -Tables quest_template,quest_template_addon,quest_poi,creature,creature_template,creature_queststarter,creature_questender,gameobject,gameobject_template,gameobject_queststarter,gameobject_questender,item_template,game_event_creature_quest,game_event_gameobject_quest,pool_quest,disables,trainer,trainer_spell,spell_ranks,creature_default_trainer,playercreateinfo_skills,conditions
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
