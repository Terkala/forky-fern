namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component added to body parts when an unskilled technician performs surgery.
/// Indicates that the body part has a surgery penalty that needs to be fixed by a skilled technician.
/// Server-only to avoid LastComponentRemoved triggering client crashes when removed.
/// </summary>
[RegisterComponent]
public sealed partial class UnskilledSurgeryPenaltyComponent : Component
{
}
