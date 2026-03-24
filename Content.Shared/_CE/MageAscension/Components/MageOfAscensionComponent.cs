using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Marks a mob as a Mage of Ascension antag (school choice, confluence tier, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MageOfAscensionComponent : Component
{
    /// <summary>Skill tree id chosen at round start (e.g. Necromancy).</summary>
    [DataField, AutoNetworkedField]
    public string? SchoolId;

    /// <summary>How many confluences this mage has opened (drives spell tier).</summary>
    [DataField, AutoNetworkedField]
    public int ConfluencesOpened;

    /// <summary>
    /// Spell tiers the player still needs to pick one spell for (FIFO). Resolved against the mage school's tier lists.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<int> PendingSpellPickTiers = new();

    [DataField, AutoNetworkedField]
    public bool HasChosenSchool;
}
