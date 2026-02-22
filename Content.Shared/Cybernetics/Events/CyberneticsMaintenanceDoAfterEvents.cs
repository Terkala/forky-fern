using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Cybernetics.Events;

[Serializable, NetSerializable]
public sealed partial class CyberneticsScrewdriverDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CyberneticsWrenchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CyberneticsWireInsertDoAfterEvent : DoAfterEvent
{
    [DataField]
    public bool IsPrecisionScrewing { get; private set; }

    [DataField]
    public NetEntity? ScrewdriverEntity { get; private set; }

    private CyberneticsWireInsertDoAfterEvent()
    {
    }

    public CyberneticsWireInsertDoAfterEvent(bool isPrecisionScrewing, NetEntity? screwdriverEntity = null)
    {
        IsPrecisionScrewing = isPrecisionScrewing;
        ScrewdriverEntity = screwdriverEntity;
    }

    public override DoAfterEvent Clone() => new CyberneticsWireInsertDoAfterEvent(IsPrecisionScrewing, ScrewdriverEntity);
}
