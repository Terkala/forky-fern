// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Shared system that handles GetTotalSurgeryPenaltyEvent using GetBodyPartsEvent.
/// Runs on both client and server so integrity calculations (including prediction) use correct surgery penalty.
/// If integrity is server-only, this is harmless.
/// </summary>
public sealed class SurgeryPenaltyQuerySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetTotalSurgeryPenaltyEvent>(OnGetTotalSurgeryPenalty);
    }

    private void OnGetTotalSurgeryPenalty(Entity<BodyComponent> ent, ref GetTotalSurgeryPenaltyEvent args)
    {
        FixedPoint2 totalPenalty = FixedPoint2.Zero;

        var partsEv = new GetBodyPartsEvent();
        RaiseLocalEvent(ent, ref partsEv);

        foreach (var (partId, _) in partsEv.Parts)
        {
            if (TryComp<SurgeryPenaltyComponent>(partId, out var penalty))
            {
                totalPenalty += penalty.CurrentPenalty;
            }

            if (TryComp<NonPrecisionToolPenaltyComponent>(partId, out var nonPrecisionPenalty))
            {
                totalPenalty += nonPrecisionPenalty.PermanentPenalty;
            }

            if (TryComp<CyberLimbComponent>(partId, out var cyberLimb))
            {
                if (cyberLimb.PanelOpen)
                {
                    totalPenalty += FixedPoint2.New(2);
                }
                else if (cyberLimb.PanelExposed)
                {
                    totalPenalty += FixedPoint2.New(1);
                }
            }

            if (TryComp<IonDamagedComponent>(partId, out var ionDamage))
            {
                totalPenalty += ionDamage.BioRejectionPenalty;
            }
        }

        args.TotalPenalty = totalPenalty;
    }
}
