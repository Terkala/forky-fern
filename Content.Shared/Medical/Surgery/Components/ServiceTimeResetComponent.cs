// SPDX-FileCopyrightText: 2025
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Marker component added to body parts when wiring is replaced during maintenance.
/// Triggers service time reset in CyberLimbStatsSystem.
/// Server-only to avoid LastComponentRemoved triggering client crashes when removed.
/// </summary>
[RegisterComponent]
public sealed partial class ServiceTimeResetComponent : Component
{
}
