using System.Linq;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Tier 2–5 progression spell IDs per <see cref="MageSchoolPrototype"/> (see mage_schools.yml).
/// </summary>
public static class MageSchoolProgressStatics
{
    public static HashSet<ProtoId<CESkillPrototype>> GetProgressionSkillIds(
        string schoolProtoId,
        IPrototypeManager proto)
    {
        var set = new HashSet<ProtoId<CESkillPrototype>>();
        if (!proto.TryIndex(schoolProtoId, out MageSchoolPrototype? school))
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
        string schoolProtoId,
        IPrototypeManager proto)
    {
        var ids = GetProgressionSkillIds(schoolProtoId, proto);
        return learned.Count(ids.Contains);
    }
}
