using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodySystem when a body part is being detached from a body.
/// This event is raised on the body entity when a body part is removed (from root container or parent body parts).
/// Other systems should subscribe to this event instead of BodyPartDetachedEvent to avoid duplicate subscriptions.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartDetachingEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> BodyPart);
