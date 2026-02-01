using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Marker component added to body parts when ion damage is repaired during surgery.
/// Triggers ion damage removal in CyberLimbStatsSystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IonDamageRepairedComponent : Component
{
}
