// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Feet;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Feet.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(FeetSystem))]
public sealed partial class FeetComponent : Component
{
    /// <summary>
    /// Dictionary relating a unique foot ID to information about the foot itself.
    /// </summary>
    [DataField]
    public Dictionary<string, Foot> Feet = new();

    /// <summary>
    /// The number of feet.
    /// </summary>
    [ViewVariables]
    public int Count => Feet.Count;

    /// <summary>
    /// List of foot IDs. The order determines iteration order.
    /// </summary>
    [DataField]
    public List<string> SortedFeet = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public partial record struct Foot
{
    [DataField]
    public FootLocation Location = FootLocation.Left;
}

/// <summary>
/// What side of the body this foot is on.
/// Mirrors HandLocation (Left, Middle, Right).
/// </summary>
[Serializable, NetSerializable]
public enum FootLocation : byte
{
    Left,
    Right,
    Middle
}
