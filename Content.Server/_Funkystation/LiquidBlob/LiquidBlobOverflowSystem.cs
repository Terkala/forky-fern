using System.Collections.Generic;
using Content.Server.Atmos.Components;
using Content.Shared._Funkystation.LiquidBlob.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.LiquidBlob;

public sealed class LiquidBlobOverflowSystem : EntitySystem
{
    private const float OverflowAmount = 5f;
    private const int MaxOverflowIterations = 10;

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly ProtoId<TagPrototype> IgnoredTag = "SpreaderIgnore";

    private readonly Queue<(EntityUid Tile, float Amount)> _overflowQueue = new();

    public override void Update(float frameTime)
    {
        var blobQuery = GetEntityQuery<LiquidBlobTileComponent>();
        var airtightQuery = GetEntityQuery<AirtightComponent>();
        var transformQuery = GetEntityQuery<TransformComponent>();

        var toProcess = new List<EntityUid>();
        var blobEnum = EntityQueryEnumerator<LiquidBlobTileComponent>();
        while (blobEnum.MoveNext(out var uid, out var comp))
        {
            if (comp.LiquidLevel >= 20f)
                toProcess.Add(uid);
        }

        foreach (var uid in toProcess)
        {
            if (!blobQuery.TryGetComponent(uid, out var comp) || comp.LiquidLevel < 20f)
                continue;

            if (!transformQuery.TryGetComponent(uid, out var xform) || xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
                continue;

            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var rootTile = comp.RootTile ?? uid;

            for (var i = 0; i < 4; i++)
            {
                var atmosDir = (AtmosDirection)(1 << i);
                var neighborPos = tile.Offset(atmosDir);
                var otherAtmosDir = i.ToOppositeDir();

                if (!_map.TryGetTileRef(gridUid, grid, neighborPos, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                if (_turf.IsSpace(tileRef))
                    continue;

                var blocked = false;
                var neighborEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, neighborPos);
                while (neighborEnumerator.MoveNext(out var ent))
                {
                    if (!airtightQuery.TryGetComponent(ent, out var airtight) || !airtight.AirBlocked || _tag.HasTag(ent.Value, IgnoredTag))
                        continue;
                    if ((airtight.AirBlockedDirection & otherAtmosDir) == 0x0)
                        continue;
                    blocked = true;
                    break;
                }
                if (blocked)
                    continue;

                var neighborCoords = _map.GridTileToLocal(gridUid, grid, neighborPos);
                LiquidBlobTileComponent? existingBlob = null;
                EntityUid? existingBlobUid = null;
                neighborEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, neighborPos);
                while (neighborEnumerator.MoveNext(out var ent))
                {
                    if (blobQuery.TryGetComponent(ent, out existingBlob))
                    {
                        existingBlobUid = ent;
                        break;
                    }
                }

                if (existingBlobUid.HasValue && existingBlob != null)
                {
                    var space = existingBlob.MaxCapacity - existingBlob.LiquidLevel;
                    if (space >= OverflowAmount)
                    {
                        existingBlob.LiquidLevel += OverflowAmount;
                        Dirty(existingBlobUid.Value, existingBlob);
                    }
                    else if (space > 0)
                    {
                        existingBlob.LiquidLevel = existingBlob.MaxCapacity;
                        Dirty(existingBlobUid.Value, existingBlob);
                        _overflowQueue.Enqueue((existingBlobUid.Value, OverflowAmount - space));
                    }
                    else
                    {
                        _overflowQueue.Enqueue((existingBlobUid.Value, OverflowAmount));
                    }
                }
                else
                {
                    var newTile = Spawn("LiquidBlobTile", neighborCoords);
                    var newComp = Comp<LiquidBlobTileComponent>(newTile);
                    newComp.RootTile = rootTile;
                    newComp.LiquidLevel = OverflowAmount;
                    Dirty(newTile, newComp);
                }

                comp.LiquidLevel -= OverflowAmount;
                Dirty(uid, comp);
                break;
            }
        }

        var iterations = 0;
        while (_overflowQueue.Count > 0 && iterations < MaxOverflowIterations)
        {
            iterations++;
            var (tileUid, amount) = _overflowQueue.Dequeue();
            if (!Exists(tileUid) || !blobQuery.TryGetComponent(tileUid, out var tileComp))
                continue;

            if (amount <= 0)
                continue;

            if (!transformQuery.TryGetComponent(tileUid, out var xform) || xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
                continue;

            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var rootTile = tileComp.RootTile ?? tileUid;

            for (var i = 0; i < 4; i++)
            {
                var atmosDir = (AtmosDirection)(1 << i);
                var neighborPos = tile.Offset(atmosDir);
                var otherAtmosDir = i.ToOppositeDir();

                if (!_map.TryGetTileRef(gridUid, grid, neighborPos, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                if (_turf.IsSpace(tileRef))
                    continue;

                var blocked = false;
                var neighborEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, neighborPos);
                while (neighborEnumerator.MoveNext(out var ent))
                {
                    if (!airtightQuery.TryGetComponent(ent, out var airtight) || !airtight.AirBlocked || _tag.HasTag(ent.Value, IgnoredTag))
                        continue;
                    if ((airtight.AirBlockedDirection & otherAtmosDir) == 0x0)
                        continue;
                    blocked = true;
                    break;
                }
                if (blocked)
                    continue;

                var neighborCoords = _map.GridTileToLocal(gridUid, grid, neighborPos);
                LiquidBlobTileComponent? existingBlob = null;
                EntityUid? existingBlobUid = null;
                neighborEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, neighborPos);
                while (neighborEnumerator.MoveNext(out var ent))
                {
                    if (blobQuery.TryGetComponent(ent, out existingBlob))
                    {
                        existingBlobUid = ent;
                        break;
                    }
                }

                var toAdd = Math.Min(amount, OverflowAmount);
                if (existingBlobUid.HasValue && existingBlob != null)
                {
                    var space = existingBlob.MaxCapacity - existingBlob.LiquidLevel;
                    if (space >= toAdd)
                    {
                        existingBlob.LiquidLevel += toAdd;
                        Dirty(existingBlobUid.Value, existingBlob);
                        amount -= toAdd;
                    }
                    else if (space > 0)
                    {
                        existingBlob.LiquidLevel = existingBlob.MaxCapacity;
                        Dirty(existingBlobUid.Value, existingBlob);
                        _overflowQueue.Enqueue((existingBlobUid.Value, toAdd - space));
                        amount -= toAdd;
                    }
                    else
                    {
                        _overflowQueue.Enqueue((existingBlobUid.Value, toAdd));
                        amount -= toAdd;
                    }
                }
                else
                {
                    var newTile = Spawn("LiquidBlobTile", neighborCoords);
                    var newComp = Comp<LiquidBlobTileComponent>(newTile);
                    newComp.RootTile = rootTile;
                    newComp.LiquidLevel = toAdd;
                    Dirty(newTile, newComp);
                    amount -= toAdd;
                }

                if (amount <= 0)
                    break;
            }
        }

        _overflowQueue.Clear();
    }
}
