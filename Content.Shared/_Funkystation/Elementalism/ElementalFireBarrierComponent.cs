using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.Elementalism;

/// <summary>
/// Active fire aura: mana drain, heat immunity, self fire-stack suppression, contact ignition (server).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ElementalFireBarrierComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;
}
