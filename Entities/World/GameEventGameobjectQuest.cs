using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

[PrimaryKey("Id", "Quest", "EventEntry")]
[Table("game_event_gameobject_quest")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class GameEventGameobjectQuest
{
    /// <summary>
    /// Entry of the game event
    /// </summary>
    [Key]
    [Column("eventEntry")]
    public byte EventEntry { get; set; }

    [Key]
    [Column("id")]
    public uint Id { get; set; }

    [Key]
    [Column("quest")]
    public uint Quest { get; set; }
}
