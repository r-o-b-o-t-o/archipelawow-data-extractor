using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Spell Rank Data
/// </summary>
[PrimaryKey("FirstSpellId", "Rank")]
[Table("spell_ranks")]
[Index("SpellId", Name = "spell_id", IsUnique = true)]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class SpellRank
{
    [Key]
    [Column("first_spell_id")]
    public uint FirstSpellId { get; set; }

    [Column("spell_id")]
    public uint SpellId { get; set; }

    [Key]
    [Column("rank")]
    public byte Rank { get; set; }
}
