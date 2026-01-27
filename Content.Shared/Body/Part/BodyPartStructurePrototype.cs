using Content.Shared.Body;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Body.Part;

/// <summary>
/// Prototype that defines the body part structure for a species.
/// Defines which body parts are created and how they're attached.
/// </summary>
[Prototype("bodyPartStructure")]
public sealed partial class BodyPartStructurePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// List of body part definitions that make up this structure.
    /// Body parts are initialized in dependency order (torso first, then parts that attach to torso, etc.).
    /// </summary>
    [DataField(required: true)]
    public List<BodyPartDefinition> Parts { get; private set; } = new();

    /// <summary>
    /// Rules for migrating organs from the body container to body part containers.
    /// </summary>
    [DataField]
    public List<OrganPlacementRule> OrganPlacementRules { get; private set; } = new();
}

/// <summary>
/// Defines a single body part in a body part structure.
/// </summary>
[DataDefinition]
public sealed partial class BodyPartDefinition
{
    /// <summary>
    /// The entity prototype ID for this body part.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype { get; private set; } = default!;

    /// <summary>
    /// The parent body part's prototype ID, or null if this is a root part (torso or head).
    /// Root parts attach directly to the body.
    /// </summary>
    [DataField]
    public EntProtoId? ParentPrototype { get; private set; }

    /// <summary>
    /// The slot ID on the parent part where this part attaches.
    /// Required if ParentPrototype is set.
    /// </summary>
    [DataField]
    public ProtoId<BodyPartSlotPrototype>? SlotId { get; private set; }
}

/// <summary>
/// Rule for placing organs from the body container into body part containers.
/// </summary>
[DataDefinition]
public sealed partial class OrganPlacementRule
{
    /// <summary>
    /// Organ category prototype ID to match. If null, matches all organs.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? OrganCategory { get; private set; }

    /// <summary>
    /// The body part type where organs matching this rule should be placed.
    /// </summary>
    [DataField(required: true)]
    public BodyPartType TargetPartType { get; private set; }
}
