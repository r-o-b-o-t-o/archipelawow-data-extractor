using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

/// <summary>
/// Gameobject System
/// </summary>
[Table("gameobject")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class Gameobject
{
    /// <summary>
    /// Global Unique Identifier
    /// </summary>
    [Key]
    [Column("guid")]
    public uint Guid { get; set; }

    /// <summary>
    /// Gameobject Identifier
    /// </summary>
    [Column("id")]
    public uint Id { get; set; }

    /// <summary>
    /// Map Identifier
    /// </summary>
    [Column("map")]
    public ushort Map { get; set; }

    /// <summary>
    /// Zone Identifier
    /// </summary>
    [Column("zoneId")]
    public ushort ZoneId { get; set; }

    /// <summary>
    /// Area Identifier
    /// </summary>
    [Column("areaId")]
    public ushort AreaId { get; set; }

    [Column("spawnMask")]
    public byte SpawnMask { get; set; }

    [Column("phaseMask")]
    public uint PhaseMask { get; set; }

    [Column("position_x")]
    public float PositionX { get; set; }

    [Column("position_y")]
    public float PositionY { get; set; }

    [Column("position_z")]
    public float PositionZ { get; set; }

    [Column("orientation")]
    public float Orientation { get; set; }

    [Column("rotation0")]
    public float Rotation0 { get; set; }

    [Column("rotation1")]
    public float Rotation1 { get; set; }

    [Column("rotation2")]
    public float Rotation2 { get; set; }

    [Column("rotation3")]
    public float Rotation3 { get; set; }

    [Column("spawntimesecs")]
    public int Spawntimesecs { get; set; }

    [Column("animprogress")]
    public byte Animprogress { get; set; }

    [Column("state")]
    public byte State { get; set; }

    [StringLength(64)]
    public string ScriptName { get; set; }

    public int? VerifiedBuild { get; set; }

    [Column(TypeName = "text")]
    public string Comment { get; set; }
}
