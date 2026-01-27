using Content.Shared.Body.Components;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodyPartAppearanceSystem after it has handled appearance changes for a body part detachment.
/// This event is raised after appearance layers have been hidden, allowing other systems (like DetachedBodyPartSystem)
/// to perform their logic without subscribing to the same event as BodyPartAppearanceSystem.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartAppearanceHandledEvent(Entity<BodyComponent> Body, Entity<BodyPartComponent> BodyPart);
