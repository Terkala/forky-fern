using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component that tracks progress on bidirectional surgery steps.
/// Used for steps that can be performed in both directions (e.g., open/close).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepProgressComponent : Component
{
    /// <summary>
    /// Set of completed step IDs.
    /// </summary>
    [DataField]
    public HashSet<string> CompletedSteps = new();

    /// <summary>
    /// Dictionary mapping sequence IDs to their progress values.
    /// Progress indicates how many steps in the sequence have been completed.
    /// </summary>
    [DataField]
    public Dictionary<string, int> SequenceProgress = new();

    /// <summary>
    /// Dictionary mapping sequence IDs to their step IDs.
    /// Used to track which steps belong to which sequences.
    /// </summary>
    [DataField]
    public Dictionary<string, List<string>> SequenceSteps = new();
}
