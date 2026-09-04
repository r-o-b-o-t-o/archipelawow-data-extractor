using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[Table("trainer")]
[MySqlCollation("utf8mb4_general_ci")]
public partial class Trainer
{
    [Key]
    public uint Id { get; set; }

    public byte Type { get; set; }

    [Column(TypeName = "mediumint unsigned")]
    public uint Requirement { get; set; }

    [Column(TypeName = "mediumtext")]
    public string Greeting { get; set; }

    public int? VerifiedBuild { get; set; }
}
