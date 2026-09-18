# CLAUDE.md

.NET 10 console tool that reads an AzerothCore world database and the client DBCs, and writes the
`quests.json` and `spells.json` the ArchipelaWoW APWorld ships. `README.md` documents the setup, the
filtering rules and the output formats; read the relevant section before changing an extractor.

## Related repositories

- `archipelawow` — the APWorld that consumes the output, committed there as `data/quests.json` and
  `data/spells.json` (`OUT_DIR`). A change to the output shape needs a matching change in its
  `quest_model.py` / `spell_model.py`, and a change to what is extracted means regenerating and
  committing the extracts there.
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

- `dotnet build` for a compile check. A full `dotnet run` needs a populated world database and DBC
  directory configured in `.env` (see README).
- The quest extract needs the spawn zone columns populated (README, "Preparing the world database");
  without them `startZones` / `endZones` come out empty. The tool warns and continues, so never commit
  an extract produced with that warning.

## Commits

- Conventional Commits with concise messages; one logical change per commit.
