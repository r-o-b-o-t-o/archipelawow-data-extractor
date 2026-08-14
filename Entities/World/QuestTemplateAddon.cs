using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities.World;

[Table("quest_template_addon")]
[MySqlCollation("utf8mb4_unicode_ci")]
public partial class QuestTemplateAddon
{
    [Key]
    [Column("ID")]
    public uint Id { get; set; }

    public byte MaxLevel { get; set; }

    public uint AllowableClasses { get; set; }

    [Column("SourceSpellID")]
    public uint SourceSpellId { get; set; }

    [Column("PrevQuestID")]
    public int PrevQuestId { get; set; }

    [Column("NextQuestID")]
    public uint NextQuestId { get; set; }

    public int ExclusiveGroup { get; set; }

    [Column(TypeName = "mediumint unsigned")]
    public uint BreadcrumbForQuestId { get; set; }

    [Column("RewardMailTemplateID")]
    public uint RewardMailTemplateId { get; set; }

    public uint RewardMailDelay { get; set; }

    [Column("RequiredSkillID")]
    public ushort RequiredSkillId { get; set; }

    public ushort RequiredSkillPoints { get; set; }

    public ushort RequiredMinRepFaction { get; set; }

    public ushort RequiredMaxRepFaction { get; set; }

    public int RequiredMinRepValue { get; set; }

    public int RequiredMaxRepValue { get; set; }

    public byte ProvidedItemCount { get; set; }

    public uint SpecialFlags { get; set; }
}
