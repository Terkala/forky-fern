using Content.Shared._CE.Skill.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Effects;

/// <summary>
/// When the skill is learned, only the point cap increases (Sum unchanged).
/// </summary>
public sealed partial class IncreaseSkillPointCap : CESkillEffect
{
    [DataField(required: true)]
    public ProtoId<CESkillPointPrototype> PointType = default!;

    [DataField]
    public FixedPoint2 Amount = 1;

    public override void AddSkill(IEntityManager entManager, EntityUid target)
    {
        var sys = entManager.System<CESharedSkillSystem>();
        if (!sys.TryGetSkillStorage(target, out var storage))
            return;

        sys.TryIncreaseSkillPointCapOnly(storage.AsNullable(), PointType, Amount);
    }

    public override void RemoveSkill(IEntityManager entManager, EntityUid target)
    {
        // Cap-only bumps are not rolled back on skill strip; admins use skill reset tools if needed.
    }

    public override string? GetName(IEntityManager entManager, IPrototypeManager protoManager)
    {
        return null;
    }

    public override string? GetDescription(IEntityManager entManager, IPrototypeManager protoManager, ProtoId<CESkillPrototype> skill)
    {
        return Loc.GetString("ce-skill-effect-increase-cap-desc", ("amount", Amount));
    }
}
