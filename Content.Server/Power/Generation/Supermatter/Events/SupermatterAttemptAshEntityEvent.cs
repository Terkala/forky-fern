using Content.Server.Power.Generation.Supermatter.Components;

namespace Content.Server.Power.Generation.Supermatter.Events;

/// <summary>
/// Event raised on the target entity whenever a supermatter attempts to ash an entity.
/// Can be cancelled to prevent the target entity from being ashed.
/// </summary>
[ByRefEvent]
public record struct SupermatterAttemptAshEntityEvent
(EntityUid entity, EntityUid supermatterUid, SupermatterProcessingComponent processing)
{
    /// <summary>
    /// The entity that the supermatter is attempting to ash.
    /// </summary>
    public readonly EntityUid Entity = entity;

    /// <summary>
    /// The uid of the supermatter.
    /// </summary>
    public readonly EntityUid SupermatterUid = supermatterUid;

    /// <summary>
    /// The supermatter processing component.
    /// </summary>
    public readonly SupermatterProcessingComponent Processing = processing;

    /// <summary>
    /// Whether the supermatter has been prevented from ashing the target entity.
    /// </summary>
    public bool Cancelled = false;
}
