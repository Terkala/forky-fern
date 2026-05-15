using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.MageAscension.Components;

[Serializable, NetSerializable]
public enum MageRiftElementKind : byte
{
    Fire,
    Air,
    Earth,
    Water,
}

/// <summary>
/// Elemental rift horror: rotating element (fire → air → earth → water) with synced tint.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class MageRiftElementalVariantComponent : Component
{
    [DataField, AutoNetworkedField]
    public MageRiftElementKind Current = MageRiftElementKind.Fire;

    /// <summary>
    /// How long between automatic element rotations.
    /// </summary>
    [DataField]
    public TimeSpan RotationInterval = TimeSpan.FromSeconds(12);
}
