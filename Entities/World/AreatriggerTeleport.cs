using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

/// <summary>
/// Trigger System
/// </summary>
[Table("areatrigger_teleport")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class AreatriggerTeleport
{
    [Key]
    [Column("ID")]
    public uint Id { get; set; }

    [Column(TypeName = "text")]
    public string Name { get; set; }

    [Column("target_map")]
    public ushort TargetMap { get; set; }

    [Column("target_position_x")]
    public float TargetPositionX { get; set; }

    [Column("target_position_y")]
    public float TargetPositionY { get; set; }

    [Column("target_position_z")]
    public float TargetPositionZ { get; set; }

    [Column("target_orientation")]
    public float TargetOrientation { get; set; }
}
