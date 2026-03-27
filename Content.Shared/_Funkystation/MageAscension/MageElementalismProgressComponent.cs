using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Synced count of learned Elementalism progression spells (tiers 2–5); recomputed from skills.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMageElementalismDepthSystem))]
public sealed partial class MageElementalismProgressComponent : Component
{
    /// <summary>
    /// Number of skills learned from Elementalism school tiers 2–5.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ElementalismDepth;
}
