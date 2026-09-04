namespace ArchipelaWoW.DataExtractor.Entities.World;

public partial class Trainer
{
    public ICollection<TrainerSpell> Spells { get; set; } = [];
}
