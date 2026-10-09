# ArchipelaWoW Data Extractor

![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/r-o-b-o-t-o/archipelawow-data-extractor/build.yml?branch=master)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Generates the `data/quests.json`, `data/spells.json`, `data/skills.json`, `data/bosses.json` and
`data/explorations.json` files consumed by [ArchipelaWoW](https://github.com/r-o-b-o-t-o/archipelawow),
a custom APWorld for the [Archipelago](https://archipelago.gg) randomizer framework.

The tool reads an [AzerothCore](https://www.azerothcore.org) world database and a 3.3.5a client's DBC
files, works out which quests, which trainable spells, which skills, which dungeon bosses and which
subzones make sense as randomizer locations, and writes each list as JSON. All five extracts are produced
in one run.

It can also write the data and images of the tracker in the
[ArchipelaWoW launcher](https://github.com/r-o-b-o-t-o/archipelawow-launcher): the world maps, where the
checks are on them, and icons, read from an extracted client as well.

## Contents

- [Requirements](#requirements)
- [Preparing the world database](#preparing-the-world-database)
- [Configuration](#configuration)
- [Running](#running)
- [Quests](#quests)
- [Spells](#spells)
- [Skills](#skills)
- [Bosses](#bosses)
- [Explorations](#explorations)
- [Tracker](#tracker)
- [Regenerating the entity model](#regenerating-the-entity-model)
- [Third-party data](#third-party-data)
- [License](#license)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A MySQL AzerothCore world database, reachable and already populated
- A 3.3.5a `dbc` directory, such as the one AzerothCore needs at `data/dbc`
- For the tracker extracts only, a 3.3.5a client's `Interface` folder, extracted from its MPQ archives

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

Without this step most quests are extracted with empty `startZones` and `endZones`, and weapon skills
and riding ranks with empty `trainerZones`. The extractor logs a warning when it detects that, but it
will not stop.

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
| `TRACKER_OUT_DIR`   | no       | —                | Directory the tracker extracts are written to: the launcher's `ui/public/tracker`. Skipped when unset. |
| `CLIENT_DIRECTORY`  | with `TRACKER_OUT_DIR` | — | Extracted client folder holding `Interface`, such as `Data_enUS`.                 |

## Running

```sh
dotnet run
```

The tool logs every quest and boss it drops along with the reason, then writes `quests.json`,
`spells.json`, `skills.json`, `bosses.json` and `explorations.json` to `OUT_DIR`, creating the directory
if needed, and the tracker extracts to `TRACKER_OUT_DIR` when it is set.

Settings in `.env` take precedence over the environment, so set `OUT_DIR` and `TRACKER_OUT_DIR` in `.env`
itself.

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
otherwise. `races` also leaves out the races a quest giver attacks on sight, and a quest started only by
items flagged for one faction, such as "Resting in Pieces", keeps only that faction's races, as the core
does not let the other one loot them. A few quests whose givers the faction relations open to the other
faction by mistake, such as "Hilary's Necklace" in Lakeshire, are limited to their faction by hand, as
are the only two quests the Alliance could take in Durotar, so that zone stays Horde-only.

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
  that ladder. Both carry `trainerZones`: a racial riding trainer only teaches its own race, and the
  ones a night elf, a draenei and an orc learn from stand in Darnassus, The Exodar and Orgrimmar rather
  than in their starting zones. The race comes from the conditions on the trainer's gossip option, read
  without the alternative that lets in a player exalted with the trainer's faction.
- **Weapon skills** (`"weapon"`) — the weapon proficiencies a weapon master sells. A weapon master is
  keyed by no class at all, so these carry `classRaces` rather than `classId`: who may buy one is read
  off `SkillLineAbility.dbc` and `SkillRaceClassInfo.dbc` the way `Player::IsSpellFitByClassAndRace`
  does, and whoever is created already holding the skill is left out. Both halves matter — Thrown names
  no class in its skill line entry, and only `SkillRaceClassInfo.dbc` says it belongs to warriors,
  hunters and rogues — and so does race: a dwarf hunter starts with Guns and a troll one with Bows, so
  each is sold what the other started with. They also carry `trainerZones`, the zones their weapon
  masters stand in: a class trainer is in every starting zone, but a weapon master only in the capital
  cities and Eversong Woods, so ArchipelaWoW gates these checks on reaching one of those zones. Each zone
  lists the races its trainers will teach, leaving out any their faction is hostile to: Thunder Bluff's
  weapon master teaches the Horde only.
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
[`CharacterSkills`](Services/CharacterSkills.cs), the one place to change if that ever moves.

### Output

`spells.json` is an array sorted by class, then by required level, then by id:

```json
{
  "id": 403,
  "name": "Lightning Bolt",
  "classId": 7,
  "classRaces": {},
  "trainerZones": {},
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
| `trainerZones` | For weapon and mount trainer entries, `AreaTable.dbc` zone id to the races taught there, `0` for all. |
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

## Skills

Raising a skill is a location in ArchipelaWoW: the weapon skills and Defense rise with use, up to five
times the character's level. The extract says which skills there are, and which races of each class are
created holding one, since those need no item to raise it. A character not created holding a skill buys
it from a weapon master, which is the weapon skill of `spells.json` named by `spell`, and whose
`classRaces` says who may.

### What becomes an entry

Every skill line of `SkillLine.dbc`'s Weapon Skills category, the one the character pane lists Defense
under, except two: Dual Wield, whose `SkillRaceClassInfo.dbc` entries flag it as always at its cap, so it
is never raised, and Unarmed, which rises by fighting with no weapon in hand. Which races of each class are created holding a skill is worked out the same way
as for the weapon skills of `spells.json`.

### Output

`skills.json` is an array sorted by id:

```json
{
  "id": 43,
  "name": "Swords",
  "spell": 201,
  "startingClassRaces": { "1": 1279, "2": 1541, "3": 1791 }
}
```

| Field                | Meaning                                                                                  |
| -------------------- | ---------------------------------------------------------------------------------------- |
| `id`                 | The `SkillLine.dbc` id, which the server module checks the locations on.                  |
| `spell`              | The weapon proficiency a weapon master sells for it, `0` for Defense.                     |
| `startingClassRaces` | The races of each class created holding it, keyed by class id. Classes with none are left out. |

## Bosses

Defeating a dungeon boss is a location in ArchipelaWoW when a seed asks for it. A boss is an encounter of
`DungeonEncounter.dbc`, and its id is what the server module checks the location on: the core credits an
encounter through the world database's `instance_encounters`, when the creature it names dies or a
script casts the spell it names. That also covers the bosses no single kill stands for, such as the Ring
of Law in Blackrock Depths or the Tribunal of Ages in Halls of Stone.

### What becomes an entry

Every encounter of a 5-player dungeon (a `Map.dbc` instance type of `1`) at normal difficulty. A heroic
encounter is a row of its own with an id of its own, and is left out. ArchipelaWoW only uses the dungeons
it has a zone item for, so Trial of the Champion and the three Frozen Halls are extracted but go unused.

Dropped along the way:

- encounters `instance_encounters` has no row for, which the core never credits
- bosses summoned with an item that comes from outside their dungeon, which the dungeon's zone item
  alone does not lead to: Kirtonos the Herald, Avatar of Hakkar and Gahz'rilla. Urok Doomhowl stays, as
  both items his summoning uses come from the same dungeon: Omokk's Head drops from Highlord Omokk, and
  the Roughshod Pike from a chest anyone can open
- the Violet Hold's First and Second Prisoner, which the core only credits for Erekem and Moragg, two of
  the six prisoners a run picks from at random

The few names `DungeonEncounter.dbc` misspells, such as "Salram the Fleshcrafter", are corrected by hand.

### Output

`bosses.json` is an array sorted by map, then in the order `DungeonEncounter.dbc` lists each dungeon's
encounters:

```json
{
  "id": 167,
  "name": "Edwin VanCleef",
  "map": { "id": 36, "name": "Deadmines" }
}
```

`map` is the dungeon's `Map.dbc` id and name. ArchipelaWoW matches it to the map its dungeon's zone
teleports into.

## Explorations

Discovering a subzone is a location in ArchipelaWoW when a seed asks for it. A subzone is a criterion of
an exploration achievement in `Achievement_Criteria.dbc`, and its id is what the server module checks the
location on: the core credits it the first time the character discovers one of the areas of the
`WorldMapOverlay.dbc` entry it names.

### What becomes an entry

Every "explore area" criterion of the achievements in the Exploration category and the categories under
it: the subzones of "Explore Elwynn Forest" and its kind. The category's achievements that ask for
something else, such as other achievements or kills, add none. Nothing is filtered out: ArchipelaWoW
only uses the subzones of the zones it has a zone item for, so those of Moonglade or Deadwind Pass are
extracted but go unused.

The few names `Achievement_Criteria.dbc` misspells, such as "Ampitheater of Anguish", are corrected by
hand to the spelling of `AreaTable.dbc`.

### Output

`explorations.json` is an array sorted by achievement id, then in the order the achievement lists its
criteria:

```json
{
  "id": 1149,
  "name": "Stormwind City",
  "zone": { "id": 1519, "name": "Stormwind City" }
}
```

`name` is the criterion's, as the achievement shows it. `zone` is the `AreaTable.dbc` zone the
subzone's areas lie in, which is the achievement's zone for all but the capitals some of them ask for:
Elwynn Forest's lists Stormwind City, Terokkar Forest's Shattrath City.

## Tracker

The launcher's tracker shows a seed's checks on the game's world maps. The rules come from the seed
itself, which carries them in its slot data; what the extractor writes is everything else: the maps,
where the checks of `quests.json`, `spells.json`, `bosses.json` and `explorations.json` are on them, and
the icons to draw them and those of `skills.json` with. It reads those five extracts back from `OUT_DIR`,
so they are written first.

Everything goes to `TRACKER_OUT_DIR`, images as WebP:

| Path               | Contents                                                                                 |
| ------------------ | ---------------------------------------------------------------------------------------- |
| `maps.json`        | The maps, from the cosmic map down to the cities                                          |
| `maps/`            | Each map put together from its tiles, fully explored                                      |
| `highlights/`      | What lights up on a map when the pointer is over one of its children                      |
| `quests.json`      | Where each quest is picked up                                                             |
| `flightpaths.json` | The flight masters, by `TaxiNodes.dbc` id: the taxi nodes with one standing by            |
| `dungeons.json`    | The 5-player dungeons' entrances and encounters, by `Map.dbc` id                          |
| `explorations.json` | Where each subzone is, and its achievement's icon, by `Achievement_Criteria.dbc` id      |
| `spells.json`      | The spells' icons                                                                         |
| `skills.json`      | The skills' names and icons, by `SkillLine.dbc` id                                        |
| `achievements.json`, `items.json` | Icons, and an achievement's name and dungeon                               |
| `icons/`           | Every icon those name, as `<lowercase name>.webp`                                         |
| `ui/`              | Marker images, and the class and race icons as `class_<id>.webp` and `race_<id>.webp`     |
| `characters.json`  | The names of those classes and races, and each race's side, by `ChrClasses.dbc` and `ChrRaces.dbc` id |

Files are overwritten but never deleted: delete the folder first when an icon or a map goes away.

### Positions

A spot on a map is `[map, x, y]`: a `WorldMapArea.dbc` id, and where on that map from 0 to 1, left to
right and top to bottom, the game's own map coordinates divided by 100. It is the map of the zone or
city the spawn stands in, by the spawn tables' zone columns (see
[Preparing the world database](#preparing-the-world-database)). A spot with no zone of its own, such as
a taxi node or an area trigger, takes the zone of the closest spawn that has one: the spawns around an
instance's entrance are filed under the instance's zone, which has no map. Spawns inside an instance are
placed at its entrance.

`quests.json` is keyed by quest id. `givers` lists where the quest givers spawn, up to 12 spots, with a
fourth element when only one side can talk to the giver: `1` Alliance, `2` Horde. A quest started by an
item has `"atEnder": true` and lists where it is turned in instead. A quest with neither has its zone's
map as `map`, when it has one:

```json
{
  "2": { "givers": [[43, 0.7378, 0.6146, 2]], "atEnder": true },
  "7": { "givers": [[30, 0.4892, 0.4161, 1]] },
  "9418": { "map": 465 }
}
```

`dungeons.json` places each dungeon at the area triggers leading inside, or at its `Map.dbc` corpse
position for the ones with no trigger on a continent, and lists the encounters of `bosses.json` in it.

`explorations.json` places each subzone on its zone's map, in the middle of the `WorldMapOverlay.dbc`
explored area its criterion names: the centroid of the area's shape, or the point of the shape closest
to it when the shape bends around it. The few explored areas with no texture are placed in the middle of
the hit rectangle `WorldMapOverlay.dbc` gives them instead.

```json
{
  "1149": { "position": [30, 0.251, 0.2822], "icon": "achievement_zone_elwynnforest" }
}
```

### Maps

`maps.json` is an array of maps. Ids are `WorldMapArea.dbc` ids, and the game's own numbers for the two
maps that file has no row for: `0` for Azeroth's world map and `-1` for the cosmic map.

```json
{
  "id": 30,
  "name": "Elwynn Forest",
  "kind": "zone",
  "parent": 14,
  "image": "maps/elwynn.webp",
  "continent": 0,
  "bounds": [1535.4166, -1935.4166, -7939.583, -10254.166],
  "center": [0.5187, 0.542],
  "highlight": { "image": "highlights/elwynn.webp", "blend": "add", "rect": [0.40835, 0.70409, 0.08519, 0.08525] }
}
```

| Field       | Meaning                                                                                      |
| ----------- | -------------------------------------------------------------------------------------------- |
| `kind`      | `cosmic`, `world`, `continent`, `zone` or `city`.                                              |
| `continent` | The map id of the continent the map is drawn on: the blood elf and draenei lands are part of Outland's map but drawn on Azeroth's continents. |
| `bounds`    | What the map covers, in world coordinates on that continent: left, right, top and bottom.      |
| `center`    | Where a zone's checks with no spot of their own gather: the middle of its highlight.            |
| `highlight` | What lights up on the parent map, where it is drawn there (`rect`: x, y, width and height from 0 to 1), and how, as the game's alphaMode (`blend`): `add` adds the image's colour onto the map, black adding nothing; `blend` draws it over the map through its alpha. |
| `hit`       | Where the map opens from on its parent, for the ones whose highlight is an outline rather than a shape. |
| `zones`     | A continent's zones, as the game tells which one the pointer is over: a grid of zone ids over its map (`rect`, as a highlight's), `columns` wide and row by row, `0` where there is none. A city's cells are its zone's. |

The images and placements come from the client. A zone's highlight is the game's `<zone>Highlight`
texture, stretched over the zone's bounds on its continent. A city opens from its zone, through the
zone's explored area that covers it, which lights up as the game has no highlight for it. The cosmic
map's two buttons are read from FrameXML's `WorldMapFrame.xml`. A continent's zones come from its `.zmp`,
the game's grid of `AreaTable.dbc` ids over the continent, half an ADT tile per cell, which it reads the
zone under the pointer from, on a zone's map too: Hrothgar's Landing came after Northrend's, and takes
the empty cells its highlight lights up. The class and race icons are cut out of the character creation
screen's with the coordinates in FrameXML and GlueXML. Only the placement of the continents on Azeroth's
world map is in no file: the game works it out in code from `WorldMapContinent.dbc`, and the scale it
uses was measured against in-game screenshots (`WORLD_MAP_SCALE` in
[`TrackerMapExtractorService`](Services/TrackerMapExtractorService.cs)).

### Icons

`achievements.json` covers the General, Quests, Exploration and Dungeons & Raids categories, where
ArchipelaWoW's achievements come from, and whose exploration and dungeon achievements lend most of its
zone items their icons. An achievement's `map` is its dungeon's `Map.dbc` id: `Achievement.dbc` names it
for few of the classic and Outland dungeons' own, which take that of the encounter their boss kill credits
(`instance_encounters`) instead. `items.json` covers the glyphs, bags and heirlooms. The icons no extract names
are listed in `TRACKER_ICONS` in [`TrackerExtractorService`](Services/TrackerExtractorService.cs): the
progressive items', gold's and random gear's, and those of the zone items no achievement lends one to,
such as the capital cities' mage teleports. A zone item given an icon of its own in ArchipelaWoW's
`items/zones.py` needs it added there. A skill takes the icon of the weapon proficiency that teaches it,
and Defense, which no weapon master sells, its own from `SKILL_ICONS` in the same file.

## Regenerating the entity model

Everything under [`Entities/`](Entities) is scaffolded from the world database and should not be edited
by hand; the hand-written navigations and keys live in [`EntityExtensions/`](EntityExtensions) as
partial classes instead. Only the 25 tables the extractor actually reads are generated. From the Visual
Studio Package Manager Console:

```powershell
Scaffold-DbContext 'Host=localhost;User=root;Password=root;Database=acore_world' Microting.EntityFrameworkCore.MySql `
  -Context WorldDbContext -NoOnConfiguring -DataAnnotations -Force `
  -ContextDir Entities -ContextNamespace ArchipelaWoW.DataExtractor.Entities `
  -OutputDir Entities/World -Namespace ArchipelaWoW.DataExtractor.Entities.World `
  -Tables quest_template,quest_template_addon,quest_poi,creature,creature_template,creature_queststarter,creature_questender,gameobject,gameobject_template,gameobject_queststarter,gameobject_questender,item_template,game_event_creature_quest,game_event_gameobject_quest,pool_quest,disables,trainer,trainer_spell,spell_ranks,creature_default_trainer,playercreateinfo_skills,conditions,gossip_menu_option,instance_encounters,areatrigger_teleport
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
