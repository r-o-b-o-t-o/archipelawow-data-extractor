namespace ArchipelaWoW.DataExtractor.Entities.World;

public partial class QuestTemplate
{
    public List<QuestTemplateAddon> QuestTemplateAddons { get; set; } = [];
    public List<CreatureQueststarter> CreatureStarters { get; set; } = [];
    public List<CreatureQuestender> CreatureEnders { get; set; } = [];
    public List<GameobjectQueststarter> GameObjectStarters { get; set; } = [];
    public List<GameobjectQuestender> GameObjectEnders { get; set; } = [];
    public List<ItemTemplate> ItemTemplateStarters { get; set; } = [];
    public List<GameEventCreatureQuest> GameEventCreatureQuests { get; set; } = [];
    public List<GameEventGameobjectQuest> GameEventGameObjectQuests { get; set; } = [];
    public PoolQuest Pool { get; set; }

    public QuestTemplateAddon QuestTemplateAddon => QuestTemplateAddons.FirstOrDefault();
}
