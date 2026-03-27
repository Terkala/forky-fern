using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Player grimoire item. Ley confluences require this grimoire to be used from the hand (open/closed does not matter).
/// IsOpen is true while the skill tree bound UI is open (book is being read).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MageGrimoireComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsOpen;

    /// <summary>
    /// First mage allowed to open the skill tree UI; others are blocked once set.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? SkillTreeBoundOwner;
}
