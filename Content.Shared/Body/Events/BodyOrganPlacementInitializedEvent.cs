using Content.Shared.Body.Components;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised by BodySystem after body initialization is complete and containers are set up.
/// This event signals that organ placement initialization can begin.
/// </summary>
[ByRefEvent]
public readonly record struct BodyOrganPlacementInitializedEvent(Entity<BodyComponent> Body);
