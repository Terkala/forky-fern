// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Body.Organ;

/// <summary>
/// Marker component to identify the slime core organ.
/// The slime core is an all-in-one organ that combines brain, stomach, and metabolizer functionality.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeCoreOrganComponent : Component
{
}
