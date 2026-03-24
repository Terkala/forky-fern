namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Short-lived mana mote moving toward <see cref="Target"/>.
/// </summary>
[RegisterComponent]
public sealed partial class MageManaMoteComponent : Component
{
    public EntityUid Target;

    [DataField]
    public float Speed = 6f;
}
