using System.Linq;
using Content.Shared._Funkystation.Behonker;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Funkystation.Behonker;

public sealed class BehonkerRaySystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BehonkerRayActionEvent>(OnRay);
    }

    private void OnRay(BehonkerRayActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (!TryComp<TransformComponent>(performer, out var perfXform))
            return;

        var origin = _transform.GetWorldPosition(perfXform);
        var clickMap = _transform.ToMapCoordinates(args.Target);
        var destWorld = clickMap.Position;
        var delta = destWorld - origin;
        var len = delta.Length();

        if (len < 0.01f)
        {
            args.Handled = true;
            return;
        }

        var dir = delta / len;
        var maxDist = Math.Min(args.Range, len);

        var ray = new CollisionRay(origin, dir,
            (int) (CollisionGroup.MobMask | CollisionGroup.Opaque));

        var hits = _physics.IntersectRay(clickMap.MapId, ray, maxDist, performer, returnOnFirstHit: false)
            .OrderBy(h => h.Distance)
            .ToList();

        var seen = new HashSet<EntityUid>();

        foreach (var hit in hits)
        {
            if (!seen.Add(hit.HitEntity))
                continue;

            if (hit.HitEntity == performer)
                continue;

            if (TryComp<DamageableComponent>(hit.HitEntity, out _))
            {
                _damageable.TryChangeDamage(hit.HitEntity, args.Damage, origin: performer);
                continue;
            }

            break;
        }

        args.Handled = true;
    }
}
