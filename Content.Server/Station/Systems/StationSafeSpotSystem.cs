using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Components;
using Content.Shared.Physics;
using Content.Shared.Random.Helpers;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.SubFloor;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Log;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server.Station.Systems;

public enum StationLocateQuality
{
    Strict,
    RandomTile,
    Arbitrary,
}

/// <summary>
/// Parameters for <see cref="StationSafeSpotSystem.TryLocateSafeSpotOnStation"/>.
/// </summary>
public sealed class StationSafeSpotLocateSpec
{
    public required Entity<StationDataComponent> Station { get; init; }

    /// <summary>
    /// Width in tiles of the open area (all tiles must pass strict checks). Centered on the chosen anchor tile.
    /// </summary>
    public int FootprintWidth { get; init; } = 3;

    /// <summary>
    /// Height in tiles of the open area (all tiles must pass strict checks). Centered on the chosen anchor tile.
    /// </summary>
    public int FootprintHeight { get; init; } = 3;

    /// <summary> If set, strict search runs in a tile box around this position first. </summary>
    public EntityCoordinates? LocalAnchor { get; init; }
    public int LocalHalfExtent { get; init; } = 5;
    /// <summary> After local strict, this many random station-wide strict samples (0 skips). </summary>
    public int StationWideStrictAttempts { get; init; } = 40;
}

