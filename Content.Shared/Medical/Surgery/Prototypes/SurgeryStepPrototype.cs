using Content.Shared.Medical.Surgery;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

[Prototype]
public sealed partial class SurgeryStepPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SurgeryLayer Layer { get; private set; }

    [DataField]
    public LocId? Name { get; private set; }

    [DataField]
    public int Penalty { get; private set; }

    [DataField]
    public int ProcedureTypeIndex { get; private set; }

    [DataField]
    public string? RequiredToolTag { get; private set; }

    [DataField]
    public float DoAfterDelay { get; private set; } = 2f;
}
