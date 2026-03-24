using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Restrictions;

public sealed partial class SpeciesBlacklist : CESkillRestriction
{
    public override bool HideFromUI => true;

    [DataField(required: true)]
    public ProtoId<SpeciesPrototype> Species = new();

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var body = ResolveBody(entManager, target);
        if (!entManager.TryGetComponent<HumanoidProfileComponent>(body, out var profile))
            return false;

        return profile.Species != Species;
    }

    private static EntityUid ResolveBody(IEntityManager entManager, EntityUid target)
    {
        if (entManager.TryGetComponent<HumanoidProfileComponent>(target, out _))
            return target;

        if (entManager.TryGetComponent<MindComponent>(target, out var mind) && mind.OwnedEntity != null)
            return mind.OwnedEntity.Value;

        return target;
    }

    public override string GetDescription(IEntityManager entManager, IPrototypeManager protoManager)
    {
        var species = protoManager.Index(Species);

        return Loc.GetString("ce-skill-req-notspecies", ("name", Loc.GetString(species.Name)));
    }
}
