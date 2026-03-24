using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Restrictions;

public sealed partial class NeedPrerequisite : CESkillRestriction
{
    [DataField(required: true)]
    public ProtoId<CESkillPrototype> Prerequisite = new();

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();
        if (!skillSystem.TryGetSkillStorage(target, out var storage))
            return false;

        return skillSystem.HaveSkill(storage.Owner, Prerequisite);
    }

    public override string GetDescription(IEntityManager entManager, IPrototypeManager protoManager)
    {
        var skillSystem = entManager.System<CESharedSkillSystem>();

        return Loc.GetString("ce-skill-req-prerequisite", ("name", skillSystem.GetSkillName(Prerequisite)));
    }
}
