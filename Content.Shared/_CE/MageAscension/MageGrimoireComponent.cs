using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension;

/// <summary>
/// Player grimoire item; must be "open" to interact with ley confluences.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MageGrimoireComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsOpen;
}
