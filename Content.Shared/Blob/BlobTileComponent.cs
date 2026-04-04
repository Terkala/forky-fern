using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.Blob;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobTileComponent : Component
{
    /// <summary>
    /// Hive owner (typically the blob overmind entity with <see cref="BlobHiveComponent"/>).
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity Hive;

    [DataField, AutoNetworkedField]
    public BlobTileKind Kind = BlobTileKind.Normal;
}
