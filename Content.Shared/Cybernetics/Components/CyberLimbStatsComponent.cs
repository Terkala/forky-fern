using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Caches cyber limb stats for a body. Service time is a shared pool that drains when limbs are installed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbStatsComponent : Component
{
    /// <summary>
    /// Remaining service time in the shared pool. Ticks down; does not use a future timestamp.
    /// </summary>
    [DataField]
    public TimeSpan ServiceTimeRemaining { get; set; }

    /// <summary>
    /// Maximum service time when all limbs are freshly repaired.
    /// </summary>
    [DataField]
    public TimeSpan ServiceTimeMax { get; set; }

    /// <summary>
    /// Efficiency multiplier. 1.0 when service time remaining; 0.5 when depleted.
    /// </summary>
    [DataField]
    public float Efficiency { get; set; } = 1f;

    /// <summary>
    /// Service time contributed per cyber limb. Design doc: "10 minutes per matter bin".
    /// </summary>
    [DataField]
    public TimeSpan ServiceTimePerLimb { get; set; } = TimeSpan.FromMinutes(10);
}
