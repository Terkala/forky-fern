using System.Linq;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Restrictions;

public sealed partial class NeedPrerequisite : CESkillRestriction
{
    /// <summary>
    /// Single prerequisite (YAML: <c>prerequisite:</c>). Ignored when <see cref="Prerequisites"/> is non-empty.
    /// </summary>
    [DataField]
    public ProtoId<CESkillPrototype> Prerequisite;

    /// <summary>
    /// Multiple prerequisites (YAML: <c>prerequisites:</c>). If there is more than one, the player needs any one (OR).
    /// </summary>
    [DataField]
    public List<ProtoId<CESkillPrototype>> Prerequisites = new();

    public IReadOnlyList<ProtoId<CESkillPrototype>> EnumeratePrerequisiteIds()
    {
        if (Prerequisites.Count > 0)
            return Prerequisites;

        if (!string.IsNullOrEmpty(Prerequisite.Id))
            return new[] { Prerequisite };

        return Array.Empty<ProtoId<CESkillPrototype>>();
    }

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var ids = EnumeratePrerequisiteIds();
        if (ids.Count == 0)
            return false;

        var skillSystem = entManager.System<CESharedSkillSystem>();
        if (!skillSystem.TryGetSkillStorage(target, out var storage))
            return false;

        if (ids.Count == 1)
            return skillSystem.HaveSkill(storage.Owner, ids[0]);

        foreach (var id in ids)
        {
            if (skillSystem.HaveSkill(storage.Owner, id))
                return true;
        }

        return false;
    }

    public override bool CheckGivenLearnedSkills(
        IEntityManager entManager,
        EntityUid target,
        HashSet<ProtoId<CESkillPrototype>> learnedSkills)
    {
        var ids = EnumeratePrerequisiteIds();
        if (ids.Count == 0)
            return false;

        if (ids.Count == 1)
            return learnedSkills.Contains(ids[0]);

        foreach (var id in ids)
        {
            if (learnedSkills.Contains(id))
                return true;
        }

        return false;
    }

    public override string GetDescription(IEntityManager entManager, IPrototypeManager protoManager)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();
        var ids = EnumeratePrerequisiteIds();
        if (ids.Count == 0)
            return string.Empty;

        if (ids.Count == 1)
            return Loc.GetString("ce-skill-req-prerequisite", ("name", skillSystem.GetSkillName(ids[0])));

        var names = string.Join(", ", ids.Select(id => $"\"{skillSystem.GetSkillName(id)}\""));
        return Loc.GetString("ce-skill-req-prerequisite-any", ("names", names));
    }
}
