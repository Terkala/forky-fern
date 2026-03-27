using System.Numerics;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Short-lived mana mote moving toward <see cref="Target"/>.
/// Leyline targets snapshot <see cref="StaticHomingWorld"/> so motion does not re-query their transform.
/// </summary>
[RegisterComponent]
public sealed partial class MageManaMoteComponent : Component
{
    public EntityUid Target;

    /// <summary>
    /// If set, world position to home toward (snapshotted leyline location). If null, use <see cref="Target"/> each frame (mages).
    /// </summary>
    public Vector2? StaticHomingWorld;

    [DataField]
    public float Speed = 6f;
}
