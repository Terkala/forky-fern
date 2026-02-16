using System.Linq;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Tracks surgery layer state on a body part. Stores which steps have been performed per layer.
/// Used to determine which steps are available and which layers are "open".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SurgerySystem), typeof(SurgeryLayerSystem))]
public sealed partial class SurgeryLayerComponent : Component
{
    /// <summary>
    /// Step IDs that have been performed for the Skin layer (e.g. RetractSkin).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedSkinSteps { get; set; } = new();

    /// <summary>
    /// Step IDs that have been performed for the Tissue layer (e.g. RetractTissue, SawBones).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedTissueSteps { get; set; } = new();

    /// <summary>
    /// Step IDs that have been performed for the Organ layer (e.g. RemoveOrgan, DetachLimb).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> PerformedOrganSteps { get; set; } = new();

    /// <summary>
    /// True if RetractSkin has been performed and not yet closed (skin layer open for tissue).
    /// </summary>
    public bool SkinRetracted => PerformedSkinSteps.Contains("RetractSkin") && !PerformedSkinSteps.Contains("CloseIncision");

    /// <summary>
    /// True if RetractTissue has been performed and not yet closed (tissue layer open for organ).
    /// </summary>
    public bool TissueRetracted => PerformedTissueSteps.Contains("RetractTissue") && !PerformedTissueSteps.Contains("CloseTissue");

    /// <summary>
    /// True if SawBones has been performed and tissue not closed (organ layer fully open).
    /// </summary>
    public bool BonesSawed => PerformedTissueSteps.Contains("SawBones") && !PerformedTissueSteps.Contains("CloseTissue");
}
