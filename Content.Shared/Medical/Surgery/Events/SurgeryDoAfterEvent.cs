using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery.Events;

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetEntity BodyPart { get; private set; }

    [DataField(required: true)]
    public string StepId { get; private set; } = default!;

    private SurgeryDoAfterEvent()
    {
    }

    public SurgeryDoAfterEvent(NetEntity bodyPart, string stepId)
    {
        BodyPart = bodyPart;
        StepId = stepId;
    }

    public override DoAfterEvent Clone() => new SurgeryDoAfterEvent(BodyPart, StepId);
}
