using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("Id", "Quest")]
[Table("game_event_creature_quest")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class GameEventCreatureQuest
{
    /// <summary>
    /// Entry of the game event.
    /// </summary>
    [Column("eventEntry")]
    public byte EventEntry { get; set; }

    [Key]
    [Column("id")]
    public uint Id { get; set; }

    [Key]
    [Column("quest")]
    public uint Quest { get; set; }
}
