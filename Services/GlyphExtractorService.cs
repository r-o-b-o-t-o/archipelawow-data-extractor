using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities;
using ArchipelaWoW.DataExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static ArchipelaWoW.DataExtractor.Services.CharacterSkills;

namespace ArchipelaWoW.DataExtractor.Services;

public enum GlyphType
{
    Major,
    Minor,
}

public class ExtractedGlyphData
{
    /// <summary>The item_template entry, which the server module mails.</summary>
    public int Id { get; set; }
    public string Name { get; set; }

    /// <summary>The ChrClasses.dbc id of the one class that can use it.</summary>
    public int Class { get; set; }
    public GlyphType Type { get; set; }
    public int RequiredLevel { get; set; }
}

/// <summary>
/// Builds the glyph table archipelawow ships: the glyph items Inscription makes for the randomized classes.
/// </summary>
public class GlyphExtractorService(
        ILogger<GlyphExtractorService> logger,
        WorldDbContext db,
        SpellContainer spells,
        SkillLineAbilityContainer skillLineAbilities,
        GlyphPropertiesContainer glyphProperties
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // ItemTemplate.Class, see ItemTemplate.h
    private const int ITEM_CLASS_GLYPH = 16;

    // SkillLine.dbc id
    private const int SKILL_INSCRIPTION = 773;

    // Spell effects, see SharedDefines.h
    private const int SPELL_EFFECT_CREATE_ITEM = 24;
    private const int SPELL_EFFECT_APPLY_GLYPH = 74;

    // GlyphProperties.GlyphSlotFlags, which the core matches against GlyphSlot.dbc's to tell major from minor
    private const int GLYPH_SLOT_FLAGS_MINOR = 1;

    public async Task ExtractGlyphs()
    {
        string outDir = OutputDirectory.Prepare();

        // Only what a scribe can make: the other glyph items are NPC equipment, test and deprecated glyphs,
        // and a few no recipe makes, such as the Glyph of Envenom.
        var crafted = skillLineAbilities
            .Where(ability => ability.SkillLine == SKILL_INSCRIPTION)
            .Select(ability => spells.Get(ability.Spell))
            .Where(spell => spell != null)
            .SelectMany(spell => spell.Effect
                .Select((effect, i) => (effect, item: spell.EffectItemType[i]))
                .Where(e => e.effect == SPELL_EFFECT_CREATE_ITEM)
                .Select(e => e.item))
            .ToHashSet();

        var items = await db.ItemTemplates.Where(item => item.Class == ITEM_CLASS_GLYPH).ToListAsync();

        List<ExtractedGlyphData> glyphs = [.. items
            .Where(item => FilterItem(item, "not made by Inscription", i => crafted.Contains((int)i.Entry)))
            .Select(item => (item, classId: ClassOf(item), glyph: GlyphOf(item)))
            .Where(g => FilterItem(g.item, "not for a randomized class", _ => g.classId != 0))
            .Where(g => FilterItem(g.item, "applies no glyph", _ => g.glyph != null))
            .Select(g => new ExtractedGlyphData()
            {
                Id = (int)g.item.Entry,
                Name = g.item.Name,
                Class = g.classId,
                Type = g.glyph.GlyphSlotFlags == GLYPH_SLOT_FLAGS_MINOR ? GlyphType.Minor : GlyphType.Major,
                RequiredLevel = g.item.RequiredLevel,
            })
            .OrderBy(glyph => glyph.Class)
            .ThenBy(glyph => glyph.Type)
            .ThenBy(glyph => glyph.Name, StringComparer.Ordinal)];

        string outFile = Path.Combine(outDir, "glyphs.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(glyphs, jsonOptions));

        logger.LogInformation("Wrote {count} glyphs to {file}.", glyphs.Count, outFile);
    }

    private static int ClassOf(ItemTemplate item)
    {
        return RANDOMIZED_CLASS_IDS.FirstOrDefault(classId => item.AllowableClass == ClassBit(classId));
    }

    private GlyphProperties GlyphOf(ItemTemplate item)
    {
        var spell = spells.Get(item.Spellid1);
        if (spell == null)
        {
            return null;
        }

        int index = Array.IndexOf(spell.Effect, SPELL_EFFECT_APPLY_GLYPH);
        return index < 0 ? null : glyphProperties.Get(spell.EffectMiscValue[index]);
    }

    private bool FilterItem(ItemTemplate item, string reason, Func<ItemTemplate, bool> fn)
    {
        bool result = fn(item);
        if (!result && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Filtered out \"{name}\" ({id}): {reason}", item.Name, item.Entry, reason);
        }
        return result;
    }
}
