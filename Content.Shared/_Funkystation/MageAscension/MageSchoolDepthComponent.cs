using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.MageAscension;

/// <summary>
/// Per-<see cref="MageSchoolPrototype"/> depth (count of learned tier 2–5 picks); recomputed from skills.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMageSchoolDepthSystem))]
public sealed partial class MageSchoolDepthComponent : Component
{
    /// <summary>Keys are <see cref="MageSchoolPrototype"/> ids (e.g. Elementalism, Honkamancy).</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> DepthBySchool = new();
}
