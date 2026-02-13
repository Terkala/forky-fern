using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Components;

/// <summary>
/// Marks an entity as a body part (torso, head, limb) that can contain child organs.
/// Body parts are inserted into the body's body_organs container.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(BodySystem), typeof(BodyPartOrganSystem))]
public sealed partial class BodyPartComponent : Component
{
    /// <summary>
    /// ID of the container that holds child organs (e.g. internal organs for torso, hands for arms).
    /// </summary>
    [DataField]
    public string ContainerId = "body_part_organs";

    /// <summary>
    /// The root body entity this part belongs to. Set when the part is inserted into a body.
    /// </summary>
    [ViewVariables]
    public EntityUid? Body;

    /// <summary>
    /// The container for child organs. Created on init.
    /// </summary>
    [ViewVariables]
    public Container? Organs;
}
