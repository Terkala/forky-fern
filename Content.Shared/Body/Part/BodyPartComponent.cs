using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Part;

/// <summary>
/// Component for body part entities. Body parts can be attached to bodies or other body parts.
/// Arms include hands, legs include feet - they are never detached separately.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBodyPartSystem))]
public sealed partial class BodyPartComponent : Component
{
    /// <summary>
    /// The body entity this body part is attached to, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    /// The type of this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartType PartType;

    /// <summary>
    /// The symmetry of this body part (None for head/torso, Left/Right for arms/legs).
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;

    /// <summary>
    /// The parent body part this is attached to, if any.
    /// Root parts (torso, head) have no parent.
    /// Arms and legs attach to torso.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Parent;

    /// <summary>
    /// The slot ID on the parent part where this part is attached, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SlotId;

    /// <summary>
    /// Container for organs within this body part.
    /// Head contains the brain, torso contains all other organs.
    /// </summary>
    [ViewVariables]
    public Container? Organs;

    /// <summary>
    /// Container ID for organs in this body part.
    /// </summary>
    public const string OrganContainerId = "body_part_organs";
}
