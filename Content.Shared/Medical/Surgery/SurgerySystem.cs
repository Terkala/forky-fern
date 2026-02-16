using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

public sealed class SurgerySystem : EntitySystem
{
    private static readonly string[] DetachLimbCategories = ["ArmLeft", "ArmRight", "LegLeft", "LegRight"];

    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
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
        var isImprovised = false;
        if (!string.IsNullOrEmpty(step.RequiredToolTag))
        {
            var requiredTag = new ProtoId<TagPrototype>(step.RequiredToolTag);
            foreach (var held in _hands.EnumerateHeld((args.User, null)))
            {
                if (_tag.HasTag(held, requiredTag))
                {
                    tool = held;
                    break;
                }
            }
            if (!tool.HasValue && step.ImprovisedToolTags.Count > 0)
            {
                foreach (var improvisedTagStr in step.ImprovisedToolTags)
                {
                    var improvisedTag = new ProtoId<TagPrototype>(improvisedTagStr);
                    foreach (var held in _hands.EnumerateHeld((args.User, null)))
                    {
                        if (_tag.HasTag(held, improvisedTag))
                        {
                            tool = held;
                            isImprovised = true;
                            break;
                        }
                    }
                    if (tool.HasValue)
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

        var performedList = step.Layer switch
        {
            SurgeryLayer.Skin => layerComp.PerformedSkinSteps,
            SurgeryLayer.Tissue => layerComp.PerformedTissueSteps,
            SurgeryLayer.Organ => layerComp.PerformedOrganSteps,
            _ => null
        };
        if (performedList != null && performedList.Contains(args.StepId))
        {
            args.RejectReason = "already-done";
            return;
        }

        if (args.StepId == "RetractTissue")
        {
            if (!layerComp.SkinRetracted)
            {
                args.RejectReason = "layer-not-open";
                return;
            }
        }
        else if (args.StepId == "SawBones")
        {
            if (!layerComp.TissueRetracted)
            {
                args.RejectReason = "layer-not-open";
                return;
            }
        }
        else if (args.StepId == "CloseIncision")
        {
            if (!layerComp.PerformedSkinSteps.Contains("RetractSkin"))
            {
                args.RejectReason = "layer-not-open";
                return;
            }
        }
        else if (args.StepId == "CloseTissue")
        {
            if (!layerComp.PerformedTissueSteps.Contains("RetractTissue"))
            {
                args.RejectReason = "layer-not-open";
                return;
            }
        }
        else if (args.StepId == "DetachLimb")
        {
            if (!TryComp<OrganComponent>(args.BodyPart, out var limbOrgan) || limbOrgan.Category is not { } limbCategory)
            {
                args.RejectReason = "invalid-body-part";
                return;
            }
            if (!DetachLimbCategories.Contains(limbCategory.ToString()))
            {
                args.RejectReason = "cannot-detach-limb";
                return;
            }
            if (!layerComp.SkinRetracted || !layerComp.TissueRetracted || !layerComp.BonesSawed)
            {
                args.RejectReason = "layer-not-open";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var limbBodyPart) || limbBodyPart.Body != ent.Owner)
            {
                args.RejectReason = "body-part-detached";
                return;
            }
        }
        else if (args.StepId == "AttachLimb")
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<OrganComponent>(args.Organ.Value, out var limbOrgan) || limbOrgan.Body.HasValue)
            {
                args.RejectReason = "organ-already-in-body";
                return;
            }
            if (limbOrgan.Category is not { } limbCategory || !DetachLimbCategories.Contains(limbCategory.ToString()))
            {
                args.RejectReason = "invalid-limb-type";
                return;
            }
            if (!_hands.IsHolding(args.User, args.Organ.Value))
            {
                args.RejectReason = "limb-not-in-hand";
                return;
            }
            if (!layerComp.SkinRetracted || !layerComp.TissueRetracted || !layerComp.BonesSawed)
            {
                args.RejectReason = "layer-not-open";
                return;
            }
        }

