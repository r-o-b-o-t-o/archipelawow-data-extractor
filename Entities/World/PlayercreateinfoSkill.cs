using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("RaceMask", "ClassMask", "Skill")]
[Table("playercreateinfo_skills")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class PlayercreateinfoSkill
{
    [Key]
    [Column("raceMask")]
    public uint RaceMask { get; set; }

    [Key]
    [Column("classMask")]
    public uint ClassMask { get; set; }

    [Key]
    [Column("skill")]
    public ushort Skill { get; set; }

    [Column("rank")]
    public ushort Rank { get; set; }

    [Column("comment")]
    [StringLength(255)]
    public string Comment { get; set; }
}
