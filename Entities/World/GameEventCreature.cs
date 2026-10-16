using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("Guid", "EventEntry")]
[Table("game_event_creature")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class GameEventCreature
{
    /// <summary>
    /// Entry of the game event. Put negative entry to remove during event.
    /// </summary>
    [Key]
    [Column("eventEntry")]
    public short EventEntry { get; set; }

    [Key]
    [Column("guid")]
    public uint Guid { get; set; }
}
