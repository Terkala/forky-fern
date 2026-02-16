// SPDX-FileCopyrightText: 2022-2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022-2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022-2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Fishfish458 <47410468+Fishfish458@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023-2024 Whisper <121047731+QuietlyWhisper@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 Emisse <99158783+Emisse@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2023 nmajask <nmajask@gmail.com>
// SPDX-FileCopyrightText: 2024 Saphire Lattice <lattice@saphi.re>
// SPDX-FileCopyrightText: 2024 ArchRBX <5040911+ArchRBX@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Cojoke <83733158+Cojoke-dot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Milon <plmilonpl@gmail.com>
// SPDX-FileCopyrightText: 2024 Brandon Hu <103440971+Brandon-Huu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Plykiya <58439124+Plykiya@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 nikthechampiongr <32041239+nikthechampiongr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2024 Rainfey <rainfey0+github@gmail.com>
// SPDX-FileCopyrightText: 2025 Nikovnik <116634167+nkokic@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Hannah Giovanna Dawson <karakkaraz@gmail.com>
// SPDX-FileCopyrightText: 2025 PJB3005 <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Vasilis The Pikachu <vasilis@pikachu.systems>
// SPDX-FileCopyrightText: 2025 Princess Cheeseballs <66055347+Princess-Cheeseballs@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Minemoder5000 <minemoder50000@gmail.com>
// SPDX-FileCopyrightText: 2025 Zachary Higgs <compgeek223@gmail.com>
// SPDX-FileCopyrightText: 2026 Fruitsalad <949631+Fruitsalad@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Medical.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Body.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Integrity.Components;
using Content.Shared.Medical.Integrity.Events;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.Body.Systems;

namespace Content.Server.Medical;

