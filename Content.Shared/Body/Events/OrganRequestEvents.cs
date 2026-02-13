using Robust.Shared.Map;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised to request inserting an organ into a body part. Handled by BodyPartOrganSystem.
/// </summary>
[ByRefEvent]
public record struct OrganInsertRequestEvent(EntityUid BodyPart, EntityUid Organ)
{
    /// <summary>
    /// Whether the insert succeeded. Populated by BodyPartOrganSystem.
    /// </summary>
    public bool Success { get; set; }
}

/// <summary>
/// Raised to request removing an organ from its container. Handled by BodyPartOrganSystem.
/// </summary>
[ByRefEvent]
public record struct OrganRemoveRequestEvent(EntityUid Organ)
{
    /// <summary>
    /// Where to place the organ after removal. If null, uses default AttachParentToContainerOrGrid behavior.
    /// </summary>
    public EntityCoordinates? Destination;

    /// <summary>
    /// Whether the remove succeeded. Populated by BodyPartOrganSystem.
    /// </summary>
    public bool Success { get; set; }
}
