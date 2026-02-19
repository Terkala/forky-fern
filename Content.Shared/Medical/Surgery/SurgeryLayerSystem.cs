using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Events;
using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Surgery layer state is initialized by BodySystem.OnBodyPartInit.
/// Provides config-driven layer state and step validation.
/// </summary>
public sealed class SurgeryLayerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    /// <summary>
    /// Resolves BodyPartSurgeryStepsPrototype for the given species and organ category.
    /// </summary>
    public BodyPartSurgeryStepsPrototype? GetStepsConfig(ProtoId<Humanoid.Prototypes.SpeciesPrototype> speciesId, ProtoId<OrganCategoryPrototype> organCategory)
    {
        foreach (var proto in _prototypes.EnumeratePrototypes<BodyPartSurgeryStepsPrototype>())
        {
            if (proto.SpeciesId == speciesId && proto.OrganCategory == organCategory)
                return proto;
        }
        return null;
    }

    /// <summary>
    /// Resolves steps config for a body part. Uses SurgeryBodyPartComponent if present, else body's species and part's OrganComponent.
    /// </summary>
    public BodyPartSurgeryStepsPrototype? GetStepsConfig(EntityUid body, EntityUid bodyPart)
    {
        if (TryComp<SurgeryBodyPartComponent>(bodyPart, out var surgeryBodyPart))
            return GetStepsConfig(surgeryBodyPart.SpeciesId, surgeryBodyPart.OrganCategory);

        if (!TryComp<HumanoidAppearanceComponent>(body, out var humanoid) || !TryComp<OrganComponent>(bodyPart, out var organ) || organ.Category is not { } category)
            return null;

        return GetStepsConfig(humanoid.Species, category);
    }

    /// <summary>
    /// Returns whether the skin layer is open (all skinOpenSteps done, no skinCloseSteps done).
    /// </summary>
    public bool IsSkinOpen(SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        var skinOpen = stepsConfig.GetSkinOpenStepIds(_prototypes);
        var skinClose = stepsConfig.GetSkinCloseStepIds(_prototypes);
        return skinOpen.All(s => layerComp.PerformedSkinSteps.Contains(s))
            && !skinClose.Any(s => layerComp.PerformedSkinSteps.Contains(s));
    }

    /// <summary>
    /// Returns whether the tissue layer is open (skin open + all tissueOpenSteps done, no tissueCloseSteps done).
    /// </summary>
    public bool IsTissueOpen(SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        if (!IsSkinOpen(layerComp, stepsConfig))
            return false;
        var tissueOpen = stepsConfig.GetTissueOpenStepIds(_prototypes);
        var tissueClose = stepsConfig.GetTissueCloseStepIds(_prototypes);
        return tissueOpen.All(s => layerComp.PerformedTissueSteps.Contains(s))
            && !tissueClose.Any(s => layerComp.PerformedTissueSteps.Contains(s));
    }

    /// <summary>
    /// Returns whether the organ layer is open (tissue open; organ has no close steps).
    /// </summary>
    public bool IsOrganLayerOpen(SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        return IsTissueOpen(layerComp, stepsConfig);
    }

    /// <summary>
    /// Returns whether the given step can be performed based on declarative prerequisites.
    /// </summary>
    public bool CanPerformStep(string stepId, SurgeryLayer layer, SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        if (!_prototypes.TryIndex<SurgeryStepPrototype>(stepId, out var step))
            return false;
        if (step.Layer != layer)
            return false;

        if (!IsStepAllowedForBodyPart(stepId, layer, stepsConfig))
            return false;

        return EvaluatePrerequisites(step.Prerequisites, layerComp, stepsConfig);
    }

    /// <summary>
    /// Returns whether the step is in this body part's catalog (from stepsConfig).
    /// </summary>
    public bool IsStepAllowedForBodyPart(string stepId, SurgeryLayer layer, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        return layer switch
        {
            SurgeryLayer.Skin => stepsConfig.GetSkinOpenStepIds(_prototypes).Contains(stepId)
                || stepsConfig.GetSkinCloseStepIds(_prototypes).Contains(stepId),
            SurgeryLayer.Tissue => stepsConfig.GetTissueOpenStepIds(_prototypes).Contains(stepId)
                || stepsConfig.GetTissueCloseStepIds(_prototypes).Contains(stepId),
            SurgeryLayer.Organ => stepsConfig.OrganSteps.Any(s => s.ToString() == stepId),
            _ => false
        };
    }

    private bool EvaluatePrerequisites(IReadOnlyList<StepPrerequisite> prereqs, SurgeryLayerComponent comp, BodyPartSurgeryStepsPrototype config)
    {
        foreach (var p in prereqs)
        {
            if (!EvaluateOne(p, comp, config))
                return false;
        }
        return true;
    }

    private bool EvaluateOne(StepPrerequisite p, SurgeryLayerComponent comp, BodyPartSurgeryStepsPrototype config)
    {
        switch (p.Type)
        {
            case StepPrerequisiteType.RequireLayerOpen:
                if (p.Layer is not { } layer)
                    return false;
                return IsLayerOpen(comp, config, layer);
            case StepPrerequisiteType.RequireLayerClosed:
                if (p.Layer is not { } layerClosed)
                    return false;
                return !IsLayerOpen(comp, config, layerClosed);
            case StepPrerequisiteType.RequireStepPerformed:
                if (string.IsNullOrEmpty(p.StepId))
                    return false;
                if (!_prototypes.TryIndex<SurgeryStepPrototype>(p.StepId, out var reqStep))
                    return false;
                var performedList = reqStep.Layer switch
                {
                    SurgeryLayer.Skin => comp.PerformedSkinSteps,
                    SurgeryLayer.Tissue => comp.PerformedTissueSteps,
                    SurgeryLayer.Organ => comp.PerformedOrganSteps,
                    _ => null
                };
                return performedList != null && performedList.Contains(p.StepId);
            default:
                return false;
        }
    }

    private bool IsLayerOpen(SurgeryLayerComponent comp, BodyPartSurgeryStepsPrototype config, SurgeryLayer layer)
    {
        return layer switch
        {
            SurgeryLayer.Skin => IsSkinOpen(comp, config),
            SurgeryLayer.Tissue => IsTissueOpen(comp, config),
            SurgeryLayer.Organ => IsOrganLayerOpen(comp, config),
            _ => false
        };
    }

    /// <summary>
    /// Returns the list of step IDs that can be performed on this body part.
    /// </summary>
    public IReadOnlyList<string> GetAvailableSteps(EntityUid body, EntityUid bodyPart, SurgeryLayer? filterLayer = null)
    {
        var stepsConfig = GetStepsConfig(body, bodyPart);
        if (stepsConfig == null)
            return Array.Empty<string>();

        var layerComp = CompOrNull<SurgeryLayerComponent>(bodyPart);
        if (layerComp == null)
            return Array.Empty<string>();

        var allSteps = GetAllStepsForBodyPart(stepsConfig);
        var result = new List<string>();
        foreach (var stepId in allSteps)
        {
            if (!_prototypes.TryIndex<SurgeryStepPrototype>(stepId, out var step))
                continue;
            if (filterLayer.HasValue && step.Layer != filterLayer.Value)
                continue;
            if (CanPerformStep(stepId, step.Layer, layerComp, stepsConfig))
                result.Add(stepId);
        }
        return result;
    }

    private IEnumerable<string> GetAllStepsForBodyPart(BodyPartSurgeryStepsPrototype config)
    {
        var skinOpen = config.GetSkinOpenStepIds(_prototypes);
        var skinClose = config.GetSkinCloseStepIds(_prototypes);
        var tissueOpen = config.GetTissueOpenStepIds(_prototypes);
        var tissueClose = config.GetTissueCloseStepIds(_prototypes);
        var organ = config.OrganSteps.Select(s => s.ToString());
        return skinOpen.Concat(skinClose).Concat(tissueOpen).Concat(tissueClose).Concat(organ).Distinct();
    }

    /// <summary>
    /// Returns available steps for an empty limb slot (e.g. after DetachLimb).
    /// Only AttachLimb is relevant; no layer opening is required.
    /// </summary>
    public IReadOnlyList<string> GetAvailableStepsForEmptySlot(EntityUid body, string categoryId)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            return Array.Empty<string>();

        var category = new ProtoId<OrganCategoryPrototype>(categoryId);
        var stepsConfig = GetStepsConfig(humanoid.Species, category);
        if (stepsConfig == null)
            return Array.Empty<string>();

        if (!stepsConfig.OrganSteps.Any(s => s.ToString() == "AttachLimb"))
            return Array.Empty<string>();

        return new[] { "AttachLimb" };
    }

    /// <summary>
    /// Returns whether the given step has been performed on this body part.
    /// </summary>
    public bool IsStepPerformed(Entity<SurgeryLayerComponent> ent, string stepId)
    {
        var comp = ent.Comp;
        return comp.PerformedSkinSteps.Contains(stepId)
            || comp.PerformedTissueSteps.Contains(stepId)
            || comp.PerformedOrganSteps.Contains(stepId);
    }

    /// <summary>
    /// Returns the list of performed step IDs for the given layer.
    /// </summary>
    public IReadOnlyList<string> GetPerformedSteps(Entity<SurgeryLayerComponent> ent, SurgeryLayer layer)
    {
        var comp = ent.Comp;
        return layer switch
        {
            SurgeryLayer.Skin => comp.PerformedSkinSteps,
            SurgeryLayer.Tissue => comp.PerformedTissueSteps,
            SurgeryLayer.Organ => comp.PerformedOrganSteps,
            _ => Array.Empty<string>()
        };
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryLayerComponent, SurgeryStepRequestEvent>(OnSurgeryStepRequest);
    }

    private void OnSurgeryStepRequest(Entity<SurgeryLayerComponent> ent, ref SurgeryStepRequestEvent args)
    {
        if (args.StepsConfig == null)
        {
            args.Valid = false;
            args.RejectReason = "unknown-species-or-category";
            return;
        }

        if (!CanPerformStep(args.StepId, args.Layer, ent.Comp, args.StepsConfig))
        {
            args.Valid = false;
            args.RejectReason = "layer-not-open";
            return;
        }
    }
}
