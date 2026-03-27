namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Marks an entity as a ley line anchor (e.g. ley confluence). <see cref="Enabled"/> tracks whether the
/// node is active (opened confluence vs dormant). Used for gameplay state, not for aura mana regen.
/// </summary>
[RegisterComponent]
public sealed partial class LeyLineSourceComponent : Component
{
    /// <summary>
    /// False until the confluence is opened, then true—indicates an active ley knot on the map.
    /// </summary>
    [DataField]
    public bool Enabled = true;
}
