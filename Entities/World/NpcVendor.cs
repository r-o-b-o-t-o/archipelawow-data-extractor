using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Npc System
/// </summary>
[PrimaryKey("Entry", "Item", "ExtendedCost")]
[Table("npc_vendor")]
[Index("Slot", Name = "slot")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class NpcVendor
{
    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("slot")]
    public short Slot { get; set; }

    [Key]
    [Column("item")]
    public int Item { get; set; }

    [Column("maxcount")]
    public uint Maxcount { get; set; }

    [Column("incrtime")]
    public uint Incrtime { get; set; }

    [Key]
    public uint ExtendedCost { get; set; }

    public int? VerifiedBuild { get; set; }
}
