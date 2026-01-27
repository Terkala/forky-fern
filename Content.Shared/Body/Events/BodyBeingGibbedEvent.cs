using Content.Shared.Body.Components;
using Content.Shared.Gibbing;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodySystem after relaying BeingGibbedEvent to organs.
/// Allows other systems (like DetachedBodyPartSystem) to react to body gibbing
/// without conflicting with the relay subscription.
/// This event is raised during the BeingGibbedEvent handling, before the gib completes,
/// ensuring body parts can be detached successfully.
/// Note: The GibbingEvent contains a HashSet which is a reference type, so modifications
/// to Giblets will be reflected in the original event.
/// </summary>
[ByRefEvent]
public readonly record struct BodyBeingGibbedEvent(Entity<BodyComponent> Body, BeingGibbedEvent GibbingEvent);
