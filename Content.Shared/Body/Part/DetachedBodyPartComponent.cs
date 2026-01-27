using Robust.Shared.GameStates;

namespace Content.Shared.Body.Part;

/// <summary>
/// Component for detached body part entities that have been removed from a body.
/// These entities can be picked up and potentially reattached.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DetachedBodyPartComponent : Component
{
    /// <summary>
    /// Reference to the original body part entity, if it still exists.
    /// </summary>
    [DataField]
    public EntityUid? OriginalBodyPart;
}
