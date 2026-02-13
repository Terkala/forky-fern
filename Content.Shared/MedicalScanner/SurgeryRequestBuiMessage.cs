using Content.Shared.Medical.Surgery;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

[Serializable, NetSerializable]
public sealed class SurgeryRequestBuiMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;
    public NetEntity BodyPart;
    public string StepId;
    public SurgeryLayer Layer;
    public bool IsImprovised;

    public SurgeryRequestBuiMessage(NetEntity target, NetEntity bodyPart, string stepId, SurgeryLayer layer, bool isImprovised)
    {
        Target = target;
        BodyPart = bodyPart;
        StepId = stepId;
        Layer = layer;
        IsImprovised = isImprovised;
    }
}
