using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodySystem when a head is being attached to a body.
/// Specialized event for head-specific logic (species abilities, etc.)
/// Systems should subscribe to this for head-specific attachment handling.
/// </summary>
[ByRefEvent]
public readonly record struct HeadAttachingEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> HeadPart);
