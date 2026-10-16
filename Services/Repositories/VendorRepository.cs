using ArchipelaWoW.DataExtractor.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Services.Repositories;

/// <summary>
/// One weapon a vendor sells for money, in one zone its creature stands in.
/// </summary>
public class VendorWeapon
{
    public int ZoneId { get; set; }
    public ushort Faction { get; set; }
    public int Subclass { get; set; }
    public int RequiredLevel { get; set; }
    public int AllowableRace { get; set; }
}

public class VendorRepository(WorldDbContext db)
{
    // creature_template.npcflag, see UnitDefines.h
    private const uint UNIT_NPC_FLAG_VENDOR = 0x80;

    // ItemTemplate classes, see ItemTemplate.h
    private const int ITEM_CLASS_WEAPON = 2;

    // creature.phaseMask: the phase a character is in unless a quest or an aura moves it
    private const uint PHASEMASK_NORMAL = 1;

    /// <summary>
    /// Every weapon sold for money, with no reputation or profession asked of the buyer, by a creature spawned
    /// in the normal phase outside of game events. Reads the denormalised <c>zoneId</c> spawn column, which
    /// stays at 0 until worldserver has populated it (see the README). A spawn only removed during an event
    /// (a negative <c>eventEntry</c>) stands there the rest of the time, so it counts.
    /// </summary>
    public async Task<List<VendorWeapon>> GetVendorWeapons()
    {
        var eventSpawns = db.GameEventCreatures.Where(spawn => spawn.EventEntry > 0).Select(spawn => spawn.Guid);

        var weapons = await db.NpcVendors
            .AsNoTracking()
            .Where(vendor => vendor.ExtendedCost == 0)
            .Join(db.CreatureTemplates, vendor => vendor.Entry, template => template.Entry,
                  (vendor, template) => new { vendor.Item, template.Entry, template.Faction, template.Npcflag })
            .Where(vendor => (vendor.Npcflag & UNIT_NPC_FLAG_VENDOR) != 0)
            .Join(db.Creatures, vendor => vendor.Entry, creature => creature.Id,
                  (vendor, creature) => new { vendor.Item, vendor.Faction, creature.Guid, creature.ZoneId, creature.PhaseMask })
            .Where(vendor => vendor.ZoneId != 0 && (vendor.PhaseMask & PHASEMASK_NORMAL) != 0 && !eventSpawns.Contains(vendor.Guid))
            .Join(db.ItemTemplates, vendor => vendor.Item, item => (int)item.Entry,
                  (vendor, item) => new
                  {
                      vendor.ZoneId, vendor.Faction, item.Class, item.Subclass, item.RequiredLevel, item.AllowableRace,
                      item.RequiredReputationFaction, item.RequiredSkill,
                  })
            .Where(vendor => vendor.Class == ITEM_CLASS_WEAPON && vendor.RequiredReputationFaction == 0 && vendor.RequiredSkill == 0)
            .Select(vendor => new { vendor.ZoneId, vendor.Faction, vendor.Subclass, vendor.RequiredLevel, vendor.AllowableRace })
            .Distinct()
            .ToListAsync();

        return [.. weapons.Select(weapon => new VendorWeapon
        {
            ZoneId = weapon.ZoneId,
            Faction = weapon.Faction,
            Subclass = weapon.Subclass,
            RequiredLevel = weapon.RequiredLevel,
            AllowableRace = weapon.AllowableRace,
        })];
    }
}
