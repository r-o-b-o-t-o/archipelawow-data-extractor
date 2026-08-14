using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

/// <summary>
/// Item System
/// </summary>
[Table("item_template")]
[Index("Class", Name = "items_index")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class ItemTemplate
{
    [Key]
    [Column("entry")]
    public uint Entry { get; set; }

    [Column("class")]
    public byte Class { get; set; }

    [Column("subclass")]
    public byte Subclass { get; set; }

    public sbyte SoundOverrideSubclass { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; }

    [Column("displayid")]
    public uint Displayid { get; set; }

    public byte Quality { get; set; }

    public uint Flags { get; set; }

    public uint FlagsExtra { get; set; }

    public byte BuyCount { get; set; }

    public long BuyPrice { get; set; }

    public uint SellPrice { get; set; }

    public byte InventoryType { get; set; }

    public int AllowableClass { get; set; }

    public int AllowableRace { get; set; }

    public ushort ItemLevel { get; set; }

    public byte RequiredLevel { get; set; }

    public ushort RequiredSkill { get; set; }

    public ushort RequiredSkillRank { get; set; }

    [Column("requiredspell")]
    public uint Requiredspell { get; set; }

    [Column("requiredhonorrank")]
    public uint Requiredhonorrank { get; set; }

    public uint RequiredCityRank { get; set; }

    public ushort RequiredReputationFaction { get; set; }

    public ushort RequiredReputationRank { get; set; }

    [Column("maxcount")]
    public int Maxcount { get; set; }

    [Column("stackable")]
    public int? Stackable { get; set; }

    public byte ContainerSlots { get; set; }

    [Column("stat_type1")]
    public byte StatType1 { get; set; }

    [Column("stat_value1")]
    public int StatValue1 { get; set; }

    [Column("stat_type2")]
    public byte StatType2 { get; set; }

    [Column("stat_value2")]
    public int StatValue2 { get; set; }

    [Column("stat_type3")]
    public byte StatType3 { get; set; }

    [Column("stat_value3")]
    public int StatValue3 { get; set; }

    [Column("stat_type4")]
    public byte StatType4 { get; set; }

    [Column("stat_value4")]
    public int StatValue4 { get; set; }

    [Column("stat_type5")]
    public byte StatType5 { get; set; }

    [Column("stat_value5")]
    public int StatValue5 { get; set; }

    [Column("stat_type6")]
    public byte StatType6 { get; set; }

    [Column("stat_value6")]
    public int StatValue6 { get; set; }

    [Column("stat_type7")]
    public byte StatType7 { get; set; }

    [Column("stat_value7")]
    public int StatValue7 { get; set; }

    [Column("stat_type8")]
    public byte StatType8 { get; set; }

    [Column("stat_value8")]
    public int StatValue8 { get; set; }

    [Column("stat_type9")]
    public byte StatType9 { get; set; }

    [Column("stat_value9")]
    public int StatValue9 { get; set; }

    [Column("stat_type10")]
    public byte StatType10 { get; set; }

    [Column("stat_value10")]
    public int StatValue10 { get; set; }

    public short ScalingStatDistribution { get; set; }

    public uint ScalingStatValue { get; set; }

    [Column("dmg_min1")]
    public float DmgMin1 { get; set; }

    [Column("dmg_max1")]
    public float DmgMax1 { get; set; }

    [Column("dmg_type1")]
    public byte DmgType1 { get; set; }

    [Column("dmg_min2")]
    public float DmgMin2 { get; set; }

    [Column("dmg_max2")]
    public float DmgMax2 { get; set; }

    [Column("dmg_type2")]
    public byte DmgType2 { get; set; }

    [Column("armor")]
    public uint Armor { get; set; }

    [Column("holy_res")]
    public short? HolyRes { get; set; }

    [Column("fire_res")]
    public short? FireRes { get; set; }

    [Column("nature_res")]
    public short? NatureRes { get; set; }

    [Column("frost_res")]
    public short? FrostRes { get; set; }

    [Column("shadow_res")]
    public short? ShadowRes { get; set; }

    [Column("arcane_res")]
    public short? ArcaneRes { get; set; }

    [Column("delay")]
    public ushort Delay { get; set; }

    [Column("ammo_type")]
    public byte AmmoType { get; set; }

    public float RangedModRange { get; set; }

    [Column("spellid_1")]
    public int Spellid1 { get; set; }

    [Column("spelltrigger_1")]
    public byte Spelltrigger1 { get; set; }

    [Column("spellcharges_1")]
    public short Spellcharges1 { get; set; }

    [Column("spellppmRate_1")]
    public float SpellppmRate1 { get; set; }

    [Column("spellcooldown_1")]
    public int Spellcooldown1 { get; set; }

    [Column("spellcategory_1")]
    public ushort Spellcategory1 { get; set; }

    [Column("spellcategorycooldown_1")]
    public int Spellcategorycooldown1 { get; set; }

    [Column("spellid_2")]
    public int Spellid2 { get; set; }

    [Column("spelltrigger_2")]
    public byte Spelltrigger2 { get; set; }

    [Column("spellcharges_2")]
    public short Spellcharges2 { get; set; }

    [Column("spellppmRate_2")]
    public float SpellppmRate2 { get; set; }

    [Column("spellcooldown_2")]
    public int Spellcooldown2 { get; set; }

    [Column("spellcategory_2")]
    public ushort Spellcategory2 { get; set; }

    [Column("spellcategorycooldown_2")]
    public int Spellcategorycooldown2 { get; set; }

    [Column("spellid_3")]
    public int Spellid3 { get; set; }

    [Column("spelltrigger_3")]
    public byte Spelltrigger3 { get; set; }

    [Column("spellcharges_3")]
    public short Spellcharges3 { get; set; }

    [Column("spellppmRate_3")]
    public float SpellppmRate3 { get; set; }

    [Column("spellcooldown_3")]
    public int Spellcooldown3 { get; set; }

    [Column("spellcategory_3")]
    public ushort Spellcategory3 { get; set; }

    [Column("spellcategorycooldown_3")]
    public int Spellcategorycooldown3 { get; set; }

    [Column("spellid_4")]
    public int Spellid4 { get; set; }

    [Column("spelltrigger_4")]
    public byte Spelltrigger4 { get; set; }

    [Column("spellcharges_4")]
    public short Spellcharges4 { get; set; }

    [Column("spellppmRate_4")]
    public float SpellppmRate4 { get; set; }

    [Column("spellcooldown_4")]
    public int Spellcooldown4 { get; set; }

    [Column("spellcategory_4")]
    public ushort Spellcategory4 { get; set; }

    [Column("spellcategorycooldown_4")]
    public int Spellcategorycooldown4 { get; set; }

    [Column("spellid_5")]
    public int Spellid5 { get; set; }

    [Column("spelltrigger_5")]
    public byte Spelltrigger5 { get; set; }

    [Column("spellcharges_5")]
    public short Spellcharges5 { get; set; }

    [Column("spellppmRate_5")]
    public float SpellppmRate5 { get; set; }

    [Column("spellcooldown_5")]
    public int Spellcooldown5 { get; set; }

    [Column("spellcategory_5")]
    public ushort Spellcategory5 { get; set; }

    [Column("spellcategorycooldown_5")]
    public int Spellcategorycooldown5 { get; set; }

    [Column("bonding")]
    public byte Bonding { get; set; }

    [Required]
    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; }

    public uint PageText { get; set; }

    [Column("LanguageID")]
    public byte LanguageId { get; set; }

    public byte PageMaterial { get; set; }

    [Column("startquest")]
    public uint Startquest { get; set; }

    [Column("lockid")]
    public uint Lockid { get; set; }

    public sbyte Material { get; set; }

    [Column("sheath")]
    public byte Sheath { get; set; }

    public int RandomProperty { get; set; }

    public uint RandomSuffix { get; set; }

    [Column("block")]
    public uint Block { get; set; }

    [Column("itemset")]
    public uint Itemset { get; set; }

    public ushort MaxDurability { get; set; }

    [Column("area")]
    public uint Area { get; set; }

    public short Map { get; set; }

    public int BagFamily { get; set; }

    public int TotemCategory { get; set; }

    [Column("socketColor_1")]
    public sbyte SocketColor1 { get; set; }

    [Column("socketContent_1")]
    public int SocketContent1 { get; set; }

    [Column("socketColor_2")]
    public sbyte SocketColor2 { get; set; }

    [Column("socketContent_2")]
    public int SocketContent2 { get; set; }

    [Column("socketColor_3")]
    public sbyte SocketColor3 { get; set; }

    [Column("socketContent_3")]
    public int SocketContent3 { get; set; }

    [Column("socketBonus")]
    public int SocketBonus { get; set; }

    public int GemProperties { get; set; }

    public short RequiredDisenchantSkill { get; set; }

    public float ArmorDamageModifier { get; set; }

    [Column("duration")]
    public uint Duration { get; set; }

    public short ItemLimitCategory { get; set; }

    public uint HolidayId { get; set; }

    [Required]
    [StringLength(64)]
    public string ScriptName { get; set; }

    [Column("DisenchantID")]
    public uint DisenchantId { get; set; }

    public byte FoodType { get; set; }

    [Column("minMoneyLoot")]
    public uint MinMoneyLoot { get; set; }

    [Column("maxMoneyLoot")]
    public uint MaxMoneyLoot { get; set; }

    [Column("flagsCustom")]
    public uint FlagsCustom { get; set; }

    public int? VerifiedBuild { get; set; }
}
