using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Marks a dimensional rift as the single station-wide ascension portal (round-scoped spawn).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MageStationDimensionalRiftComponent : Component
{
    /// <summary>
    /// Set when the first successful pry completes; later do-after completions must no-op.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool MonsterReleased;

    /// <summary>
    /// If set, another mage cannot start a concurrent pry on this rift.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActivePryingUser;
}
