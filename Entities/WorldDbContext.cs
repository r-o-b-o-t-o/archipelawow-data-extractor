using System;
using System.Collections.Generic;
using ArchipelaWoW.QuestExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities;

public partial class WorldDbContext : DbContext
{
    public WorldDbContext(DbContextOptions<WorldDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Creature> Creatures { get; set; }

    public virtual DbSet<CreatureQuestender> CreatureQuestenders { get; set; }

    public virtual DbSet<CreatureQueststarter> CreatureQueststarters { get; set; }

    public virtual DbSet<CreatureTemplate> CreatureTemplates { get; set; }

    public virtual DbSet<Disable> Disables { get; set; }

    public virtual DbSet<GameEventCreatureQuest> GameEventCreatureQuests { get; set; }

    public virtual DbSet<GameEventGameobjectQuest> GameEventGameobjectQuests { get; set; }

    public virtual DbSet<Gameobject> Gameobjects { get; set; }

    public virtual DbSet<GameobjectQuestender> GameobjectQuestenders { get; set; }

    public virtual DbSet<GameobjectQueststarter> GameobjectQueststarters { get; set; }

    public virtual DbSet<GameobjectTemplate> GameobjectTemplates { get; set; }

    public virtual DbSet<ItemTemplate> ItemTemplates { get; set; }

    public virtual DbSet<PoolQuest> PoolQuests { get; set; }

    public virtual DbSet<QuestPoi> QuestPois { get; set; }

    public virtual DbSet<QuestTemplate> QuestTemplates { get; set; }

    public virtual DbSet<QuestTemplateAddon> QuestTemplateAddons { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Creature>(entity =>
        {
            entity.HasKey(e => e.Guid).HasName("PRIMARY");

            entity.ToTable("creature", tb => tb.HasComment("Creature System"));

            entity.Property(e => e.Guid).HasComment("Global Unique Identifier");
            entity.Property(e => e.AreaId).HasComment("Area Identifier");
            entity.Property(e => e.Curhealth).HasDefaultValueSql("'1'");
            entity.Property(e => e.Id).HasComment("Creature Identifier");
            entity.Property(e => e.Map).HasComment("Map Identifier");
            entity.Property(e => e.PhaseMask).HasDefaultValueSql("'1'");
            entity.Property(e => e.ScriptName)
                .HasDefaultValueSql("''")
                .IsFixedLength();
            entity.Property(e => e.SpawnMask).HasDefaultValueSql("'1'");
            entity.Property(e => e.Spawntimesecs).HasDefaultValueSql("'120'");
            entity.Property(e => e.ZoneId).HasComment("Zone Identifier");
        });

        modelBuilder.Entity<CreatureQuestender>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Quest })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("creature_questender", tb => tb.HasComment("Creature System"));

