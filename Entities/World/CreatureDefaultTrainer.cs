using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[Table("creature_default_trainer")]
[MySqlCollation("utf8mb4_general_ci")]
public partial class CreatureDefaultTrainer
{
    [Key]
    public uint CreatureId { get; set; }

    public uint TrainerId { get; set; }
}
