using System.Linq;
using System.Text;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Restrictions;

/// <summary>
/// Fails if the learner has any of the listed skills (e.g. other magic paths already chosen).
/// </summary>
public sealed partial class BlockIfAnySkillLearned : CESkillRestriction
{
    [DataField(required: true)]
    public List<ProtoId<CESkillPrototype>> BlockedIfAnyLearned = new();

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();
        if (!skillSystem.TryGetSkillStorage(target, out var storage))
            return false;

        foreach (var id in BlockedIfAnyLearned)
        {
            if (skillSystem.HaveSkill(storage.Owner, id, storage.Comp))
                return false;
        }

        return true;
    }

    public override bool CheckGivenLearnedSkills(
        IEntityManager entManager,
        EntityUid target,
        HashSet<ProtoId<CESkillPrototype>> learnedSkills)
    {
        foreach (var id in BlockedIfAnyLearned)
        {
            if (learnedSkills.Contains(id))
                return false;
        }

        return true;
    }

    public override string GetDescription(IEntityManager entManager, IPrototypeManager protoManager)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();
        var sb = new StringBuilder();
        sb.Append(Loc.GetString("ce-skill-req-block-if-any-learned-prefix"));

        var names = BlockedIfAnyLearned
            .Select(id => skillSystem.GetSkillName(id))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        if (names.Count > 0)
            sb.Append(' ').Append(string.Join(", ", names));

        return sb.ToString();
    }
}
