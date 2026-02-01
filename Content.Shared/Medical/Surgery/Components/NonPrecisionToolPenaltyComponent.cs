// SPDX-FileCopyrightText: 2025
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component that tracks permanent integrity penalty from using non-precision tools during cyber-limb maintenance.
/// This penalty is applied when opening maintenance panels with standard screwdrivers instead of high-precision tools.
/// Unlike SurgeryPenaltyComponent, this penalty is permanent and cannot be removed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NonPrecisionToolPenaltyComponent : Component
{
    /// <summary>
    /// The permanent integrity penalty amount.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 PermanentPenalty = FixedPoint2.New(2);
}
