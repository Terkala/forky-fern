using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Tracks maintenance state for a body with cyber limbs. One component per body.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticsMaintenanceComponent : Component
{
    [DataField]
    public bool PanelExposed { get; set; }

    [DataField]
    public bool PanelOpen { get; set; }

    [DataField]
    public bool PanelSecured { get; set; } = true;

    /// <summary>
    /// Number of wires inserted in the current repair session. Resets when panel is closed.
    /// </summary>
    [DataField]
    public int WiresInsertedCount { get; set; }
}
