using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.LiquidBlob.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class LiquidBlobObserverComponent : Component
{
    [DataField]
    public EntityUid RootTile;
}
