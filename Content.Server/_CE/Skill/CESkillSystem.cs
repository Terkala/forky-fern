using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;

namespace Content.Server._CE.Skill;

public sealed partial class CESkillSystem : CESharedSkillSystem
{
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

        TryLearnSkill(storageUid, ev.Skill);
    }
}
