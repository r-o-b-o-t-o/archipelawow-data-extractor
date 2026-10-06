using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[Table("instance_encounters")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class InstanceEncounter
{
    /// <summary>
    /// Unique entry from DungeonEncounter.dbc
    /// </summary>
    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("creditType")]
    public byte CreditType { get; set; }

    [Column("creditEntry")]
    public uint CreditEntry { get; set; }

    /// <summary>
    /// If not 0, LfgDungeon.dbc entry for the instance it is last encounter in
    /// </summary>
    [Column("lastEncounterDungeon")]
    public ushort LastEncounterDungeon { get; set; }

    [Required]
    [Column("comment")]
    [StringLength(255)]
    public string Comment { get; set; }
}
