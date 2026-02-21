namespace Content.Shared.Cybernetics.Events;

/// <summary>
/// Raised when a cyber limb is inserted into a body's container.
/// Used by CyberLimbStatsSystem to add/update stats without duplicating container subscriptions.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbAttachedToBodyEvent(EntityUid Body);

/// <summary>
/// Raised when a cyber limb is removed from a body's container.
/// Used by CyberLimbStatsSystem to remove/update stats without duplicating container subscriptions.
/// </summary>
[ByRefEvent]
public readonly record struct CyberLimbDetachedFromBodyEvent(EntityUid Body);
