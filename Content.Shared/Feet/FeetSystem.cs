// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Feet.Components;
using Content.Shared.Slippery;
using Content.Shared.Standing;

namespace Content.Shared.Feet;

/// <summary>
/// Feet map 1:1 to legs. Left/Right/Middle (None) symmetry supported, mirroring HandLocation.
/// Future body plans (e.g., 4+ legs) may require extending FootLocation or using dynamic foot IDs.
/// </summary>
public sealed class FeetSystem : EntitySystem
{
    private const string LeftFootId = "left_foot";
    private const string RightFootId = "right_foot";
    private const string MiddleFootId = "middle_foot";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FeetComponent, SlipAttemptEvent>(OnSlipAttempt);
        SubscribeLocalEvent<FeetComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<FeetComponent, BodyPartDetachingEvent>(OnBodyPartDetaching);
        SubscribeLocalEvent<FeetComponent, BodyPartAttachingEvent>(OnBodyPartAttaching);
        SubscribeLocalEvent<FeetComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FeetComponent> ent, ref MapInitEvent args)
    {
        ReconcileFeetFromBody(ent);
    }

    private void ReconcileFeetFromBody(Entity<FeetComponent> ent)
    {
        if (!HasComp<BodyComponent>(ent))
            return;

        ent.Comp.Feet.Clear();
        ent.Comp.SortedFeet.Clear();

        var partsEv = new GetBodyPartsEvent();
        RaiseLocalEvent(ent, ref partsEv);

        foreach (var (partId, bodyPart) in partsEv.Parts)
        {
            if (bodyPart.PartType != BodyPartType.Leg)
                continue;

            var (footId, location) = bodyPart.Symmetry switch
            {
                BodyPartSymmetry.Left => (LeftFootId, FootLocation.Left),
                BodyPartSymmetry.Right => (RightFootId, FootLocation.Right),
                BodyPartSymmetry.None => (MiddleFootId, FootLocation.Middle),
                _ => (null, FootLocation.Left)
            };

            if (footId == null || ent.Comp.Feet.ContainsKey(footId))
                continue;

            ent.Comp.Feet[footId] = new Foot { Location = location };
            ent.Comp.SortedFeet.Add(footId);
        }

        Dirty(ent);
    }

    private void OnBodyPartDetaching(Entity<FeetComponent> ent, ref BodyPartDetachingEvent args)
    {
        if (args.BodyPart.Comp.PartType != BodyPartType.Leg)
            return;

        var footId = args.BodyPart.Comp.Symmetry switch
        {
            BodyPartSymmetry.Left => LeftFootId,
            BodyPartSymmetry.Right => RightFootId,
            BodyPartSymmetry.None => MiddleFootId,
            _ => null
        };

        if (footId == null || !ent.Comp.Feet.Remove(footId))
            return;

        ent.Comp.SortedFeet.Remove(footId);
        Dirty(ent);
    }

    private void OnBodyPartAttaching(Entity<FeetComponent> ent, ref BodyPartAttachingEvent args)
    {
        if (args.BodyPart.Comp.PartType != BodyPartType.Leg)
            return;

        var (footId, location) = args.BodyPart.Comp.Symmetry switch
        {
            BodyPartSymmetry.Left => (LeftFootId, FootLocation.Left),
            BodyPartSymmetry.Right => (RightFootId, FootLocation.Right),
            BodyPartSymmetry.None => (MiddleFootId, FootLocation.Middle),
            _ => (null, FootLocation.Left)
        };

        if (footId == null || ent.Comp.Feet.ContainsKey(footId))
            return;

        ent.Comp.Feet[footId] = new Foot { Location = location };
        ent.Comp.SortedFeet.Add(footId);
        Dirty(ent);
    }

    private void OnSlipAttempt(Entity<FeetComponent> ent, ref SlipAttemptEvent args)
    {
        if (ent.Comp.Count == 0)
            args.NoSlip = true;
    }

    private void OnStandAttempt(Entity<FeetComponent> ent, ref StandAttemptEvent args)
    {
        if (ent.Comp.Count == 0)
            args.Cancel();
    }
}
