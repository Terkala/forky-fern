using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised after a body part has been fully detached and appearance/detached-entity handling is complete.
/// Use this event when you need to react to body part removal without subscribing to BodyPartAppearanceHandledEvent
/// (which may have duplicate subscription constraints).
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartFullyDetachedEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> BodyPart);
