using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

[Table("pool_quest")]
[Index("Entry", Name = "idx_guid")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class PoolQuest
{
    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("pool_entry")]
    public uint PoolEntry { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; }
}
