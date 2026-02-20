using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Cybernetics.Events;

[Serializable, NetSerializable]
public sealed partial class CyberneticsScrewdriverDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CyberneticsWrenchDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CyberneticsWireInsertDoAfterEvent : SimpleDoAfterEvent;
