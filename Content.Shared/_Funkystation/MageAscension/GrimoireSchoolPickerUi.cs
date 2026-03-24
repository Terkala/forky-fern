using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.MageAscension;

[Serializable, NetSerializable]
public enum GrimoireSchoolPickerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GrimoireSchoolPickerSelectMessage : BoundUserInterfaceMessage
{
    public GrimoireSchoolPickerSelectMessage(string schoolId)
    {
        SchoolId = schoolId;
    }

    public string SchoolId { get; }
}

[Serializable, NetSerializable]
public sealed class GrimoireSchoolPickerBuiState : BoundUserInterfaceState
{
    public GrimoireSchoolPickerBuiState(
        bool committed,
        string? chosenSchoolId,
        int pendingSpellPickTier = 0,
        string[]? spellPickOptionIds = null)
    {
        Committed = committed;
        ChosenSchoolId = chosenSchoolId;
        PendingSpellPickTier = pendingSpellPickTier;
        SpellPickOptionIds = spellPickOptionIds ?? Array.Empty<string>();
    }

    public bool Committed { get; }
    public string? ChosenSchoolId { get; }
    public int PendingSpellPickTier { get; }
    public string[] SpellPickOptionIds { get; }
}

[Serializable, NetSerializable]
public sealed class GrimoireSpellPickSelectMessage : BoundUserInterfaceMessage
{
    public GrimoireSpellPickSelectMessage(string skillId)
    {
        SkillId = skillId;
    }

    public string SkillId { get; }
}
