namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Marker component added to body parts when ion damage is repaired during surgery.
/// Triggers ion damage removal in CyberLimbStatsSystem.
/// Server-only to avoid LastComponentRemoved triggering client crashes when removed.
/// </summary>
[RegisterComponent]
public sealed partial class IonDamageRepairedComponent : Component
{
}
