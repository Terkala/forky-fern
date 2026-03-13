using Content.Shared.Power.Generation.Supermatter.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Power.Generation.Supermatter.Components;

/// <summary>
/// Networked state for the Supermatter crystal. Minimal data for client visuals.
/// Server updates this from SupermatterProcessingComponent; client reads for cracks, color, particles, audio.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSupermatterSystem))]
public sealed partial class SupermatterStateComponent : Component
{
    /// <summary>
    /// Current power level in GeV. Drives lightning tier, grav radius, radiation, etc.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float Power;

    /// <summary>
    /// Integrity/health (0-1000). At 0, delamination occurs.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float Integrity = 1000f;

    /// <summary>
    /// Stability. 10 = inactive; lower = more active and dangerous.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float Stability = 10f;

    /// <summary>
    /// Growth characteristic. Negative = produce gas; positive = absorb gas.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float Growth;

    /// <summary>
    /// Conductivity. Positive = lightning out; negative = absorb power from devices.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float Conductivity;

    /// <summary>
    /// Enthalpy. Positive = heat out; negative = absorb heat.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float Enthalpy;
}
