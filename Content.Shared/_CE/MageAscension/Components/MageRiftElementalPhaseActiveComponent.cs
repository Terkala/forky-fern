using System;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Active wall-phase on the elemental rift horror; restored on shutdown or timeout.
/// </summary>
[RegisterComponent]
public sealed partial class MageRiftElementalPhaseActiveComponent : Component
{
    /// <summary>
    /// Absolute game time when phase ends.
    /// </summary>
    [DataField]
    public TimeSpan EndTime;

    [DataField]
    public bool SavedCanCollide = true;
}
