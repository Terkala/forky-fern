using Content.Shared.Body.Components;

namespace Content.Shared.Body.Events;

/// <summary>
/// Raised after BodySystem has finished initializing a BodyComponent.
/// This event is raised after containers are set up, allowing other systems
/// to perform additional initialization that depends on the body being ready.
/// </summary>
[ByRefEvent]
public readonly record struct BodyInitializedEvent(Entity<BodyComponent> Body);
