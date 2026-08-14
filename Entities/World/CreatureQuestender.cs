using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

/// <summary>
/// Creature System
/// </summary>
[PrimaryKey("Id", "Quest")]
[Table("creature_questender")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class CreatureQuestender
{
    /// <summary>
    /// Identifier
    /// </summary>
    [Key]
    [Column("id")]
    public uint Id { get; set; }

    /// <summary>
    /// Quest Identifier
    /// </summary>
    [Key]
    [Column("quest")]
    public uint Quest { get; set; }
}
