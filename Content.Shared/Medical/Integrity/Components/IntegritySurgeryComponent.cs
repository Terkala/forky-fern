using Content.Shared.Medical.Integrity;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity.Components;

/// <summary>
/// Contextual integrity penalties on a body (dirty room, improper tools). Cleared when surgery is performed properly.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(IntegrityPenaltyAggregatorSystem))]
public sealed partial class IntegritySurgeryComponent : Component
{
    /// <summary>
    /// List of contextual penalties: (Reason, ProcedureTypeIndex, Amount).
    /// </summary>
    [DataField]
    public List<IntegrityPenaltyEntry> Entries { get; set; } = new();
}

/// <summary>
/// A single contextual integrity penalty entry.
/// </summary>
public readonly record struct IntegrityPenaltyEntry(string Reason, SurgeryProcedureType ProcedureTypeIndex, int Amount);
