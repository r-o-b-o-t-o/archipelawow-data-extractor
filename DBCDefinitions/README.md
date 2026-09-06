# DBC definitions

The `.dbd` files in this directory describe the column layout of the client's DBC files per build.
They are used at compile time by [Roboto.Dbc.Generator](https://www.nuget.org/packages/Roboto.Dbc.Generator)
to generate the reader classes under the `ArchipelaWoW.DataExtractor.Dbc` namespace.

## Attribution

The definitions are taken unmodified from **[wowdev/WoWDBDefs](https://github.com/wowdev/WoWDBDefs)**,
copyright the WoWDBDefs contributors, and are licensed under the
[Creative Commons Attribution-ShareAlike 4.0 International License](LICENSE.md) (CC BY-SA 4.0).

Only the definitions this project actually reads are vendored here: `AreaTable`, `ChrClasses`,
`ChrRaces`, `FactionTemplate`, `QuestInfo`, `QuestSort`, `SkillLine`, `SkillLineAbility`,
`SkillRaceClassInfo`, `Spell` and `WorldMapArea`. To add another, copy the matching `.dbd` from the
upstream repository's `definitions/` directory — the build picks up every `.dbd` in this folder
automatically.
