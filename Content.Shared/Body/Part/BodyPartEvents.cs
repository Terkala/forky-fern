namespace Content.Shared.Body.Part;

/// <summary>
/// Raised on body part entity when it is attached to a body or parent part.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartAttachedEvent(EntityUid Body, EntityUid? Parent);

/// <summary>
/// Raised on body part entity when it is detached from a body or parent part.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartDetachedEvent(EntityUid? Body, EntityUid? Parent);

/// <summary>
/// Raised on body entity when a body part is attached to it.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartAddedToBodyEvent(EntityUid BodyPart);

/// <summary>
/// Raised on body entity when a body part is detached from it.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartRemovedFromBodyEvent(EntityUid BodyPart);
