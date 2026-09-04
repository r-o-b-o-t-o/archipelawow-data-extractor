using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Creature System
/// </summary>
[PrimaryKey("Id", "Quest")]
[Table("creature_queststarter")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class CreatureQueststarter
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
