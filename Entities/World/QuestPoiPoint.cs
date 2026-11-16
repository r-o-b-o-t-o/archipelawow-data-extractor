using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("QuestId", "Idx1", "Idx2")]
[Table("quest_poi_points")]
[Index("QuestId", "Idx1", Name = "questId_id")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class QuestPoiPoint
{
    [Key]
    [Column("QuestID")]
    public uint QuestId { get; set; }

    [Key]
    public uint Idx1 { get; set; }

    [Key]
    public uint Idx2 { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int? VerifiedBuild { get; set; }
}
