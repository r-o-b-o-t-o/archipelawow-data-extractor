using ArchipelaWoW.QuestExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Entities;

public partial class WorldDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestTemplate>(entity =>
        {
            entity
                .HasMany(quest => quest.QuestTemplateAddons)
                .WithOne(addon => addon.QuestTemplate)
                .HasForeignKey(addon => addon.Id);

            entity
                .HasMany(quest => quest.CreatureStarters)
                .WithOne(starter => starter.StartedQuest)
                .HasForeignKey(starter => starter.Quest)
                .IsRequired(false);

            entity
                .HasMany(quest => quest.CreatureEnders)
                .WithOne(ender => ender.EndedQuest)
                .HasForeignKey(ender => ender.Quest)
                .IsRequired(false);

            entity
                .HasMany(quest => quest.GameObjectStarters)
                .WithOne(starter => starter.StartedQuest)
                .HasForeignKey(starter => starter.Quest)
                .IsRequired(false);

            entity
                .HasMany(quest => quest.GameObjectEnders)
                .WithOne(ender => ender.EndedQuest)
                .HasForeignKey(ender => ender.Quest)
                .IsRequired(false);

            entity
                .HasMany(quest => quest.ItemTemplateStarters)
                .WithOne(itemTemplate => itemTemplate.StartedQuest)
                .HasForeignKey(itemTemplate => itemTemplate.Startquest)
                .IsRequired(false);

            entity
                .HasMany(quest => quest.GameEventCreatureQuests)
                .WithOne(e => e.EventQuest)
                .HasForeignKey(e => e.Quest)
                .IsRequired(false);

            entity
                .HasMany(quest => quest.GameEventGameObjectQuests)
                .WithOne(e => e.EventQuest)
                .HasForeignKey(e => e.Quest)
                .IsRequired(false);
        });

        modelBuilder.Entity<PoolQuest>(entity =>
        {
            entity
                .HasMany(pool => pool.Entries)
                .WithOne(quest => quest.Pool)
                .HasForeignKey(quest => quest.Id)
                .IsRequired(false);
        });

        modelBuilder.Entity<CreatureQueststarter>(entity =>
        {
            entity
                .HasOne(starter => starter.CreatureTemplate)
                .WithMany()
                .HasForeignKey(starter => starter.Id)
                .HasPrincipalKey(creature => creature.Entry);
        });

        modelBuilder.Entity<CreatureQuestender>(entity =>
        {
            entity
                .HasOne(ender => ender.CreatureTemplate)
                .WithMany()
                .HasForeignKey(ender => ender.Id)
                .HasPrincipalKey(creature => creature.Entry);
        });

        modelBuilder.Entity<GameobjectQueststarter>(entity =>
        {
            entity
                .HasOne(starter => starter.GameObjectTemplate)
                .WithMany()
                .HasForeignKey(starter => starter.Id)
                .HasPrincipalKey(gobj => gobj.Entry);
        });

        modelBuilder.Entity<GameobjectQuestender>(entity =>
        {
            entity
                .HasOne(ender => ender.GameObjectTemplate)
                .WithMany()
                .HasForeignKey(ender => ender.Id)
                .HasPrincipalKey(gobj => gobj.Entry);
        });
    }
}
