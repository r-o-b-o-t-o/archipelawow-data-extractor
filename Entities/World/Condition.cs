using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Condition System
/// </summary>
[PrimaryKey("SourceTypeOrReferenceId", "SourceGroup", "SourceEntry", "SourceId", "ElseGroup", "ConditionTypeOrReference", "ConditionTarget", "ConditionValue1", "ConditionValue2", "ConditionValue3")]
[Table("conditions")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class Condition
{
    [Key]
    public int SourceTypeOrReferenceId { get; set; }

    [Key]
    public uint SourceGroup { get; set; }

    [Key]
    public int SourceEntry { get; set; }

    [Key]
    public int SourceId { get; set; }

    [Key]
    public uint ElseGroup { get; set; }

    [Key]
    public int ConditionTypeOrReference { get; set; }

    [Key]
    public byte ConditionTarget { get; set; }

    [Key]
    public uint ConditionValue1 { get; set; }

    [Key]
    public uint ConditionValue2 { get; set; }

    [Key]
    public uint ConditionValue3 { get; set; }

    public byte NegativeCondition { get; set; }

    public uint ErrorType { get; set; }

    public uint ErrorTextId { get; set; }

    [Required]
    [StringLength(64)]
    public string ScriptName { get; set; }

    [StringLength(255)]
    public string Comment { get; set; }
}
