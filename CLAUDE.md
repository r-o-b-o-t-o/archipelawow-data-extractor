# CLAUDE.md

.NET 10 console tool that reads an AzerothCore world database and the client DBCs, and writes the
`quests.json`, `spells.json`, `skills.json`, `bosses.json` and `explorations.json` the ArchipelaWoW
APWorld ships, and, from an extracted client too, the maps, positions and icons of the launcher's tracker.
`README.md` documents the setup, the filtering rules and the output formats; read the relevant
section before changing an extractor.

## Related repositories

- `archipelawow` — the APWorld that consumes the output, committed there as `data/quests.json`,
  `data/spells.json`, `data/skills.json`, `data/bosses.json` and `data/explorations.json` (`OUT_DIR`). A
  change to the output shape needs a matching change in its `quest_model.py` / `spell_model.py` /
  `skill_model.py` / `boss_model.py` / `exploration_model.py`, and a change to what is extracted means
  regenerating and committing the extracts there.
- `archipelawow-launcher` — its tracker reads the tracker extracts, committed there under
  `ui/public/tracker` (`TRACKER_OUT_DIR`). A change to their shape needs a matching change in its
  `ui/src/tracker/types.ts`.
- `mod-i-found-your-sword` — the AzerothCore module.

## Code

- Write straightforward code. Skip minor edge cases (ones only reachable with malformed database or
  DBC data); point out notable ones (reachable with a stock AzerothCore database, or that would silently
  drop or misclassify entries) and let the user decide whether they are worth handling.
- Comment only when the code isn't obvious or there is an implication a future maintainer could easily
  miss. Keep comments brief and don't restate the code.
- `Entities/` is scaffolded by EF Core: never hand-edit it. Navigations and keys go in the partial
  classes under `EntityExtensions/`; adding a table means adding it to the scaffold command in the
  README and wiring it in `EntityExtensions/WorldDbContext.cs`.
- The tool only reads the database. Keep it that way.
- Nullable reference types are off, implicit usings are on; 4-space indent.

## Verifying changes

- `.env` overrides the environment: a run with `OUT_DIR` pointing at the APWorld rewrites its extracts.
- `dotnet build` for a compile check. A full `dotnet run` needs a populated world database and DBC
  directory configured in `.env` (see README).
- The extracts need the spawn zone columns populated (README, "Preparing the world database");
  without them `startZones` / `endZones` and the weapon and riding skills' `trainerZones` come out
  empty, and the tracker's positions with them. The tool warns and continues, so never commit an
  extract produced with that warning.
- Check the tracker extracts' images by eye, and their positions in the launcher's tracker.

## Commits

- Conventional Commits with concise messages; one logical change per commit.
