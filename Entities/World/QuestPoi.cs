using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("QuestId", "Id")]
[Table("quest_poi")]
[Index("QuestId", "Id", Name = "idx")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class QuestPoi
{
    [Key]
    [Column("QuestID")]
    public uint QuestId { get; set; }

    [Key]
    [Column("id")]
    public uint Id { get; set; }

    public int ObjectiveIndex { get; set; }

    [Column("MapID")]
    public uint MapId { get; set; }

    public uint WorldMapAreaId { get; set; }

    public uint Floor { get; set; }

    public uint Priority { get; set; }

    public uint Flags { get; set; }

    public int? VerifiedBuild { get; set; }
}
