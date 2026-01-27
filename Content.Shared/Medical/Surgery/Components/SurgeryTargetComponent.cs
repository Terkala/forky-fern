using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component that marks an entity as a valid target for surgery.
/// Added to body entities (players, NPCs) that can have surgery performed on them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryTargetComponent : Component
{
}
