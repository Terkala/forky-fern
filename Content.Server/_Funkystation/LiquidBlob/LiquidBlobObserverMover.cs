using Content.Shared._Funkystation.LiquidBlob.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Funkystation.LiquidBlob;

public sealed class LiquidBlobObserverMover : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LiquidBlobObserverComponent, MoveEvent>(OnObserverMoved);
    }

    private void OnObserverMoved(EntityUid uid, LiquidBlobObserverComponent comp, ref MoveEvent args)
    {
        if (TerminatingOrDeleted(comp.RootTile))
            return;

        var newPos = args.NewPosition;

        if (!TryComp(comp.RootTile, out TransformComponent? rootXform))
            return;

        var rootTile = comp.RootTile;

        if (newPos.EntityId != rootXform.GridUid)
        {
            _transform.SetCoordinates(uid, rootXform.Coordinates);
            return;
        }

        if (!TryComp(newPos.EntityId, out MapGridComponent? grid))
        {
            _transform.SetCoordinates(uid, rootXform.Coordinates);
            return;
        }

        var newTile = _map.TileIndicesFor(newPos.EntityId, grid, newPos);
        var blobQuery = GetEntityQuery<LiquidBlobTileComponent>();
        var foundBlob = false;

        foreach (var ent in _map.GetAnchoredEntities(newPos.EntityId, grid, newTile))
        {
            if (blobQuery.TryGetComponent(ent, out var blobTile) && blobTile.RootTile == rootTile)
            {
                foundBlob = true;
                break;
            }
        }

        if (!foundBlob)
            _transform.SetCoordinates(uid, rootXform.Coordinates);
    }
}
