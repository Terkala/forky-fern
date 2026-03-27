using System.Linq;
using Content.Shared._CE.Skill.Prototypes;
using Content.Shared._Funkystation.MageAscension;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Elementalism progression spells: tier pick options from school tiers 2–5 (see mage_schools.yml).
/// </summary>
public static class MageElementalismProgressStatics
{
    public const string ElementalismSchoolId = "Elementalism";

    /// <summary>
    /// Skills that increment Elementalism depth when learned (tiers 2–5 options).
    /// </summary>
    public static HashSet<ProtoId<CESkillPrototype>> GetProgressionSkillIds(IPrototypeManager proto)
    {
        var set = new HashSet<ProtoId<CESkillPrototype>>();
        if (!proto.TryIndex(ElementalismSchoolId, out MageSchoolPrototype? school))
            return set;

        foreach (var entry in school.TierSpellPicks)
        {
            if (entry.Tier is < 2 or > 5)
                continue;

            foreach (var id in entry.Options)
                set.Add(id);
        }

        return set;
    }

    public static int CountProgressionLearned(
        IEnumerable<ProtoId<CESkillPrototype>> learned,
        IPrototypeManager proto)
    {
        var ids = GetProgressionSkillIds(proto);
        return learned.Count(ids.Contains);
    }
}
