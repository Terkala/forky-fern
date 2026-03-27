using Robust.Shared.Serialization;

namespace Content.Shared._CE.Skill;

/// <summary>
/// Appearance data for mage grimoire world/inventory sprite (see GenericVisualizer on BaseMageGrimoire).
/// </summary>
[Serializable, NetSerializable]
public enum GrimoireBookVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum GrimoireBookVisualState : byte
{
    Closed,
    Opening,
    Open,
    Closing,
}
