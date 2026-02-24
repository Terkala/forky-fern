using Robust.Client.Graphics;

namespace Content.Client._Funkystation.LiquidBlob;

public sealed class LiquidBlobOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    private LiquidBlobOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LiquidBlobOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
        }
    }
}
