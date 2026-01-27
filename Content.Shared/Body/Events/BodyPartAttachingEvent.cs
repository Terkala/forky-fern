using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodySystem when a body part is being attached to a body.
/// This is the PRIMARY event for attachment logic.
/// Systems should subscribe to this instead of BodyPartAddedToBodyEvent for cross-system coordination.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartAttachingEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> BodyPart);
