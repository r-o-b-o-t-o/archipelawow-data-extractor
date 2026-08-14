using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

/// <summary>
/// Creature System
/// </summary>
[Table("creature")]
[Index("Id", Name = "idx_id")]
[Index("Map", Name = "idx_map")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class Creature
{
    /// <summary>
    /// Global Unique Identifier
    /// </summary>
    [Key]
    [Column("guid")]
    public uint Guid { get; set; }

    /// <summary>
    /// Creature Identifier
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

    [Column("equipment_id")]
    public sbyte EquipmentId { get; set; }

    [Column("position_x")]
    public float PositionX { get; set; }

    [Column("position_y")]
    public float PositionY { get; set; }

    [Column("position_z")]
    public float PositionZ { get; set; }

    [Column("orientation")]
    public float Orientation { get; set; }

    [Column("spawntimesecs")]
    public uint Spawntimesecs { get; set; }

    [Column("wander_distance")]
    public float WanderDistance { get; set; }

    [Column("currentwaypoint")]
    public uint Currentwaypoint { get; set; }

    [Column("curhealth")]
    public uint Curhealth { get; set; }

    [Column("curmana")]
    public uint Curmana { get; set; }

    public byte MovementType { get; set; }

    [Column("npcflag")]
    public uint Npcflag { get; set; }

    [Column("unit_flags")]
    public uint UnitFlags { get; set; }

    [Column("dynamicflags")]
    public uint Dynamicflags { get; set; }

    [StringLength(64)]
    public string ScriptName { get; set; }

    public int? VerifiedBuild { get; set; }

    public byte CreateObject { get; set; }

    [Column(TypeName = "text")]
    public string Comment { get; set; }
}
