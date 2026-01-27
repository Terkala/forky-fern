using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Part;

/// <summary>
/// Prototype that defines a body part slot (e.g., "head", "left_arm", "right_leg").
/// Slots define where body parts can be attached on parent body parts.
/// </summary>
[Prototype("bodyPartSlot")]
public sealed partial class BodyPartSlotPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The type of body part that can be attached to this slot.
    /// </summary>
    [DataField(required: true)]
    public BodyPartType PartType { get; private set; }

    /// <summary>
    /// The symmetry required for this slot (None, Left, or Right).
    /// </summary>
    [DataField]
    public BodyPartSymmetry Symmetry { get; private set; } = BodyPartSymmetry.None;
}
