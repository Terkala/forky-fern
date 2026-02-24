using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.LiquidBlob.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LiquidBlobTileComponent : Component
{
    [DataField, AutoNetworkedField]
    public float LiquidLevel;

    [DataField]
    public float MaxCapacity = 20f;

    [DataField]
    public float ProductionPerSecond = 0.5f;

    [DataField, AutoNetworkedField]
    public EntityUid? RootTile;
}
