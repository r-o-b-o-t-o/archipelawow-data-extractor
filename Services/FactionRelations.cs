using ArchipelaWoW.DataExtractor.Dbc;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// How a creature's faction treats a player's.
///
/// Faction relations are directional and only the creature's view matters here: the core does not let a
/// player interact with a creature whose reaction to it is unfriendly or worse
/// (Player::GetNPCIfCanInteractWith). That reaction can follow the player's reputation, which these
/// checks leave out: they read the faction templates only.
/// See https://www.azerothcore.org/wiki/factiontemplate
/// `Enemies`/`Friend` hold Faction ids matched against the other template's `Faction`, while
/// `FactionGroup`/`FriendGroup`/`EnemyGroup` are bitmasks over the four faction groups
/// (1: players, 2: alliance, 4: horde, 8: monsters).
/// </summary>
public static class FactionRelations
{
    /// <summary>
    /// Whether a creature of faction <paramref name="npc"/> will talk to a player of faction
    /// <paramref name="player"/>.
    /// </summary>
    public static bool WillTalkTo(FactionTemplate npc, FactionTemplate player)
    {
        return IsFriendlyTo(npc, player) || !IsHostileTo(npc, player);
    }

    public static bool IsHostileTo(FactionTemplate self, FactionTemplate other)
    {
        if (self.ID == other.ID)
        {
            return false;
        }

        if (other.Faction != 0)
        {
            if (self.Enemies.Contains(other.Faction))
            {
                return true;
            }

            // An explicit friend entry outranks the hostile group mask.
            if (self.Friend.Contains(other.Faction))
            {
                return false;
            }
        }

        return (self.EnemyGroup & other.FactionGroup) != 0;
    }

    public static bool IsFriendlyTo(FactionTemplate self, FactionTemplate other)
    {
        if (self.ID == other.ID)
        {
            return true;
        }

        if (other.Faction != 0)
        {
            if (self.Enemies.Contains(other.Faction))
            {
                return false;
            }

            if (self.Friend.Contains(other.Faction))
            {
                return true;
            }
        }

        return (self.FriendGroup & other.FactionGroup) != 0 || (self.FactionGroup & other.FriendGroup) != 0;
    }
}
