using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Mind role marker for a mage who became a dimensional rift horror.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MageRiftBeastRoleComponent : BaseMindRoleComponent;
