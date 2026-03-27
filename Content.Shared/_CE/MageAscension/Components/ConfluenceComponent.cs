using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ConfluenceComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Opened;

    /// <summary>If true, only mages see this entity (until opened).</summary>
    [DataField, AutoNetworkedField]
    public bool MageOnlyVisibility = true;
}
