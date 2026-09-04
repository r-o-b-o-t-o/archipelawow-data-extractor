using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("SourceType", "Entry")]
[Table("disables")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class Disable
{
    [Key]
    [Column("sourceType")]
    public uint SourceType { get; set; }

    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("flags")]
    public byte Flags { get; set; }

    [Required]
    [Column("params_0")]
    [StringLength(255)]
    public string Params0 { get; set; }

    [Required]
    [Column("params_1")]
    [StringLength(255)]
    public string Params1 { get; set; }

    [Required]
    [Column("comment")]
    [StringLength(255)]
    public string Comment { get; set; }
}
