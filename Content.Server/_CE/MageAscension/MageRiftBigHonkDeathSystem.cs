using Content.Server.Chat.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._CE.MageAscension;

public sealed class MageRiftBigHonkDeathSystem : EntitySystem
{
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private static readonly EntProtoId PeelProto = "TrashBananaPeel";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MageRiftBigHonkComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<MageRiftBigHonkComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        RemComp<MageRiftBigHonkComponent>(ent.Owner);

        var coords = Transform(ent).Coordinates;
        var sol = new Solution();
        sol.AddReagent("Laughter", FixedPoint2.New(100));
        _puddle.TrySpillAt(coords, sol, out _, sound: true);

        for (var i = 0; i < 12; i++)
        {
            var off = new Vector2(_random.NextFloat(-0.9f, 0.9f), _random.NextFloat(-0.9f, 0.9f));
            Spawn(PeelProto, coords.Offset(off));
        }

        _audio.PlayPvs(new SoundCollectionSpecifier("CluwneScreams"), ent.Owner);

        _chat.TrySendInGameICMessage(ent.Owner, Loc.GetString("mage-big-honk-death-laugh"), InGameICChatType.Emote,
            ChatTransmitRange.Normal, hideLog: false, ignoreActionBlocker: true);

        QueueDel(ent.Owner);
    }
}
