using System.Collections.Generic;
using Content.Shared._CE.MageAscension.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.MageAscension;

public sealed class MageRiftElementalRotationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextRotation = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MageRiftElementalVariantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MageRiftElementalVariantComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMapInit(Entity<MageRiftElementalVariantComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;

        ent.Comp.Current = (MageRiftElementKind)_random.Next(0, 4);
        Dirty(ent);
        _nextRotation[ent.Owner] = _timing.CurTime + ent.Comp.RotationInterval;
    }

    private void OnTerminating(Entity<MageRiftElementalVariantComponent> ent, ref EntityTerminatingEvent args)
    {
        _nextRotation.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<MageRiftElementalVariantComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_nextRotation.TryGetValue(uid, out var when))
                continue;

            if (cur < when)
                continue;

            comp.Current = Next(comp.Current);
            Dirty(uid, comp);
            _nextRotation[uid] = cur + comp.RotationInterval;
        }
    }

    private static MageRiftElementKind Next(MageRiftElementKind k) => k switch
    {
        MageRiftElementKind.Fire => MageRiftElementKind.Air,
        MageRiftElementKind.Air => MageRiftElementKind.Earth,
        MageRiftElementKind.Earth => MageRiftElementKind.Water,
        _ => MageRiftElementKind.Fire,
    };
}