        if (args.StepId == "RemoveOrgan")
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var bodyPartComp) || bodyPartComp.Organs == null ||
                !bodyPartComp.Organs.ContainedEntities.Contains(args.Organ.Value))
            {
                args.RejectReason = "organ-not-in-body-part";
                return;
            }
            if (!layerComp.SkinRetracted || !layerComp.TissueRetracted || !layerComp.BonesSawed)
            {
                args.RejectReason = "layer-not-open";
                return;
            }
            if (bodyPartComp.Body != ent.Owner)
            {
                args.RejectReason = "body-part-detached";
                return;
            }
        }
        else if (args.StepId == "InsertOrgan")
        {
            if (!args.Organ.HasValue || !Exists(args.Organ.Value))
            {
                args.RejectReason = "invalid-entity";
                return;
            }
            if (!TryComp<OrganComponent>(args.Organ.Value, out var organComp) || organComp.Body.HasValue)
            {
                args.RejectReason = "organ-already-in-body";
                return;
            }
            if (!_hands.IsHolding(args.User, args.Organ.Value))
            {
                args.RejectReason = "organ-not-in-hand";
                return;
            }
            if (!TryComp<BodyPartComponent>(args.BodyPart, out var insertBodyPart) || insertBodyPart.Organs == null)
            {
                args.RejectReason = "body-part-no-container";
                return;
            }
            if (!layerComp.SkinRetracted || !layerComp.TissueRetracted || !layerComp.BonesSawed)
            {
                args.RejectReason = "layer-not-open";
                return;
            }
            if (insertBodyPart.Body != ent.Owner)
            {
                args.RejectReason = "body-part-detached";
                return;
            }
            if (insertBodyPart.Slots.Count > 0)
            {
                if (organComp.Category is not { } category || !insertBodyPart.Slots.Contains(category))
                {
                    args.RejectReason = "no-slot-for-organ";
                    return;
                }
                if (insertBodyPart.Organs != null && insertBodyPart.Organs.ContainedEntities.Any(o =>
                        TryComp<OrganComponent>(o, out var oComp) && oComp.Category == category))
                {
                    args.RejectReason = "slot-filled";
                    return;
                }
            }

            var costEv = new IntegrityCostRequestEvent(args.Organ.Value);
            RaiseLocalEvent(args.Organ.Value, ref costEv);
            if (costEv.Cost > 0)
            {
                var penaltyEv = new IntegrityPenaltyTotalRequestEvent(ent.Owner);
                RaiseLocalEvent(ent.Owner, ref penaltyEv);
                var usage = TryComp<IntegrityUsageComponent>(ent.Owner, out var usageComp) ? usageComp.Usage : 0;
                const int maxIntegrity = 6;
                if (usage + penaltyEv.Total + costEv.Cost > maxIntegrity)
                {
                    args.RejectReason = "integrity-over-capacity";
                    return;
                }
            }
        }

        // InsertOrgan/RemoveOrgan/AttachLimb: organ/limb is in hand - use it as DoAfter "used" for tracking
        if (args.StepId is "InsertOrgan" or "RemoveOrgan" or "AttachLimb" && args.Organ.HasValue)
            tool = args.Organ.Value;

        var doAfterEv = new SurgeryDoAfterEvent(GetNetEntity(args.BodyPart), args.StepId, args.Organ.HasValue ? GetNetEntity(args.Organ.Value) : null);
        var isOrganStep = args.StepId is "InsertOrgan" or "RemoveOrgan" or "AttachLimb";
        var delay = step.DoAfterDelay * (isImprovised ? step.ImprovisedDelayMultiplier : 1f);
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, delay, doAfterEv, args.Target, args.Target, tool)
        {
            NeedHand = true,
            BreakOnHandChange = !isOrganStep,
            BreakOnMove = !isOrganStep,
            BreakOnDropItem = !isOrganStep,
            DistanceThreshold = isOrganStep ? 3f : 1.5f,
            RequireCanInteract = !isOrganStep,
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

        if (args.StepId is "RemoveOrgan" or "InsertOrgan" or "DetachLimb" or "AttachLimb")
        {
            if (!TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp) || bodyPartComp.Body != ent.Owner)
            {
                _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-invalid-surgical-process"), args.User, args.User, PopupType.Medium);
                return;
            }
            if (!layerComp.SkinRetracted || !layerComp.TissueRetracted || !layerComp.BonesSawed)
            {
                _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-invalid-surgical-process"), args.User, args.User, PopupType.Medium);
                return;
            }

            if (args.StepId == "RemoveOrgan")
            {
                if (args.Organ is not { } organNet)
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-gone"), args.User, args.User, PopupType.Medium);
                    return;
                }
                var organ = GetEntity(organNet);
                if (!Exists(organ) || bodyPartComp.Organs == null || !bodyPartComp.Organs.ContainedEntities.Contains(organ))
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-gone"), args.User, args.User, PopupType.Medium);
                    return;
                }
                var removeEv = new OrganRemoveRequestEvent(organ) { Destination = Transform(args.User).Coordinates };
                RaiseLocalEvent(organ, ref removeEv);
                if (removeEv.Success)
                    _hands.TryPickupAnyHand(args.User, organ, checkActionBlocker: false);
            }
            else if (args.StepId == "InsertOrgan")
            {
                if (args.Organ is not { } organNet)
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), args.User, args.User, PopupType.Medium);
                    return;
                }
                var organ = GetEntity(organNet);
                if (!Exists(organ))
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), args.User, args.User, PopupType.Medium);
                    return;
                }
                if (!_hands.IsHolding(args.User, organ))
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), args.User, args.User, PopupType.Medium);
                    return;
                }
                if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category &&
                    bodyPartComp.Slots.Count > 0 && bodyPartComp.Organs != null &&
                    bodyPartComp.Organs.ContainedEntities.Any(o =>
                        TryComp<OrganComponent>(o, out var oComp) && oComp.Category == category))
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-slot-filled"), args.User, args.User, PopupType.Medium);
                    return;
                }
                var insertEv = new OrganInsertRequestEvent(bodyPart, organ);
                RaiseLocalEvent(bodyPart, ref insertEv);
                if (!insertEv.Success)
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-slot-filled"), args.User, args.User, PopupType.Medium);
                    return;
                }
            }
            else if (args.StepId == "DetachLimb")
            {
                var removeEv = new OrganRemoveRequestEvent(bodyPart) { Destination = Transform(ent.Owner).Coordinates };
                RaiseLocalEvent(bodyPart, ref removeEv);
            }
            else if (args.StepId == "AttachLimb")
            {
                if (args.Organ is not { } limbNet)
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), args.User, args.User, PopupType.Medium);
                    return;
                }
                var limb = GetEntity(limbNet);
                if (!Exists(limb) || !_hands.IsHolding(args.User, limb))
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-organ-not-in-hand"), args.User, args.User, PopupType.Medium);
                    return;
                }
                if (TryComp<OrganComponent>(limb, out var limbOrganComp) && limbOrganComp.Category is { } attachCategory)
                {
                    var alreadyHasLimb = _body.GetAllOrgans(ent.Owner).Any(o =>
                        TryComp<OrganComponent>(o, out var oComp) && oComp.Category == attachCategory);
                    if (alreadyHasLimb)
                    {
                        _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-slot-filled"), args.User, args.User, PopupType.Medium);
                        return;
                    }
                }
                if (ent.Comp.Organs != null && _container.Insert(limb, ent.Comp.Organs))
                {
                    _hands.TryDrop(args.User, limb);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("health-analyzer-surgery-error-slot-filled"), args.User, args.User, PopupType.Medium);
                }
            }
        }
        else
        {
            var list = step.Layer switch
            {
                SurgeryLayer.Skin => layerComp.PerformedSkinSteps,
                SurgeryLayer.Tissue => layerComp.PerformedTissueSteps,
                SurgeryLayer.Organ => layerComp.PerformedOrganSteps,
                _ => null
            };
            if (list != null && !list.Contains(args.StepId))
                list.Add(args.StepId);
            Dirty(bodyPart, layerComp);

            var penaltyEv = new SurgeryPenaltyAppliedEvent(bodyPart, step.Penalty);
            RaiseLocalEvent(bodyPart, ref penaltyEv);
        }

        if (step.Damage is { } damage && !damage.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, damage, ignoreResistances: false, origin: args.User);
        }

        if (step.HealAmount is { } healAmount && !healAmount.Empty)
        {
            _damageable.TryChangeDamage(ent.Owner, healAmount, ignoreResistances: true, origin: args.User);
        }

        if (step.Sound != null && _net.IsServer)
        {
            _audio.PlayPvs(step.Sound, ent.Owner);
        }
    }
}
