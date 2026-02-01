// SPDX-FileCopyrightText: 2025
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Marker component added to body parts when wiring is replaced during maintenance.
/// Triggers service time reset in CyberLimbStatsSystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ServiceTimeResetComponent : Component
{
}