            entity.Property(e => e.Id).HasComment("Identifier");
            entity.Property(e => e.Quest).HasComment("Quest Identifier");
        });

        modelBuilder.Entity<CreatureQueststarter>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Quest })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("creature_queststarter", tb => tb.HasComment("Creature System"));

            entity.Property(e => e.Id).HasComment("Identifier");
            entity.Property(e => e.Quest).HasComment("Quest Identifier");
        });

        modelBuilder.Entity<CreatureTemplate>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("PRIMARY");

            entity.ToTable("creature_template", tb => tb.HasComment("Creature System"));

            entity.Property(e => e.Entry).ValueGeneratedNever();
            entity.Property(e => e.Ainame)
                .HasDefaultValueSql("''")
                .IsFixedLength();
            entity.Property(e => e.ArmorModifier).HasDefaultValueSql("'1'");
            entity.Property(e => e.BaseVariance).HasDefaultValueSql("'1'");
            entity.Property(e => e.DamageModifier).HasDefaultValueSql("'1'");
            entity.Property(e => e.DetectionRange).HasDefaultValueSql("'20'");
            entity.Property(e => e.ExperienceModifier).HasDefaultValueSql("'1'");
            entity.Property(e => e.HealthModifier).HasDefaultValueSql("'1'");
            entity.Property(e => e.HoverHeight).HasDefaultValueSql("'1'");
            entity.Property(e => e.IconName).IsFixedLength();
            entity.Property(e => e.ManaModifier).HasDefaultValueSql("'1'");
            entity.Property(e => e.Maxlevel).HasDefaultValueSql("'1'");
            entity.Property(e => e.Minlevel).HasDefaultValueSql("'1'");
            entity.Property(e => e.Name)
                .HasDefaultValueSql("'0'")
                .IsFixedLength();
            entity.Property(e => e.RangeVariance).HasDefaultValueSql("'1'");
            entity.Property(e => e.RegenHealth).HasDefaultValueSql("'1'");
            entity.Property(e => e.ScriptName)
                .HasDefaultValueSql("''")
                .IsFixedLength();
            entity.Property(e => e.SpeedFlight).HasDefaultValueSql("'1'");
            entity.Property(e => e.SpeedRun)
                .HasDefaultValueSql("'1.14286'")
                .HasComment("Result of 8.0/7.0, most common value");
            entity.Property(e => e.SpeedSwim).HasDefaultValueSql("'1'");
            entity.Property(e => e.SpeedWalk)
                .HasDefaultValueSql("'1'")
                .HasComment("Result of 2.5/2.5, most common value");
            entity.Property(e => e.Subname).IsFixedLength();
        });

        modelBuilder.Entity<Disable>(entity =>
        {
            entity.HasKey(e => new { e.SourceType, e.Entry })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.Comment).HasDefaultValueSql("''");
            entity.Property(e => e.Params0).HasDefaultValueSql("''");
            entity.Property(e => e.Params1).HasDefaultValueSql("''");
        });

        modelBuilder.Entity<GameEventCreatureQuest>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Quest })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.EventEntry).HasComment("Entry of the game event.");
        });

        modelBuilder.Entity<GameEventGameobjectQuest>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Quest, e.EventEntry })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.Property(e => e.EventEntry).HasComment("Entry of the game event");
        });

        modelBuilder.Entity<Gameobject>(entity =>
        {
            entity.HasKey(e => e.Guid).HasName("PRIMARY");

            entity.ToTable("gameobject", tb => tb.HasComment("Gameobject System"));

            entity.Property(e => e.Guid).HasComment("Global Unique Identifier");
            entity.Property(e => e.AreaId).HasComment("Area Identifier");
            entity.Property(e => e.Id).HasComment("Gameobject Identifier");
            entity.Property(e => e.Map).HasComment("Map Identifier");
            entity.Property(e => e.PhaseMask).HasDefaultValueSql("'1'");
            entity.Property(e => e.ScriptName)
                .HasDefaultValueSql("''")
                .IsFixedLength();
            entity.Property(e => e.SpawnMask).HasDefaultValueSql("'1'");
            entity.Property(e => e.ZoneId).HasComment("Zone Identifier");
        });

        modelBuilder.Entity<GameobjectQuestender>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Quest })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.Quest).HasComment("Quest Identifier");
        });

        modelBuilder.Entity<GameobjectQueststarter>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.Quest })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.Quest).HasComment("Quest Identifier");
        });

        modelBuilder.Entity<GameobjectTemplate>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("PRIMARY");

            entity.ToTable("gameobject_template", tb => tb.HasComment("Gameobject System"));

            entity.Property(e => e.Entry).ValueGeneratedNever();
            entity.Property(e => e.Ainame)
                .HasDefaultValueSql("''")
                .IsFixedLength();
            entity.Property(e => e.CastBarCaption).HasDefaultValueSql("''");
            entity.Property(e => e.IconName).HasDefaultValueSql("''");
            entity.Property(e => e.Name).HasDefaultValueSql("''");
            entity.Property(e => e.ScriptName).HasDefaultValueSql("''");
            entity.Property(e => e.Size).HasDefaultValueSql("'1'");
            entity.Property(e => e.Unk1).HasDefaultValueSql("''");
        });

        modelBuilder.Entity<ItemTemplate>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("PRIMARY");

            entity.ToTable("item_template", tb => tb.HasComment("Item System"));

            entity.HasIndex(e => e.Name, "idx_name").HasAnnotation("MySql:IndexPrefixLength", new[] { 250 });

            entity.Property(e => e.Entry).ValueGeneratedNever();
            entity.Property(e => e.AllowableClass).HasDefaultValueSql("'-1'");
            entity.Property(e => e.AllowableRace).HasDefaultValueSql("'-1'");
            entity.Property(e => e.BuyCount).HasDefaultValueSql("'1'");
            entity.Property(e => e.Delay).HasDefaultValueSql("'1000'");
            entity.Property(e => e.Description).HasDefaultValueSql("''");
            entity.Property(e => e.Name).HasDefaultValueSql("''");
            entity.Property(e => e.RequiredDisenchantSkill).HasDefaultValueSql("'-1'");
            entity.Property(e => e.ScriptName).HasDefaultValueSql("''");
            entity.Property(e => e.SoundOverrideSubclass).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcategorycooldown1).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcategorycooldown2).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcategorycooldown3).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcategorycooldown4).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcategorycooldown5).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcooldown1).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcooldown2).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcooldown3).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcooldown4).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Spellcooldown5).HasDefaultValueSql("'-1'");
            entity.Property(e => e.Stackable).HasDefaultValueSql("'1'");
        });

        modelBuilder.Entity<PoolQuest>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("PRIMARY");

            entity.Property(e => e.Entry).ValueGeneratedNever();
        });

        modelBuilder.Entity<QuestPoi>(entity =>
        {
            entity.HasKey(e => new { e.QuestId, e.Id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
        });

        modelBuilder.Entity<QuestTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("quest_template", tb => tb.HasComment("Quest System"));

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.QuestLevel).HasDefaultValueSql("'1'");
            entity.Property(e => e.QuestType).HasDefaultValueSql("'2'");
            entity.Property(e => e.RewardFactionId1).HasComment("faction id from Faction.dbc in this case");
            entity.Property(e => e.RewardFactionId2).HasComment("faction id from Faction.dbc in this case");
            entity.Property(e => e.RewardFactionId3).HasComment("faction id from Faction.dbc in this case");
            entity.Property(e => e.RewardFactionId4).HasComment("faction id from Faction.dbc in this case");
            entity.Property(e => e.RewardFactionId5).HasComment("faction id from Faction.dbc in this case");
        });

        modelBuilder.Entity<QuestTemplateAddon>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.BreadcrumbForQuestId).HasDefaultValueSql("'0'");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
