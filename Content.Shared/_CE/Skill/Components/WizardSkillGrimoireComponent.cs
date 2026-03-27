using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Components;

/// <summary>
/// Marks a spellbook that opens the CE skill tree for wizards (<see cref="Roles.Components.WizardRoleComponent"/>).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WizardSkillGrimoireComponent : Component
{
    /// <summary>
    /// First wizard allowed to open the skill tree UI; others are blocked once set.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? SkillTreeBoundOwner;
}
