using Content.Shared._CE.Skill;
using Content.Shared._CE.Skill.Components;
using Content.Shared._CE.Skill.Prototypes;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Skill;

public sealed partial class CEClientSkillSystem : CESharedSkillSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<EntityUid>? OnSkillUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESkillStorageComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<CESkillStorageComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_playerManager.LocalEntity is not { } local)
            return;

        if (!TryGetSkillStorage(local, out var storage))
            return;

        if (storage.Owner != ent.Owner)
            return;

        OnSkillUpdate?.Invoke(ent.Owner);
    }

    public void RequestSkillData()
    {
        if (_playerManager.LocalEntity is not { } local)
            return;

        if (!TryGetSkillStorage(local, out var storage))
            return;

        OnSkillUpdate?.Invoke(storage.Owner);
    }

    public void RequestLearnSkill(EntityUid? targetStorage, CESkillPrototype? skill)
    {
        if (skill == null || targetStorage == null)
            return;

        var netEv = new CETryLearnSkillMessage(GetNetEntity(targetStorage.Value), skill.ID);
        RaiseNetworkEvent(netEv);

        if (_proto.Resolve(skill.Tree, out var indexedTree) && _playerManager.LocalEntity is { } listener)
        {
            _audio.PlayGlobal(indexedTree.LearnSound, listener, AudioParams.Default.WithVolume(6f));
        }
    }
}
