using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

/// <summary>
/// Client-only visual: brief sprite wiggle on a normal blob tile contributing lash damage.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayBlobLashWiggleEvent : EntityEventArgs
{
    public NetEntity Tile { get; }

    public PlayBlobLashWiggleEvent(NetEntity tile)
    {
        Tile = tile;
    }
}