public sealed class HealthAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SurgeryLayerSystem _surgeryLayer = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HealthAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<HealthAnalyzerComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<HealthAnalyzerComponent, SurgeryRequestBuiMessage>(OnSurgeryRequest);
        SubscribeLocalEvent<SurgeryLayerComponent, SurgeryPenaltyAppliedEvent>(OnSurgeryPenaltyApplied);
    }

    private void OnSurgeryPenaltyApplied(Entity<SurgeryLayerComponent> ent, ref SurgeryPenaltyAppliedEvent args)
    {
        var bodyPart = ent.Owner;
        if (!TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp) || bodyPartComp.Body is not { } body)
            return;

        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.ScannedEntity == body)
            {
                UpdateScannedUser(uid, body, true);
                break;
            }
        }
    }

    private void OnSurgeryRequest(Entity<HealthAnalyzerComponent> uid, ref SurgeryRequestBuiMessage args)
    {
        if (uid.Comp.ScannedEntity is not { } target)
            return;

        var targetNet = GetNetEntity(target);
        if (args.Target != targetNet)
            return;

        var targetUid = GetEntity(args.Target);
        var bodyPartUid = GetEntity(args.BodyPart);
        var user = args.Actor;

        var ev = new SurgeryRequestEvent(uid.Owner, user, targetUid, bodyPartUid, args.StepId, args.Layer, args.IsImprovised,
            args.Organ.HasValue ? GetEntity(args.Organ.Value) : null);
        RaiseLocalEvent(targetUid, ref ev);

        if (!ev.Valid && ev.RejectReason != null && Exists(user))
        {
            var msg = ev.RejectReason switch
            {
                "missing-tool" => "health-analyzer-surgery-error-missing-tool",
                "already-done" => "health-analyzer-surgery-error-already-done",
                "layer-not-open" => "health-analyzer-surgery-error-layer-not-open",
                "doafter-failed" => "health-analyzer-surgery-error-doafter-failed",
                _ => "health-analyzer-surgery-error-invalid-surgical-process"
            };
            _popupSystem.PopupEntity(Loc.GetString(msg), user, user, PopupType.Medium);
        }
    }

    public override void Update(float frameTime)
    {
        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent, TransformComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var component, out var transform))
        {
            //Update rate limited to 1 second
            if (component.NextUpdate > _timing.CurTime)
                continue;

            if (component.ScannedEntity is not {} patient)
                continue;

            if (Deleted(patient))
            {
                StopAnalyzingEntity((uid, component), patient);
                continue;
            }

            component.NextUpdate = _timing.CurTime + component.UpdateInterval;

            //Get distance between health analyzer and the scanned entity
            //null is infinite range
            var patientCoordinates = Transform(patient).Coordinates;
            if (component.MaxScanRange != null && !_transformSystem.InRange(patientCoordinates, transform.Coordinates, component.MaxScanRange.Value))
            {
                //Range too far, disable updates
                StopAnalyzingEntity((uid, component), patient);
                continue;
            }

            UpdateScannedUser(uid, patient, true);
        }
    }

    /// <summary>
    /// Trigger the doafter for scanning
    /// </summary>
    private void OnAfterInteract(Entity<HealthAnalyzerComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target) || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        _audio.PlayPvs(uid.Comp.ScanningBeginSound, uid);

        var doAfterCancelled = !_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.ScanDelay, new HealthAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });

        if (args.Target == args.User || doAfterCancelled || uid.Comp.Silent)
            return;

        var msg = Loc.GetString("health-analyzer-popup-scan-target", ("user", Identity.Entity(args.User, EntityManager)));
        _popupSystem.PopupEntity(msg, args.Target.Value, args.Target.Value, PopupType.Medium);
    }

    private void OnDoAfter(Entity<HealthAnalyzerComponent> uid, ref HealthAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        if (!uid.Comp.Silent)
            _audio.PlayPvs(uid.Comp.ScanningEndSound, uid);

        OpenUserInterface(args.User, uid);
        BeginAnalyzingEntity(uid, args.Target.Value);
        args.Handled = true;
    }

    /// <summary>
    /// Turn off when placed into a storage item or moved between slots/hands
    /// </summary>
    private void OnInsertedIntoContainer(Entity<HealthAnalyzerComponent> uid, ref EntGotInsertedIntoContainerMessage args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    /// <summary>
    /// Disable continuous updates once turned off
    /// </summary>
    private void OnToggled(Entity<HealthAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity is { } patient)
            StopAnalyzingEntity(ent, patient);
    }

    /// <summary>
    /// Turn off the analyser when dropped
    /// </summary>
    private void OnDropped(Entity<HealthAnalyzerComponent> uid, ref DroppedEvent args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!_uiSystem.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        _uiSystem.OpenUi(analyzer, HealthAnalyzerUiKey.Key, user);
    }

    /// <summary>
    /// Mark the entity as having its health analyzed, and link the analyzer to it
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that should receive the updates</param>
    /// <param name="target">The entity to start analyzing</param>
    private void BeginAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        //Link the health analyzer to the scanned entity
        healthAnalyzer.Comp.ScannedEntity = target;

        _toggle.TryActivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, true);
    }

    /// <summary>
    /// Remove the analyzer from the active list, and remove the component if it has no active analyzers
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    private void StopAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        //Unlink the analyzer
        healthAnalyzer.Comp.ScannedEntity = null;

        _toggle.TryDeactivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, false);
    }

    /// <summary>
    /// Send an update for the target to the healthAnalyzer
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer</param>
    /// <param name="target">The entity being scanned</param>
    /// <param name="scanMode">True makes the UI show ACTIVE, False makes the UI show INACTIVE</param>
    public void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(healthAnalyzer, HealthAnalyzerUiKey.Key)
            || !HasComp<DamageableComponent>(target))
            return;

        var uiState = GetHealthAnalyzerUiState(target);
        uiState.ScanMode = scanMode;

        _uiSystem.ServerSendUiMessage(
            healthAnalyzer,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(uiState)
        );
    }

    /// <summary>
    /// Creates a HealthAnalyzerState based on the current state of an entity.
    /// </summary>
    /// <param name="target">The entity being scanned</param>
    /// <returns></returns>
    public HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target)
    {
        if (!target.HasValue || !HasComp<DamageableComponent>(target))
            return new HealthAnalyzerUiState();

        var entity = target.Value;
        var bodyTemperature = float.NaN;

        if (TryComp<TemperatureComponent>(entity, out var temp))
            bodyTemperature = temp.CurrentTemperature;

        var bloodAmount = float.NaN;
        var bleeding = false;
        var unrevivable = false;

        if (TryComp<BloodstreamComponent>(entity, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(entity, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = _bloodstreamSystem.GetBloodLevel(entity);
            bleeding = bloodstream.BleedAmount > 0;
        }

        if (TryComp<UnrevivableComponent>(entity, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        var state = new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null,
            bleeding,
            unrevivable
        );

        if (TryComp<BodyComponent>(entity, out var body))
        {
            var query = new BodyPartQueryEvent(entity);
            RaiseLocalEvent(entity, ref query);
            state.BodyParts = query.Parts.Select(e => GetNetEntity(e)).ToList();

            var totalEv = new IntegrityPenaltyTotalRequestEvent(entity);
            RaiseLocalEvent(entity, ref totalEv);
            var usage = TryComp<IntegrityUsageComponent>(entity, out var usageComp) ? usageComp.Usage : 0;
            state.IntegrityTotal = totalEv.Total + usage;
            state.IntegrityMax = 6;

            foreach (var part in query.Parts)
            {
                if (TryComp<SurgeryLayerComponent>(part, out var layer))
                {
                    var categoryId = TryComp<OrganComponent>(part, out var organ) ? organ.Category?.ToString() : null;
                    var stepsConfig = _surgeryLayer.GetStepsConfig(entity, part);

                    var skinProcedures = new List<SurgeryProcedureState>();
                    var tissueProcedures = new List<SurgeryProcedureState>();
                    bool skinOpen = false, tissueOpen = false, organOpen = false;

                    if (stepsConfig != null)
                    {
                        foreach (var stepId in stepsConfig.GetSkinOpenStepIds(_prototypes))
                            skinProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });
                        foreach (var stepId in stepsConfig.GetSkinCloseStepIds(_prototypes))
                            skinProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });
                        foreach (var stepId in stepsConfig.GetTissueOpenStepIds(_prototypes))
                            tissueProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });
                        foreach (var stepId in stepsConfig.GetTissueCloseStepIds(_prototypes))
                            tissueProcedures.Add(new SurgeryProcedureState { StepId = stepId, Performed = _surgeryLayer.IsStepPerformed((part, layer), stepId) });

                        skinOpen = _surgeryLayer.IsSkinOpen(layer, stepsConfig);
                        tissueOpen = _surgeryLayer.IsTissueOpen(layer, stepsConfig);
                        organOpen = _surgeryLayer.IsOrganLayerOpen(layer, stepsConfig);
                    }
                    else
                    {
                        skinProcedures.Add(new SurgeryProcedureState { StepId = "RetractSkin", Performed = _surgeryLayer.IsStepPerformed((part, layer), "RetractSkin") });
                        skinProcedures.Add(new SurgeryProcedureState { StepId = "CloseIncision", Performed = _surgeryLayer.IsStepPerformed((part, layer), "CloseIncision") });
                        tissueProcedures.Add(new SurgeryProcedureState { StepId = "RetractTissue", Performed = _surgeryLayer.IsStepPerformed((part, layer), "RetractTissue") });
                        tissueProcedures.Add(new SurgeryProcedureState { StepId = "SawBones", Performed = _surgeryLayer.IsStepPerformed((part, layer), "SawBones") });
                        tissueProcedures.Add(new SurgeryProcedureState { StepId = "CloseTissue", Performed = _surgeryLayer.IsStepPerformed((part, layer), "CloseTissue") });
                    }

                    var layerData = new SurgeryLayerStateData
                    {
                        BodyPart = GetNetEntity(part),
                        CategoryId = categoryId,
                        SkinProcedures = skinProcedures,
                        TissueProcedures = tissueProcedures,
                        SkinOpen = skinOpen,
                        TissueOpen = tissueOpen,
                        OrganOpen = organOpen,
                        OrganProcedures = _surgeryLayer.GetPerformedSteps((part, layer), SurgeryLayer.Organ).Select(s => new SurgeryProcedureState { StepId = s, Performed = true }).ToList()
                    };
                    if (TryComp<BodyPartComponent>(part, out var bodyPartComp) && bodyPartComp.Organs != null)
                    {
                        foreach (var child in bodyPartComp.Organs.ContainedEntities)
                        {
                            if (TryComp<OrganComponent>(child, out var childOrgan))
                            {
                                layerData.Organs.Add(new OrganInBodyPartData
                                {
                                    Organ = GetNetEntity(child),
                                    CategoryId = childOrgan.Category?.ToString()
                                });
                            }
                        }
                        foreach (var slot in bodyPartComp.Slots)
                        {
                            var slotId = slot.ToString();
                            var filled = layerData.Organs.Any(o => o.CategoryId == slotId);
                            if (!filled)
                                layerData.EmptySlots.Add(slotId);
                        }
                    }
                    state.BodyPartLayerState.Add(layerData);
                }
            }
        }

        return state;
    }
}
