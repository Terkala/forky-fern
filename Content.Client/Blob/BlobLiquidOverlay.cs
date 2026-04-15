using System;
using System.Numerics;
using Content.Client.Graphics;
using Content.Client.Parallax;
using Content.Shared.Blob;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Blob;

/// <summary>
/// Non-authoritative blob visualization: renders a continuous world-stationary
/// kudzu texture through a blob tile mask.
/// </summary>
public sealed class BlobLiquidOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    private static readonly ProtoId<ShaderPrototype> StencilMask = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDraw = "StencilEqualDraw";
    private static readonly SpriteSpecifier BlobSprite = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/Objects/Misc/kudzu.rsi"),
        "kudzu_11");

    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;
    private ParallaxSystem? _parallax;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly List<Vector2> _scratch = new();

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
        if (_sprite is null && !_ent.TrySystem(out _sprite))
            return false;
        if (_parallax is null && !_ent.TrySystem(out _parallax))
            return false;

        _scratch.Clear();
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

            _scratch.Add(world);
        }

        return _scratch.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var invMatrix = args.Viewport.GetWorldToLocalMatrix();
        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (res.Mask?.Texture.Size != args.Viewport.Size)
        {
            res.Mask?.Dispose();
            res.Mask = _clyde.CreateRenderTarget(
                args.Viewport.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "blob-liquid-mask");
        }

        var tileHalfExtentPixels = args.Viewport.RenderScale.X / (args.Viewport.Eye?.Zoom.X ?? 1f) * EyeManager.PixelsPerMeter * 0.5f;
        var revealRadius = tileHalfExtentPixels * 1.30f;
        var maxExtraRadius = tileHalfExtentPixels * 0.20f; // ~= +0.1 tile width at maximum
        var wobbleOffsetMax = tileHalfExtentPixels * 0.08f;
        var now = (float) _timing.RealTime.TotalSeconds;

        handle.RenderInRenderTarget(res.Mask!, () =>
        {
            foreach (var world in _scratch)
            {
                var local = Vector2.Transform(world, invMatrix);

                // Base fill so the interior stays solid.
                handle.DrawCircle(local, revealRadius, Color.White);

                // Irregular "breathing" edge: layered circles with unique per-tile phase.
                var seed = world.X * 12.9898f + world.Y * 78.233f;
                for (var i = 0; i < 5; i++)
                {
                    var lobePhase = seed + i * 1.618f;
                    var angle = i * (MathF.Tau / 5f) + lobePhase * 0.1f;
                    var pulse = 0.5f + 0.5f * MathF.Sin(now * (1.2f + i * 0.17f) + lobePhase);
                    var pulse2 = 0.5f + 0.5f * MathF.Sin(now * (1.9f + i * 0.13f) - lobePhase * 0.7f);

                    var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (wobbleOffsetMax * pulse2);
                    var lobeRadius = revealRadius + maxExtraRadius * pulse;
                    handle.DrawCircle(local + offset, lobeRadius, Color.White);
                }
            }
        }, Color.Transparent);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_proto.Index(StencilMask).Instance());
        handle.DrawTextureRect(res.Mask!.Texture, args.WorldBounds);

        var curTime = _timing.RealTime;
        var sprite = _sprite!.GetFrame(BlobSprite, curTime);
        var eyePos = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;

        handle.UseShader(_proto.Index(StencilEqualDraw).Instance());
        _parallax!.DrawParallax(
            handle,
            args.WorldAABB,
            sprite,
            curTime,
            eyePos,
            Vector2.Zero,
            modulate: Color.White.WithAlpha(0.85f));

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Mask;

        public void Dispose()
        {
            Mask?.Dispose();
        }
    }
}
