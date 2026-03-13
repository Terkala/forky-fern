using Robust.Shared.Map;

namespace Content.Server.NPC.Companion.Components;

/// <summary>
/// Marks an entity as companion AI. Holds the owner reference and last known position for off-grid tracking.
/// </summary>
[RegisterComponent]
public sealed partial class NPCCompanionComponent : Component
{
    /// <summary>
    /// The entity that owns this companion.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? Owner;

    /// <summary>
    /// Last known on-grid position of the owner. Used when owner goes off-grid for the companion to travel to.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public MapCoordinates LastKnownOwnerPosition;

    /// <summary>
    /// Whether the owner was on a grid during the last update. Used to detect off-grid transitions.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool OwnerWasOnGrid;
}
