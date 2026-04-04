using Content.Shared.Blob;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Blob;

/// <summary>
/// Periodically heals damaged blob tiles near mitochondria (Chebyshev radius 3).
/// </summary>
public sealed class BlobMitochondriaSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextPulse = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobTileComponent, ComponentShutdown>(OnTileShutdown);
    }

    private void OnTileShutdown(EntityUid uid, BlobTileComponent tile, ComponentShutdown args)
    {
        _nextPulse.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var cur = _timing.CurTime;
        var query = EntityQueryEnumerator<BlobTileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tile, out var xform))
        {
            if (tile.Kind != BlobTileKind.Mitochondria)
                continue;

            if (!_nextPulse.TryGetValue(uid, out var next))
            {
                _nextPulse[uid] = cur + TimeSpan.FromSeconds(3);
                continue;
            }

            if (cur < next)
                continue;

            _nextPulse[uid] = cur + TimeSpan.FromSeconds(3);

            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var hive = tile.Hive;

            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"] = -8;

            var q2 = EntityQueryEnumerator<BlobTileComponent, TransformComponent>();
            while (q2.MoveNext(out var other, out var otile, out var ox))
            {
                if (otile.Hive != hive)
                    continue;
                if (ox.GridUid != gridUid)
                    continue;
                var idx = _map.TileIndicesFor(gridUid, grid, ox.Coordinates);
                var dx = Math.Abs(idx.X - origin.X);
                var dy = Math.Abs(idx.Y - origin.Y);
                if (Math.Max(dx, dy) > 3)
                    continue;
                if (!TryComp<DamageableComponent>(other, out var dmg) || dmg.TotalDamage <= FixedPoint2.Zero)
                    continue;
                _damageable.TryChangeDamage(other, heal);
            }
        }
    }
}
