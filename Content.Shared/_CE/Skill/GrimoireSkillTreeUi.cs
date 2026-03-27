using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Skill;

/// <summary>
/// Opens the standard CE skill tree window while this bound UI session is active (e.g. grimoire item).
/// </summary>
[Serializable, NetSerializable]
public enum GrimoireSkillTreeUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GrimoireSkillTreeBuiState : BoundUserInterfaceState
{
    public GrimoireSkillTreeBuiState()
    {
    }
}
