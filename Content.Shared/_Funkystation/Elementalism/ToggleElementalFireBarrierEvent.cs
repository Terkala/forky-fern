using System;

namespace Content.Shared._Funkystation.Elementalism;

/// <summary>
/// Raised on the game bus when the fire barrier spell toggles (server-only handling).
/// </summary>
public sealed class ToggleElementalFireBarrierEvent(EntityUid target) : EntityEventArgs
{
    public EntityUid Target { get; } = target;
}
