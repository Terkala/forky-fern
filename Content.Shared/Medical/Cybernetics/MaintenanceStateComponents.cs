// SPDX-FileCopyrightText: 2025
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Marker component for exposed maintenance screws state.
/// Added during ExposeMaintenanceScrewsStep surgery step.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MaintenanceScrewsExposedComponent : Component
{
}

/// <summary>
/// Marker component for open maintenance panel state.
/// Triggers panel opening via CyberLimbMaintenanceSystem.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MaintenancePanelOpenComponent : Component
{
}
