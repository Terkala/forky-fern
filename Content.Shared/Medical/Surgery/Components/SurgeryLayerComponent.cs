using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Tracks surgery layer state on a body part (skin retracted, tissue retracted, bones sawed).
/// Used to determine which steps are available and which layers are "open".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SurgerySystem), typeof(SurgeryLayerSystem))]
public sealed partial class SurgeryLayerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool SkinRetracted;

    [DataField, AutoNetworkedField]
    public bool TissueRetracted;

    [DataField, AutoNetworkedField]
    public bool BonesSawed;
}
