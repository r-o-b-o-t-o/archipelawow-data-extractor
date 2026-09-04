using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("TrainerId", "SpellId")]
[Table("trainer_spell")]
[MySqlCollation("utf8mb4_general_ci")]
public partial class TrainerSpell
{
    [Key]
    public uint TrainerId { get; set; }

    [Key]
    public uint SpellId { get; set; }

    public uint MoneyCost { get; set; }

    public uint ReqSkillLine { get; set; }

    public uint ReqSkillRank { get; set; }

    public uint ReqAbility1 { get; set; }

    public uint ReqAbility2 { get; set; }

    public uint ReqAbility3 { get; set; }

    public byte ReqLevel { get; set; }

    public int? VerifiedBuild { get; set; }
}
