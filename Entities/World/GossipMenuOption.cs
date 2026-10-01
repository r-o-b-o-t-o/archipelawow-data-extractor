using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Entities.World;

[PrimaryKey("MenuId", "OptionId")]
[Table("gossip_menu_option")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class GossipMenuOption
{
    [Key]
    [Column("MenuID")]
    public uint MenuId { get; set; }

    [Key]
    [Column("OptionID")]
    public ushort OptionId { get; set; }

    public uint OptionIcon { get; set; }

    [Column(TypeName = "text")]
    public string OptionText { get; set; }

    [Column("OptionBroadcastTextID")]
    public int OptionBroadcastTextId { get; set; }

    public byte OptionType { get; set; }

    public uint OptionNpcFlag { get; set; }

    [Column("ActionMenuID")]
    public uint ActionMenuId { get; set; }

    [Column("ActionPoiID")]
    public uint ActionPoiId { get; set; }

    public byte BoxCoded { get; set; }

    public uint BoxMoney { get; set; }

    [Column(TypeName = "text")]
    public string BoxText { get; set; }

    [Column("BoxBroadcastTextID")]
    public int BoxBroadcastTextId { get; set; }

    public int? VerifiedBuild { get; set; }
}
