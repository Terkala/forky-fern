using Content.Shared._Funkystation.LiquidBlob;
using Content.Shared._Funkystation.LiquidBlob.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Funkystation.LiquidBlob;

public sealed class LiquidBlobSpreadSystem : EntitySystem
{
    private const float SpreadCost = 5f;

    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LiquidBlobSpreadActionEvent>(OnSpreadAction);
    }

    private void OnSpreadAction(LiquidBlobSpreadActionEvent args)
    {
        if (args.Handled)
            return;
        if (TrySpreadFromObserver(args.Performer, args.Target))
            args.Handled = true;
    }

    public bool TrySpreadFromObserver(EntityUid observerUid, EntityCoordinates targetCoords)
    {
        if (!TryComp(observerUid, out LiquidBlobObserverComponent? observerComp))
            return false;

        var rootTile = observerComp.RootTile;
        if (!TryComp(rootTile, out TransformComponent? rootXform) || rootXform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        if (targetCoords.EntityId != gridUid)
            return false;

        var targetTile = _map.TileIndicesFor(gridUid, grid, targetCoords);
        var blobQuery = GetEntityQuery<LiquidBlobTileComponent>();

        var hasAdjacentBlob = false;
        for (var i = 0; i < 4; i++)
        {
            var atmosDir = (AtmosDirection)(1 << i);
            var neighborPos = targetTile.Offset(atmosDir);
            var neighborEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, neighborPos);
            while (neighborEnumerator.MoveNext(out var ent))
            {
                if (blobQuery.TryGetComponent(ent, out var blob) && blob.RootTile == rootTile)
                {
                    hasAdjacentBlob = true;
                    break;
                }
            }
            if (hasAdjacentBlob)
                break;
        }

        if (!hasAdjacentBlob)
            return false;

        var targetEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, targetTile);
        while (targetEnumerator.MoveNext(out var ent))
        {
            if (blobQuery.HasComponent(ent))
                return false;
        }

        EntityUid? sourceTile = null;
        float sourceLevel = 0;
        for (var i = 0; i < 4; i++)
        {
            var atmosDir = (AtmosDirection)(1 << i);
            var neighborPos = targetTile.Offset(atmosDir);
            var neighborEnumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, neighborPos);
            while (neighborEnumerator.MoveNext(out var ent))
            {
                if (!blobQuery.TryGetComponent(ent, out var blob) || blob.RootTile != rootTile || blob.LiquidLevel < SpreadCost)
                    continue;
                if (sourceTile == null || sourceLevel < blob.LiquidLevel)
                {
                    sourceTile = ent;
                    sourceLevel = blob.LiquidLevel;
                }
            }
        }

        if (sourceTile == null)
        {
            var allBlobQuery = EntityQueryEnumerator<LiquidBlobTileComponent>();
            while (allBlobQuery.MoveNext(out var ent, out var blob))
            {
                if (blob.RootTile != rootTile || blob.LiquidLevel < SpreadCost)
                    continue;
                if (sourceTile == null || sourceLevel < blob.LiquidLevel)
                {
                    sourceTile = ent;
                    sourceLevel = blob.LiquidLevel;
                }
            }
        }

        if (sourceTile == null)
            return false;

        var sourceComp = Comp<LiquidBlobTileComponent>(sourceTile.Value);
        sourceComp.LiquidLevel -= SpreadCost;
        Dirty(sourceTile.Value, sourceComp);

        var spawnCoords = _map.GridTileToLocal(gridUid, grid, targetTile);
        var newTile = Spawn("LiquidBlobTile", spawnCoords);
        var newComp = Comp<LiquidBlobTileComponent>(newTile);
        newComp.RootTile = rootTile;
        newComp.LiquidLevel = 0;
        Dirty(newTile, newComp);

        return true;
    }
}
