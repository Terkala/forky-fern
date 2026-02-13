using Content.Shared.Medical.Surgery;

namespace Content.Shared.Medical.Surgery.Events;

/// <summary>
/// Raised when a surgery step is requested (e.g. from Health Analyzer BUI).
/// SurgerySystem validates and optionally starts DoAfter.
/// </summary>
[ByRefEvent]
public record struct SurgeryRequestEvent(
    EntityUid Analyzer,
    EntityUid User,
    EntityUid Target,
    EntityUid BodyPart,
    string StepId,
    SurgeryLayer Layer,
    bool IsImprovised,
    EntityUid? Organ = null)
{
    public bool Valid;
    public string? RejectReason;
}
