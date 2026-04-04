using System;
using System.Numerics;
using Content.Shared.Blob;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;

namespace Content.Client.Blob;

/// <summary>
/// Non-authoritative "liquid" visualization: soft circles per tile (feather) plus a smaller saturated core per cell.
/// </summary>
public sealed class BlobLiquidOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _ent = default!;
    private SharedTransformSystem? _xform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly List<(Vector2 Pos, Color Tint, NetEntity Hive)> _scratch = new();
    private readonly Dictionary<NetEntity, int> _hiveTileTotals = new();

    public BlobLiquidOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 80;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return false;

        if (_xform is null && !_ent.TrySystem(out _xform))
            return false;

        _scratch.Clear();
        _hiveTileTotals.Clear();
        var query = _ent.EntityQueryEnumerator<BlobTileComponent, TransformComponent>();
        while (query.MoveNext(out _, out var tile, out var xform))
        {
            if (tile.Kind == BlobTileKind.Bridge)
                continue;
            if (xform.MapID != args.MapId)
                continue;

            var world = _xform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(world))
                continue;

            var tint = Color.FromHex("#8FBA8F");
            if (_ent.TryGetEntity(tile.Hive, out var hiveUid) &&
                _ent.TryGetComponent<BlobHiveComponent>(hiveUid, out var hive))
            {
                tint = hive.Tint;
                if (!_hiveTileTotals.ContainsKey(tile.Hive))
                    _hiveTileTotals[tile.Hive] = Math.Max(1, hive.TileCount);
            }
            else if (!_hiveTileTotals.ContainsKey(tile.Hive))
            {
                _hiveTileTotals[tile.Hive] = 1;
            }

            _scratch.Add((world, tint, tile.Hive));
        }

        return _scratch.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        const float baseFeather = 0.42f;
        const float baseCore = 0.28f;
        const float scaleDivisor = 22f;
        const float maxScale = 2.35f;

        foreach (var (pos, tint, hiveNet) in _scratch)
        {
            var n = _hiveTileTotals.GetValueOrDefault(hiveNet, 1);
            var scale = MathF.Min(maxScale, 1f + MathF.Sqrt(n) / scaleDivisor);
            var featherR = baseFeather * scale;
            var coreR = baseCore * scale;
            handle.DrawCircle(pos, featherR, tint.WithAlpha(0.20f));
            handle.DrawCircle(pos, coreR, tint.WithAlpha(0.48f));
        }
    }
}
