using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skill;

public sealed partial class CESkillSystem : CESharedSkillSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<CESkillPointPrototype> WizardArcanePoint = new("WizardArcane");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CETryLearnSkillMessage>(OnClientRequestLearnSkill);
    }

    private void OnClientRequestLearnSkill(CETryLearnSkillMessage ev, EntitySessionEventArgs args)
    {
        var storageUid = GetEntity(ev.Entity);

        if (args.SenderSession.AttachedEntity is not { } mob)
            return;

        if (!TryGetSkillStorage(mob, out var storage) || storage.Owner != storageUid)
            return;

        if (!_prototypeManager.Resolve(ev.Skill, out var skillProto))
            return;

        if (!_prototypeManager.Resolve(skillProto.Tree, out var treeProto))
            return;

        if (treeProto.SkillType == WizardArcanePoint && !IsHoldingWizardSkillGrimoire(mob))
            return;

        TryLearnSkill(storageUid, ev.Skill);
    }

    private bool IsHoldingWizardSkillGrimoire(EntityUid mob)
    {
        if (!TryComp<HandsComponent>(mob, out var hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((mob, hands)))
        {
            if (HasComp<WizardSkillGrimoireComponent>(held))
                return true;
        }

        return false;
    }
}
