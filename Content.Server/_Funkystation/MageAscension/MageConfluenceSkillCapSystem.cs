using Content.Server._CE.Skill;
using Content.Shared._CE.MageAscension;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.MageAscension;

/// <summary>
/// Opening ley confluences widens the mage arcane point cap (does not grant unspent points).
/// </summary>
public sealed class MageConfluenceSkillCapSystem : EntitySystem
{
    [Dependency] private readonly CESkillSystem _skill = default!;

    private static readonly ProtoId<CESkillPointPrototype> MagePointType = new("MageGrimoire");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MageOfAscensionComponent, MageConfluenceOpenedEvent>(OnConfluenceOpened);
    }

    private void OnConfluenceOpened(Entity<MageOfAscensionComponent> ent, ref MageConfluenceOpenedEvent args)
    {
        if (!_skill.TryGetSkillStorage(ent.Owner, out var storage))
            return;

        _skill.TryIncreaseSkillPointCapOnly(storage.AsNullable(), MagePointType, 1, silent: false);
    }
}
