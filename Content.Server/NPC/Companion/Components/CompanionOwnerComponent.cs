namespace Content.Server.NPC.Companion.Components;

/// <summary>
/// Tracks companions owned by this entity. Placed on the owner (e.g. player).
/// </summary>
[RegisterComponent]
public sealed partial class CompanionOwnerComponent : Component
{
    /// <summary>
    /// Entities that are companions of this owner.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<EntityUid> Companions = new();
}
