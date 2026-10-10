using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Loot System
/// </summary>
[PrimaryKey("Entry", "Item", "Reference", "GroupId")]
[Table("creature_loot_template")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class CreatureLootTemplate
{
    [Key]
    public uint Entry { get; set; }

    [Key]
    public uint Item { get; set; }

    [Key]
    public int Reference { get; set; }

    public float Chance { get; set; }

    public sbyte QuestRequired { get; set; }

    public ushort LootMode { get; set; }

    [Key]
    public byte GroupId { get; set; }

    public byte MinCount { get; set; }

    public byte MaxCount { get; set; }

    [StringLength(255)]
    public string Comment { get; set; }
}
