using Content.Shared.Body;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Tags a body part (limb) with species and surgery config for surgical step resolution.
/// Added when the limb is inserted into body_organs via SurgeryLimbTaggingSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SurgerySystem), typeof(SurgeryLimbTaggingSystem))]
public sealed partial class SurgeryBodyPartComponent : Component
{
    /// <summary>
    /// Species of this limb (from body or limb prototype). Used to resolve BodyPartSurgeryStepsPrototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeciesPrototype> SpeciesId;

    /// <summary>
    /// Organ category of this body part (e.g. ArmLeft, LegRight). Used to resolve BodyPartSurgeryStepsPrototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<OrganCategoryPrototype> OrganCategory;
}
