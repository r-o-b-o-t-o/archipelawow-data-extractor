using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("Id", "Quest")]
[Table("gameobject_queststarter")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class GameobjectQueststarter
{
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
