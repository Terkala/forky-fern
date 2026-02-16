using Content.Shared.Body;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Defines the ordered surgical steps for a body part of a given species.
/// One per (SpeciesId, OrganCategory) - e.g. HumanArmLeft, DionaLegRight.
/// </summary>
[Prototype]
public sealed partial class BodyPartSurgeryStepsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Species this config applies to (e.g. Human, Diona).
    /// </summary>
    [DataField("species", required: true)]
    public ProtoId<SpeciesPrototype> SpeciesId { get; private set; }

    /// <summary>
    /// Organ category (e.g. Torso, ArmLeft, LegRight). Uses OrganCategoryPrototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> OrganCategory { get; private set; }

    /// <summary>
    /// Ordered list of surgery step IDs for the Skin layer.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryStepPrototype>> SkinSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of surgery step IDs for the Tissue layer.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryStepPrototype>> TissueSteps { get; private set; } = new();

    /// <summary>
    /// Ordered list of surgery step IDs for the Organ layer (e.g. SawBones, RemoveOrgan, DetachLimb).
    /// Hands and feet omit DetachLimb; only ArmLeft, ArmRight, LegLeft, LegRight include it.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryStepPrototype>> OrganSteps { get; private set; } = new();
}
