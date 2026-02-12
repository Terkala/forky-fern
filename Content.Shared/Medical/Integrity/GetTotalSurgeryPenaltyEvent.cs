// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Raised on an entity to query the total surgery penalty from all body parts.
/// Handlers (e.g. server IntegritySystem on BodyComponent) set TotalPenalty.
/// </summary>
[ByRefEvent]
public record struct GetTotalSurgeryPenaltyEvent
{
    public FixedPoint2 TotalPenalty;
}
