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

using Content.Server.Medical.Components;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.Medical.Surgery;
using Content.Server.Medical.Surgery;
using Content.Shared.Medical.Integrity;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Medical;

public sealed class HealthAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SurgerySystem _surgerySystem = default!;
    [Dependency] private readonly SharedBodyPartSystem _bodyPartSystem = default!;
    [Dependency] private readonly BodyPartQuerySystem _bodyPartQuery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HealthAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<HealthAnalyzerComponent, DroppedEvent>(OnDropped);
        
        // Subscribe to BUI messages for surgery button and surgery attempts
        Subs.BuiEvents<HealthAnalyzerComponent>(HealthAnalyzerUiKey.Key, subs =>
        {
            subs.Event<BeginSurgeryMessage>(OnBeginSurgery);
            subs.Event<AttemptSurgeryMessage>(OnAttemptSurgery);
        });
    }
    
    private void OnBeginSurgery(Entity<HealthAnalyzerComponent> ent, ref BeginSurgeryMessage msg)
    {
        var targetEntity = GetEntity(msg.TargetEntity);
        
        // Verify target entity has body
        if (!HasComp<BodyComponent>(targetEntity))
            return;
        
        // Find first body part with SurgeryLayerComponent (prefer torso)
        var bodyPartToOpen = _surgerySystem.FindBodyPartForSurgery(targetEntity);
        
        if (bodyPartToOpen == null)
            return;
        
        // Get the user from the BUI - get the first actor that has the UI open
        // In practice, we'd want to get the specific user who sent the message
        var actors = _uiSystem.GetActors((ent, null), HealthAnalyzerUiKey.Key).ToList();
        if (actors.Count == 0)
            return;
            
        var user = actors.First();
        
        // Open surgery UI
        if (bodyPartToOpen != null && TryComp<SurgeryLayerComponent>(bodyPartToOpen.Value, out var layer))
        {
            _surgerySystem.OpenSurgeryUI((bodyPartToOpen.Value, layer), user);
        }
    }

    private void OnAttemptSurgery(Entity<HealthAnalyzerComponent> ent, ref AttemptSurgeryMessage msg)
    {
        var targetEntity = GetEntity(msg.TargetEntity);
        var stepEntity = GetEntity(msg.Step);
        
        // Verify target entity has body
        if (!HasComp<BodyComponent>(targetEntity))
            return;
        
        // Find body part for surgery
        EntityUid? bodyPart = null;
        if (msg.SelectedBodyPart.HasValue)
        {
            // Find the specific body part
            var (targetType, targetSymmetry) = _bodyPartQuery.ConvertTargetBodyPart(msg.SelectedBodyPart.Value);
            var bodyParts = _bodyPartSystem.GetBodyChildrenOfType(targetEntity, targetType, symmetry: targetSymmetry);
            var foundPart = bodyParts.FirstOrDefault();
            if (foundPart.Id != default)
            {
                bodyPart = foundPart.Id;
            }
        }
        
        // Fallback to default body part
        if (bodyPart == null)
        {
            bodyPart = _surgerySystem.FindBodyPartForSurgery(targetEntity);
        }
        
        if (bodyPart == null || !TryComp<SurgeryLayerComponent>(bodyPart.Value, out var layer))
            return;
        
        // Get the user from the BUI
        var actors = _uiSystem.GetActors((ent, null), HealthAnalyzerUiKey.Key).ToList();
        if (actors.Count == 0)
            return;
            
        var user = actors.First();
        
        // If method was selected (improvised), store it first
        // SurgerySystem will check this when the step is executed
        if (msg.IsImprovised)
        {
            // We need to access SurgerySystem's internal dictionary
            // Since it's private, we'll need to send the method selection message separately
            // But first, let's send the step selected message
            var methodSelectedMsg = new SurgeryOperationMethodSelectedMessage(msg.Step, true);
            RaiseLocalEvent((bodyPart.Value, layer), methodSelectedMsg);
        }
        
        // Send surgery step selected message to SurgerySystem
        // This will trigger the surgery doafter
        var stepSelectedMsg = new SurgeryStepSelectedMessage(
            msg.Step,
            msg.Layer,
            GetNetEntity(user),
            msg.SelectedBodyPart
        );
        
        // Raise the step selected event on the body part
        RaiseLocalEvent((bodyPart.Value, layer), stepSelectedMsg);
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

        // BloodstreamComponent doesn't exist in Forky
#if false
        if (TryComp<BloodstreamComponent>(entity, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(entity, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = _bloodstreamSystem.GetBloodLevel(entity);
            bleeding = bloodstream.BleedAmount > 0;
        }
#else
        // Blood system not available in Forky - leave bloodAmount as NaN and bleeding as false
#endif

        if (TryComp<UnrevivableComponent>(entity, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        // Collect integrity data if available
        int? maxIntegrity = null;
        FixedPoint2? usedIntegrity = null;
        FixedPoint2? temporaryIntegrityBonus = null;
        FixedPoint2? currentBioRejection = null;
        FixedPoint2? surgeryPenalty = null;
        List<IntegrityBreakdownEntry>? integrityBreakdown = null;

        if (TryComp<IntegrityComponent>(entity, out var integrity))
        {
            maxIntegrity = integrity.MaxIntegrity;
            usedIntegrity = integrity.UsedIntegrity;
            temporaryIntegrityBonus = integrity.TemporaryIntegrityBonus;
            currentBioRejection = integrity.CurrentBioRejection;
            surgeryPenalty = integrity.CachedSurgeryPenalty;
            integrityBreakdown = GetIntegrityBreakdown(entity);
        }

        // Get surgery steps if entity has a body
        List<NetEntity>? surgerySteps = null;
        Dictionary<NetEntity, SurgeryStepOperationInfo>? surgeryStepOperationInfo = null;
        SurgeryLayer? currentSurgeryLayer = null;
        TargetBodyPart? selectedSurgeryBodyPart = null;

        if (HasComp<BodyComponent>(entity))
        {
            var bodyPart = _surgerySystem.FindBodyPartForSurgery(entity);
            if (bodyPart != null)
            {
                // Get user from health analyzer UI if available (for operation availability evaluation)
                EntityUid? evalUser = null;
                // Note: We can't easily get the user here since we don't have the analyzer entity
                // Operation availability will be evaluated on the client side or when user attempts surgery
                
                var (steps, stepInfo, layer, bodyPartEnum) = _surgerySystem.GetSurgeryStepsForBodyPart(bodyPart.Value, evalUser);
                surgerySteps = steps;
                surgeryStepOperationInfo = stepInfo;
                currentSurgeryLayer = layer;
                selectedSurgeryBodyPart = bodyPartEnum;
            }
        }

        return new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null,
            bleeding,
            unrevivable,
            maxIntegrity,
            usedIntegrity,
            temporaryIntegrityBonus,
            currentBioRejection,
            surgeryPenalty,
            integrityBreakdown,
            surgerySteps,
            surgeryStepOperationInfo,
            currentSurgeryLayer,
            selectedSurgeryBodyPart
        );
    }

    /// <summary>
    /// Collects integrity breakdown data from all body parts.
    /// </summary>
    private List<IntegrityBreakdownEntry> GetIntegrityBreakdown(EntityUid target)
    {
        var breakdown = new List<IntegrityBreakdownEntry>();

        if (!TryComp<BodyComponent>(target, out var body))
            return breakdown;

        // Iterate all body parts
        foreach (var (partId, part) in _bodyPartSystem.GetBodyChildren(target, body))
        {
            // Check for applied integrity cost
            if (TryComp<AppliedIntegrityCostComponent>(partId, out var appliedCost) && appliedCost.AppliedCost > 0)
            {
                var partName = MetaData(partId).EntityName;
                string componentType;

                // Determine component type
                // Check cybernetic first since cybernetics may also have BodyPartComponent
                if (HasComp<CyberneticIntegrityComponent>(partId))
                {
                    componentType = "cybernetic";
                }
                else if (HasComp<OrganComponent>(partId))
                {
                    componentType = "organ";
                }
                else if (HasComp<BodyPartComponent>(partId))
                {
                    componentType = "limb";
                }
                else
                {
                    componentType = "unknown";
                }

                breakdown.Add(new IntegrityBreakdownEntry(partName, appliedCost.AppliedCost, componentType));
            }

            // Check for surgery penalty
            if (TryComp<SurgeryPenaltyComponent>(partId, out var penalty) && penalty.CurrentPenalty > 0)
            {
                var partName = MetaData(partId).EntityName;
                breakdown.Add(new IntegrityBreakdownEntry(partName, penalty.CurrentPenalty, "surgery_penalty"));
            }
        }

        return breakdown;
    }
}