/// <summary>
/// Finds tiles on station grids: strict (Blood Cult–style) rules, then weak random tile, then a guaranteed fallback.
/// </summary>
public sealed class StationSafeSpotSystem : EntitySystem
{
    private const float MinPressureKpa = 50f;
    private const float MaxPressureKpa = 300f;
    private const float MinTemperatureK = 150f;
    private const float MaxTemperatureK = 300f;

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("station.safe_spot");
    }

    /// <summary>
    /// Picks a random station that is eligible for random events (same pool as <see cref="GameTicking.Rules.GameRuleSystem{T}.TryGetRandomStation"/>).
    /// </summary>
    public bool TryGetRandomEventEligibleStation([NotNullWhen(true)] out EntityUid? station, Func<EntityUid, bool>? filter = null)
    {
        var stations = new ValueList<EntityUid>(Count<StationEventEligibleComponent>());
        filter ??= _ => true;
        var query = AllEntityQuery<StationEventEligibleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!filter(uid))
                continue;
            stations.Add(uid);
        }

        if (stations.Count == 0)
        {
            station = null;
            return false;
        }

        station = stations[_random.Next(stations.Count)];
        return true;
    }

    /// <summary>
    /// Same behavior as <see cref="GameTicking.Rules.GameRuleSystem{T}.TryFindRandomTileOnStation"/> (weak checks only).
    /// </summary>
    public bool TryFindRandomTileOnStationWeak(
        Entity<StationDataComponent> station,
        out Vector2i tile,
        out EntityUid targetGrid,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetCoords = EntityCoordinates.Invalid;
        targetGrid = EntityUid.Invalid;

        var weights = new Dictionary<Entity<MapGridComponent>, float>();
        foreach (var possibleTarget in station.Comp.Grids)
        {
            if (!Exists(possibleTarget) || !TryComp<MapGridComponent>(possibleTarget, out var comp))
                continue;

            weights.Add((possibleTarget, comp), _map.GetAllTiles(possibleTarget, comp).Count());
        }

        if (weights.Count == 0)
            return false;

        (targetGrid, var gridComp) = _random.Pick(weights);
        if (!Exists(targetGrid) || !TryComp<TransformComponent>(targetGrid, out var targetGridXform))
            return false;

        var aabb = gridComp.LocalAABB;

        for (var i = 0; i < 10; i++)
        {
            tile = RandomTileIndicesInAabb(aabb, _random);
            if (_atmosphere.IsTileSpace(targetGrid, targetGridXform.MapUid, tile)
                || _atmosphere.IsTileAirBlockedCached(targetGrid, tile))
            {
                continue;
            }

            targetCoords = _map.GridTileToLocal(targetGrid, gridComp, tile);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Picks a random tile index inside the grid AABB. When the AABB is empty or too small for
    /// <see cref="IRobustRandom.Next(int,int)"/> (requires min &lt; max), expands bounds so a single valid range exists.
    /// </summary>
    private static Vector2i RandomTileIndicesInAabb(Box2 aabb, IRobustRandom random)
    {
        var minX = (int)Math.Floor(aabb.Left);
        var maxX = (int)Math.Ceiling(aabb.Right);
        if (maxX <= minX)
            maxX = minX + 1;

        var minY = (int)Math.Floor(aabb.Bottom);
        var maxY = (int)Math.Ceiling(aabb.Top);
        if (maxY <= minY)
            maxY = minY + 1;

        return new Vector2i(random.Next(minX, maxX), random.Next(minY, maxY));
    }

    /// <summary>
    /// Locates a spot on the station: strict rules first, then weak random tile, then a deterministic tile in grid AABB.
    /// When the station has at least one grid, outputs are always filled and the method returns true.
    /// </summary>
    public bool TryLocateSafeSpotOnStation(
        StationSafeSpotLocateSpec spec,
        out EntityUid gridUid,
        out Vector2i anchorTile,
        out EntityCoordinates localCoords,
        out StationLocateQuality quality)
    {
        gridUid = EntityUid.Invalid;
        anchorTile = default;
        localCoords = EntityCoordinates.Invalid;
        quality = StationLocateQuality.Arbitrary;

        if (!TryBuildGridWeights(spec.Station, out var weights))
            return false;

        var width = Math.Max(1, spec.FootprintWidth);
        var height = Math.Max(1, spec.FootprintHeight);

        // 1) Strict: local box
        if (spec.LocalAnchor is { } anchor)
        {
            if (TryResolveGrid(anchor, out var gUid, out var grid))
            {
                if (_station.GetOwningStation(gUid) == spec.Station.Owner)
                {
                    var centerTile = _map.TileIndicesFor(gUid, grid, anchor);
                    var half = spec.LocalHalfExtent;
                    for (var x = -half; x <= half; x++)
                    {
                        for (var y = -half; y <= half; y++)
                        {
                            var testTile = new Vector2i(centerTile.X + x, centerTile.Y + y);
                            if (!IsValidOpenAreaAtCenter(gUid, grid, testTile, width, height))
                                continue;

                            AssignOpenAreaSuccess(gUid, grid, testTile, width, height, out gridUid, out anchorTile, out localCoords);
                            quality = StationLocateQuality.Strict;
                            return true;
                        }
                    }
                }
            }
        }

        // 2) Strict: station-wide random samples
        for (var attempt = 0; attempt < spec.StationWideStrictAttempts; attempt++)
        {
            var (gUid, gridComp) = _random.Pick(weights);
            if (!Exists(gUid) || !TryComp<TransformComponent>(gUid, out _))
                continue;

            var aabb = gridComp.LocalAABB;
            var testTile = RandomTileIndicesInAabb(aabb, _random);

            if (!IsValidOpenAreaAtCenter(gUid, gridComp, testTile, width, height))
                continue;

            AssignOpenAreaSuccess(gUid, gridComp, testTile, width, height, out gridUid, out anchorTile, out localCoords);
            quality = StationLocateQuality.Strict;
            return true;
        }

        // 3) Fallback A: weak random tile (GameRule equivalent), then strict local scan for larger footprints
        if (TryFindRandomTileOnStationWeak(spec.Station, out var weakTile, out var weakGrid, out _)
            && TryComp<MapGridComponent>(weakGrid, out var weakGridComp))
        {
            if (width == 1 && height == 1)
            {
                AssignOpenAreaSuccess(weakGrid, weakGridComp, weakTile, 1, 1, out gridUid, out anchorTile, out localCoords);
                quality = StationLocateQuality.RandomTile;
                return true;
            }

            var centerTile = weakTile;
            const int scanHalf = 5;
            for (var x = -scanHalf; x <= scanHalf; x++)
            {
                for (var y = -scanHalf; y <= scanHalf; y++)
                {
                    var testTile = new Vector2i(centerTile.X + x, centerTile.Y + y);
                    if (!IsValidOpenAreaAtCenter(weakGrid, weakGridComp, testTile, width, height))
                        continue;

                    AssignOpenAreaSuccess(weakGrid, weakGridComp, testTile, width, height, out gridUid, out anchorTile, out localCoords);
                    quality = StationLocateQuality.Strict;
                    return true;
                }
            }

            AssignOpenAreaSuccess(weakGrid, weakGridComp, weakTile, 1, 1, out gridUid, out anchorTile, out localCoords);
            quality = StationLocateQuality.RandomTile;
            return true;
        }

        // 4) Fallback B: center of AABB of a weighted grid
        var pickedEnt = _random.Pick(weights);
        EntityUid fbGrid;
        MapGridComponent fbComp;
        if (Exists(pickedEnt.Owner) && TryComp<TransformComponent>(pickedEnt.Owner, out _))
        {
            fbGrid = pickedEnt.Owner;
            fbComp = pickedEnt.Comp;
        }
        else
        {
            fbGrid = EntityUid.Invalid;
            fbComp = default!;
            foreach (var gridKey in weights.Keys)
            {
                if (!Exists(gridKey.Owner) || !TryComp<TransformComponent>(gridKey.Owner, out _))
                    continue;
                fbGrid = gridKey.Owner;
                fbComp = gridKey.Comp;
                break;
            }

            if (!fbGrid.IsValid())
                return false;
        }

        var fbAabb = fbComp.LocalAABB;
        var centerCoords = new EntityCoordinates(fbGrid, new Vector2(fbAabb.Center.X, fbAabb.Center.Y));
        anchorTile = _map.TileIndicesFor(fbGrid, fbComp, centerCoords);
        gridUid = fbGrid;
        if (IsValidOpenAreaAtCenter(fbGrid, fbComp, anchorTile, width, height))
            AssignOpenAreaSuccess(fbGrid, fbComp, anchorTile, width, height, out gridUid, out anchorTile, out localCoords);
        else
            AssignOpenAreaSuccess(fbGrid, fbComp, anchorTile, 1, 1, out gridUid, out anchorTile, out localCoords);
        quality = StationLocateQuality.Arbitrary;
        _sawmill.Warning(
            "TryLocateSafeSpotOnStation used arbitrary fallback (center tile) on grid {0} tile {1}",
            fbGrid,
            anchorTile);
        return true;
    }

    /// <summary>
    /// Tile offsets from a center tile so that center + [-neg..+pos] covers <paramref name="size"/> contiguous tiles.
    /// </summary>
    private static void GetRectOffsetsFromCenter(int size, out int neg, out int pos)
    {
        pos = (size - 1) / 2;
        neg = (size - 1) - pos;
    }

    private void AssignOpenAreaSuccess(
        EntityUid gUid,
        MapGridComponent grid,
        Vector2i centerTile,
        int width,
        int height,
        out EntityUid gridUid,
        out Vector2i anchorTile,
        out EntityCoordinates localCoords)
    {
        GetRectOffsetsFromCenter(width, out var negX, out var posX);
        GetRectOffsetsFromCenter(height, out var negY, out var posY);
        var minTx = centerTile.X - negX;
        var minTy = centerTile.Y - negY;
        var maxTx = centerTile.X + posX;
        var maxTy = centerTile.Y + posY;

        gridUid = gUid;
        anchorTile = centerTile;

        var p00 = _map.GridTileToLocal(gUid, grid, new Vector2i(minTx, minTy)).Position;
        var p11 = _map.GridTileToLocal(gUid, grid, new Vector2i(maxTx, maxTy)).Position;
        localCoords = new EntityCoordinates(gUid, (p00 + p11) * 0.5f);
    }

    private bool TryBuildGridWeights(Entity<StationDataComponent> station, out Dictionary<Entity<MapGridComponent>, float> weights)
    {
        weights = new Dictionary<Entity<MapGridComponent>, float>();
        foreach (var possibleTarget in station.Comp.Grids)
        {
            if (!Exists(possibleTarget) || !TryComp<MapGridComponent>(possibleTarget, out var comp))
                continue;

            weights.Add((possibleTarget, comp), _map.GetAllTiles(possibleTarget, comp).Count());
        }

        return weights.Count > 0;
    }

    private bool TryResolveGrid(EntityCoordinates coords, out EntityUid gridUid, out MapGridComponent grid)
    {
        gridUid = EntityUid.Invalid;
        grid = default!;

        if (EntityManager.TryGetComponent<MapGridComponent>(coords.EntityId, out var directGrid) && directGrid != null)
        {
            gridUid = coords.EntityId;
            grid = directGrid;
            return true;
        }

        var resolvedGrid = _transform.GetGrid(coords);
        if (resolvedGrid is not { } gridEntity)
            return false;

        if (!EntityManager.TryGetComponent<MapGridComponent>(gridEntity, out var resolvedComp) || resolvedComp == null)
            return false;

        gridUid = gridEntity;
        grid = resolvedComp;
        return true;
    }

    private bool IsValidOpenAreaAtCenter(EntityUid gridUid, MapGridComponent grid, Vector2i centerTile, int width, int height)
    {
        if (width < 1 || height < 1)
            return false;

        GetRectOffsetsFromCenter(width, out var negX, out var posX);
        GetRectOffsetsFromCenter(height, out var negY, out var posY);

        for (var dx = -negX; dx <= posX; dx++)
        {
            for (var dy = -negY; dy <= posY; dy++)
            {
                if (!IsTileStrictValid(gridUid, grid, new Vector2i(centerTile.X + dx, centerTile.Y + dy)))
                    return false;
            }
        }

        return true;
    }

    private bool IsTileStrictValid(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!Exists(gridUid) || !TryComp<TransformComponent>(gridUid, out var gridXform))
            return false;

        var tileRef = _map.GetTileRef(gridUid, grid, tile);
        if (tileRef.Tile.IsEmpty)
            return false;

        var mixture = _atmosphere.GetTileMixture(gridUid, gridXform.MapUid, tile, excite: false);
        if (mixture == null)
            return false;

        if (mixture.Pressure < MinPressureKpa || mixture.Pressure > MaxPressureKpa)
            return false;
        if (mixture.Temperature < MinTemperatureK || mixture.Temperature > MaxTemperatureK)
            return false;

        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        foreach (var entity in anchored)
        {
            if (HasComp<SubFloorHideComponent>(entity))
                continue;

            if (TryComp<PhysicsComponent>(entity, out var physics))
            {
                var blockingLayers = CollisionGroup.Impassable | CollisionGroup.WallLayer | CollisionGroup.GlassLayer |
                                     CollisionGroup.FullTileLayer | CollisionGroup.AirlockLayer | CollisionGroup.GlassAirlockLayer;
                if ((physics.CollisionLayer & (int)blockingLayers) != 0)
                    return false;
            }
        }

        return true;
    }
}
