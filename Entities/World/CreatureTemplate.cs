using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

/// <summary>
/// Creature System
/// </summary>
[Table("creature_template")]
[Index("Name", Name = "idx_name")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class CreatureTemplate
{
    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("difficulty_entry_1")]
    public uint DifficultyEntry1 { get; set; }

    [Column("difficulty_entry_2")]
    public uint DifficultyEntry2 { get; set; }

    [Column("difficulty_entry_3")]
    public uint DifficultyEntry3 { get; set; }

    public uint KillCredit1 { get; set; }

    public uint KillCredit2 { get; set; }

    [Required]
    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; }

    [Column("subname")]
    [StringLength(100)]
    public string Subname { get; set; }

    [StringLength(100)]
    public string IconName { get; set; }

    [Column("gossip_menu_id")]
    public uint GossipMenuId { get; set; }

    [Column("minlevel")]
    public byte Minlevel { get; set; }

    [Column("maxlevel")]
    public byte Maxlevel { get; set; }

    [Column("exp")]
    public short Exp { get; set; }

    [Column("faction")]
    public ushort Faction { get; set; }

    [Column("npcflag")]
    public uint Npcflag { get; set; }

    /// <summary>
    /// Result of 2.5/2.5, most common value
    /// </summary>
    [Column("speed_walk")]
    public float SpeedWalk { get; set; }

    /// <summary>
    /// Result of 8.0/7.0, most common value
    /// </summary>
    [Column("speed_run")]
    public float SpeedRun { get; set; }

    [Column("speed_swim")]
    public float SpeedSwim { get; set; }

    [Column("speed_flight")]
    public float SpeedFlight { get; set; }

    [Column("detection_range")]
    public float DetectionRange { get; set; }

    [Column("rank")]
    public byte Rank { get; set; }

    [Column("dmgschool")]
    public sbyte Dmgschool { get; set; }

    public float DamageModifier { get; set; }

    public uint BaseAttackTime { get; set; }

    public uint RangeAttackTime { get; set; }

    public float BaseVariance { get; set; }

    public float RangeVariance { get; set; }

    [Column("unit_class")]
    public byte UnitClass { get; set; }

    [Column("unit_flags")]
    public uint UnitFlags { get; set; }

    [Column("unit_flags2")]
    public uint UnitFlags2 { get; set; }

    [Column("dynamicflags")]
    public uint Dynamicflags { get; set; }

    [Column("family")]
    public sbyte Family { get; set; }

    [Column("type")]
    public byte Type { get; set; }

    [Column("type_flags")]
    public uint TypeFlags { get; set; }

    [Column("lootid")]
    public uint Lootid { get; set; }

    [Column("pickpocketloot")]
    public uint Pickpocketloot { get; set; }

    [Column("skinloot")]
    public uint Skinloot { get; set; }

    public uint PetSpellDataId { get; set; }

    public uint VehicleId { get; set; }

    [Column("mingold")]
    public uint Mingold { get; set; }

    [Column("maxgold")]
    public uint Maxgold { get; set; }

    [Required]
    [Column("AIName")]
    [StringLength(64)]
    public string Ainame { get; set; }

    public byte MovementType { get; set; }

    public float HoverHeight { get; set; }

    public float HealthModifier { get; set; }

    public float ManaModifier { get; set; }

    public float ArmorModifier { get; set; }

    public float ExperienceModifier { get; set; }

    public byte RacialLeader { get; set; }

    [Column("movementId")]
    public uint MovementId { get; set; }

    public byte RegenHealth { get; set; }

    public int CreatureImmunitiesId { get; set; }

    [Column("flags_extra")]
    public uint FlagsExtra { get; set; }

    [Required]
    [StringLength(64)]
    public string ScriptName { get; set; }

    public int? VerifiedBuild { get; set; }
}
