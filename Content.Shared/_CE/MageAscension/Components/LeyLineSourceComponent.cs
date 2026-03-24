namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Marks an entity (confluence) as a ley source for <see cref="LeyLineManaReceiverComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class LeyLineSourceComponent : Component
{
    [DataField]
    public bool Enabled = true;
}
