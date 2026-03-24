using System.Numerics;
using Content.Shared._CE.MageAscension.Components;
using Content.Shared._CE.MagicEnergy.Systems;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Power.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Funkystation.MageAscension;

/// <summary>
/// +1/s at 0 tiles from a ley source, +0.1/s at 9 tiles, linear falloff; blocked by walls (Impassable|InteractImpassable ray).
/// Multiple sources sum their contributions.
/// Mages of Ascension use <see cref="Content.Server._CE.MageAscension.MagePassiveManaRegenSystem"/> instead of <see cref="LeyLineManaReceiverComponent"/> to match design doc regen.
/// </summary>
public sealed class LeyLineManaRegenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MagicBatterySystem _magicBattery = default!;

    private TimeSpan _nextTick;
    private const float TickSeconds = 1f;
    private const float MaxTileDistance = 9f;
    private const float RegenAtZero = 1f;
    private const float RegenAtMaxDist = 0.1f;
    private static readonly CollisionGroup LeyBlockMask = CollisionGroup.Impassable | CollisionGroup.InteractImpassable;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(TickSeconds);

        var sources = new List<(EntityUid Uid, MapCoordinates Pos)>();
        var sourceQuery = EntityQueryEnumerator<LeyLineSourceComponent, TransformComponent>();
        while (sourceQuery.MoveNext(out var uid, out var src, out var xform))
        {
            if (!src.Enabled)
                continue;
            sources.Add((uid, _transform.GetMapCoordinates(uid, xform)));
        }

        if (sources.Count == 0)
            return;

        var receiverQuery = EntityQueryEnumerator<LeyLineManaReceiverComponent, TransformComponent, BatteryComponent>();
        while (receiverQuery.MoveNext(out var recvUid, out _, out var recvXform, out var battery))
        {
            var recvMap = _transform.GetMapCoordinates(recvUid, recvXform);
            if (recvMap.MapId == MapId.Nullspace)
                continue;

            float total = 0f;
            foreach (var (srcUid, srcMap) in sources)
            {
                if (srcMap.MapId != recvMap.MapId)
                    continue;

                var distTiles = WorldDistanceTiles(srcMap.Position, recvMap.Position);
                if (distTiles > MaxTileDistance)
                    continue;

                if (!HasLeyLineOfSight(srcMap, recvMap, srcUid, recvUid))
                    continue;

                var t = distTiles / MaxTileDistance;
                total += float.Lerp(RegenAtZero, RegenAtMaxDist, t);
            }

            if (total > 0f)
                _magicBattery.ChangeMagicCharge((recvUid, battery), total * TickSeconds);
        }
    }

    private float WorldDistanceTiles(Vector2 a, Vector2 b)
    {
        var d = a - b;
        return d.Length() / TileSize;
    }

    private const float TileSize = 1f; // SS14 default world units per tile

    private bool HasLeyLineOfSight(MapCoordinates from, MapCoordinates to, EntityUid srcUid, EntityUid recvUid)
    {
        var dir = to.Position - from.Position;
        var len = dir.Length();
        if (len < 0.01f)
            return true;

        return _interaction.InRangeUnobstructed(from, to, len, LeyBlockMask,
            e => e == srcUid || e == recvUid, checkAccess: true);
    }
}
