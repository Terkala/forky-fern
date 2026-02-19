using Content.Shared.Medical.Surgery;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Type of prerequisite that must be satisfied for a surgery step to be available.
/// </summary>
public enum StepPrerequisiteType
{
    /// <summary>
    /// Layer must be open (all open steps done, no close steps done).
    /// </summary>
    RequireLayerOpen,

    /// <summary>
    /// Layer must be closed (never opened OR close step done - allows cyclical re-opening).
    /// </summary>
    RequireLayerClosed,

    /// <summary>
    /// Step ID must be in the performed list for that layer.
    /// </summary>
    RequireStepPerformed
}

/// <summary>
/// Declarative prerequisite for a surgery step. All prerequisites must pass for the step to be available.
/// </summary>
[DataDefinition]
public sealed partial class StepPrerequisite
{
    [DataField("type", required: true)]
    public StepPrerequisiteType Type { get; private set; }

    /// <summary>
    /// For RequireLayerOpen/RequireLayerClosed: which layer.
    /// </summary>
    [DataField("layer")]
    public SurgeryLayer? Layer { get; private set; }

    /// <summary>
    /// For RequireStepPerformed: which step ID must be performed.
    /// </summary>
    [DataField("step")]
    public string? StepId { get; private set; }
}
