using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component added to body parts when an unskilled technician performs surgery.
/// Indicates that the body part has a surgery penalty that needs to be fixed by a skilled technician.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnskilledSurgeryPenaltyComponent : Component
{
}
