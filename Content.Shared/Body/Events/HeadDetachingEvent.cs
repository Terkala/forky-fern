using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodySystem when a head is being detached from a body.
/// Specialized event for head-specific logic (species abilities, etc.)
/// Systems should subscribe to this for head-specific detachment handling.
/// </summary>
[ByRefEvent]
public readonly record struct HeadDetachingEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> HeadPart);
