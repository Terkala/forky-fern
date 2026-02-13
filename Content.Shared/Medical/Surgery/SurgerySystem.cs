using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

public sealed class SurgerySystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, SurgeryRequestEvent>(OnSurgeryRequest);
        SubscribeLocalEvent<BodyComponent, SurgeryDoAfterEvent>(OnSurgeryDoAfter);
    }

    private void OnSurgeryRequest(Entity<BodyComponent> ent, ref SurgeryRequestEvent args)
    {
        if (args.Target != ent.Owner)
            return;

        args.Valid = false;

        if (!Exists(args.User) || !Exists(args.Target) || !Exists(args.BodyPart))
        {
            args.RejectReason = "invalid-entity";
            return;
        }

        var query = new BodyPartQueryEvent(ent.Owner);
        RaiseLocalEvent(ent.Owner, ref query);
        if (!query.Parts.Contains(args.BodyPart))
        {
            args.RejectReason = "body-part-not-in-body";
            return;
        }

        if (!_prototypes.TryIndex<SurgeryStepPrototype>(args.StepId, out var step))
        {
            args.RejectReason = "unknown-step";
            return;
        }

        if (step.Layer != args.Layer)
        {
            args.RejectReason = "layer-mismatch";
            return;
        }

        EntityUid? tool = null;
        if (!string.IsNullOrEmpty(step.RequiredToolTag))
        {
            var tag = new ProtoId<TagPrototype>(step.RequiredToolTag);
            foreach (var held in _hands.EnumerateHeld((args.User, null)))
            {
                if (_tag.HasTag(held, tag))
                {
                    tool = held;
                    break;
                }
            }
            if (!tool.HasValue)
            {
                args.RejectReason = "missing-tool";
                return;
            }
        }

        var layerComp = EnsureComp<SurgeryLayerComponent>(args.BodyPart);

        if (args.StepId == "RetractSkin" && layerComp.SkinRetracted)
        {
            args.RejectReason = "already-done";
            return;
        }
        if (args.StepId == "RetractTissue" && layerComp.TissueRetracted)
        {
            args.RejectReason = "already-done";
            return;
        }
        if (args.StepId == "SawBones" && layerComp.BonesSawed)
        {
            args.RejectReason = "already-done";
            return;
        }

        var doAfterEv = new SurgeryDoAfterEvent(GetNetEntity(args.BodyPart), args.StepId);
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, step.DoAfterDelay, doAfterEv, args.Target, args.Target, tool)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            args.RejectReason = "doafter-failed";
            return;
        }

        args.Valid = true;
    }

    private void OnSurgeryDoAfter(Entity<BodyComponent> ent, ref SurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        var bodyPart = GetEntity(args.BodyPart);
        if (!Exists(bodyPart) || !_prototypes.TryIndex<SurgeryStepPrototype>(args.StepId, out var step))
            return;

        var layerComp = EnsureComp<SurgeryLayerComponent>(bodyPart);

        switch (args.StepId)
        {
            case "RetractSkin":
                layerComp.SkinRetracted = true;
                break;
            case "RetractTissue":
                layerComp.TissueRetracted = true;
                break;
            case "SawBones":
                layerComp.BonesSawed = true;
                break;
        }
        Dirty(bodyPart, layerComp);

        var penaltyEv = new SurgeryPenaltyAppliedEvent(bodyPart, step.Penalty);
        RaiseLocalEvent(bodyPart, ref penaltyEv);
    }
}
