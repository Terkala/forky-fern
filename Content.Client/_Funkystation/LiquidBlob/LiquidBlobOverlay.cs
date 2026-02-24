using Content.Shared._Funkystation.LiquidBlob.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;

namespace Content.Client._Funkystation.LiquidBlob;

public sealed class LiquidBlobOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "LiquidBlob";

    public const int MaxTiles = 64;

    private const float MaxDistance = 15f;

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ILogManager _logMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private SharedTransformSystem? _xformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;
    private readonly Vector2[] _positions = new Vector2[MaxTiles];
    private readonly float[] _liquidLevels = new float[MaxTiles];

    private readonly List<List<(Vector2 Pos, float LiquidLevel)>> _blobs = new();

    private const float TileSizePixels = 32f;
    private const float Threshold = 0.15f;
    private const float OutlineWidth = 0.08f;
    private const float WaveSpeed = 0.5f;
    private const float WaveAmplitude = 0.03f;
    private static readonly Color BlobColor = new(0.2f, 0.8f, 0.4f, 0.9f);

    public LiquidBlobOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).Instance().Duplicate();
    }

    private ISawmill Sawmill => _logMan.GetSawmill("liquidblob.overlay");

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return false;

        if (_xformSystem == null && !_entMan.TrySystem(out _xformSystem))
            return false;

        var blobsByRoot = new Dictionary<EntityUid, List<(Vector2 Pos, float LiquidLevel)>>();
        var query = _entMan.EntityQueryEnumerator<LiquidBlobTileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var blob, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var mapPos = _xformSystem!.GetWorldPosition(uid);
            if ((mapPos - args.WorldAABB.ClosestPoint(mapPos)).LengthSquared() > MaxDistance * MaxDistance)
                continue;

            var tempCoords = args.Viewport.WorldToLocal(mapPos);
            tempCoords.Y = args.Viewport.Size.Y - tempCoords.Y;

            var root = blob.RootTile ?? uid;
            if (!blobsByRoot.TryGetValue(root, out var list))
            {
                list = new List<(Vector2, float)>();
                blobsByRoot[root] = list;
            }
            list.Add((tempCoords, blob.LiquidLevel / blob.MaxCapacity));
        }

        _blobs.Clear();
        foreach (var list in blobsByRoot.Values)
        {
            if (list.Count > 0)
                _blobs.Add(list);
        }

        if (_blobs.Count > 0)
        {
            var totalTiles = _blobs.Sum(b => b.Count);
            Sawmill.Debug($"Drawing {_blobs.Count} blob(s) with {totalTiles} tile(s)");
        }

        return _blobs.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return;

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);

        var scale = args.Viewport.RenderScale * args.Viewport.Eye.Scale;
        _shader?.SetParameter("renderScale", (scale.X + scale.Y) * 0.5f);
        _shader?.SetParameter("tileSize", TileSizePixels);
        _shader?.SetParameter("threshold", Threshold);
        _shader?.SetParameter("outlineWidth", OutlineWidth);
        _shader?.SetParameter("waveSpeed", WaveSpeed);
        _shader?.SetParameter("waveAmplitude", WaveAmplitude);
        _shader?.SetParameter("blobColor", BlobColor);

        foreach (var blob in _blobs)
        {
            var count = Math.Min(blob.Count, MaxTiles);
            for (var i = 0; i < count; i++)
            {
                _positions[i] = blob[i].Pos;
                _liquidLevels[i] = blob[i].LiquidLevel;
            }

            _shader?.SetParameter("tileCount", count);
            _shader?.SetParameter("positions", _positions);
            _shader?.SetParameter("liquidLevels", _liquidLevels);

            worldHandle.DrawRect(args.WorldAABB, Color.White);
        }

        worldHandle.UseShader(null);
    }
}
