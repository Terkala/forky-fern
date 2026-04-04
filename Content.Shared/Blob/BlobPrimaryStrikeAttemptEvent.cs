using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Blob;

/// <summary>
/// Raised directed on the blob overmind on the server to apply a primary (left-click) strike at the given world coords.
/// </summary>
public sealed class BlobPrimaryStrikeAttemptEvent : EntityEventArgs
{
    public NetCoordinates Coordinates { get; }

    public BlobPrimaryStrikeAttemptEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}
