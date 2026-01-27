using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Skill;

/// <summary>
/// Component that marks an entity as having skilled technician training.
/// Roboticists and RDs have this component.
/// Entities with this component can perform cyberlimb maintenance without penalties.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkilledTechnicianComponent : Component
{
}
