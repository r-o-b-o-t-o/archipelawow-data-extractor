using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Gameobject System
/// </summary>
[Table("gameobject_template")]
[Index("Name", Name = "idx_name")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class GameobjectTemplate
{
    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("type")]
    public byte Type { get; set; }

    [Column("displayId")]
    public uint DisplayId { get; set; }

    [Required]
    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [StringLength(100)]
    public string IconName { get; set; }

    [Required]
    [Column("castBarCaption")]
    [StringLength(100)]
    public string CastBarCaption { get; set; }

    [Required]
    [Column("unk1")]
    [StringLength(100)]
    public string Unk1 { get; set; }

    [Column("size")]
    public float Size { get; set; }

    public uint Data0 { get; set; }

    public int Data1 { get; set; }

    public uint Data2 { get; set; }

    public uint Data3 { get; set; }

    public uint Data4 { get; set; }

    public uint Data5 { get; set; }

    public int Data6 { get; set; }

    public uint Data7 { get; set; }

    public uint Data8 { get; set; }

    public uint Data9 { get; set; }

    public uint Data10 { get; set; }

    public uint Data11 { get; set; }

    public uint Data12 { get; set; }

    public uint Data13 { get; set; }

    public uint Data14 { get; set; }

    public uint Data15 { get; set; }

    public uint Data16 { get; set; }

    public uint Data17 { get; set; }

    public uint Data18 { get; set; }

    public uint Data19 { get; set; }

    public uint Data20 { get; set; }

    public uint Data21 { get; set; }

    public uint Data22 { get; set; }

    public uint Data23 { get; set; }

    [Required]
    [Column("AIName")]
    [StringLength(64)]
    public string Ainame { get; set; }

    [Required]
    [StringLength(64)]
    public string ScriptName { get; set; }

    public int? VerifiedBuild { get; set; }
}
