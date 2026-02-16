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
    /// Returns whether the given step can be performed based on config-driven prerequisites.
    /// </summary>
    public bool CanPerformStep(string stepId, SurgeryLayer layer, SurgeryLayerComponent layerComp, BodyPartSurgeryStepsPrototype stepsConfig)
    {
        var performedList = layer switch
        {
            SurgeryLayer.Skin => layerComp.PerformedSkinSteps,
            SurgeryLayer.Tissue => layerComp.PerformedTissueSteps,
            SurgeryLayer.Organ => layerComp.PerformedOrganSteps,
            _ => null
        };
        if (performedList == null)
            return false;

        if (layer == SurgeryLayer.Skin)
        {
            var skinOpen = stepsConfig.GetSkinOpenStepIds(_prototypes).ToList();
            var skinClose = stepsConfig.GetSkinCloseStepIds(_prototypes).ToList();
            var skinClosed = skinClose.Any(s => performedList.Contains(s));
            var idx = skinOpen.IndexOf(stepId);
            if (idx >= 0)
            {
                // When layer is closed, allow open steps again (cyclical: close -> open -> close -> ...)
                if (skinClosed)
                {
                    for (var i = 0; i < idx; i++)
                        if (!performedList.Contains(skinOpen[i]))
                            return false;
                    return true;
                }
                if (performedList.Contains(stepId))
                    return false;
                for (var i = 0; i < idx; i++)
                    if (!performedList.Contains(skinOpen[i]))
                        return false;
                return true;
            }
            idx = skinClose.IndexOf(stepId);
            if (idx >= 0)
            {
                if (!skinOpen.All(s => performedList.Contains(s)))
                    return false;
                for (var i = 0; i < idx; i++)
                    if (!performedList.Contains(skinClose[i]))
                        return false;
                return true;
            }
        }

        if (layer == SurgeryLayer.Tissue)
        {
            var tissueOpen = stepsConfig.GetTissueOpenStepIds(_prototypes).ToList();
            var tissueClose = stepsConfig.GetTissueCloseStepIds(_prototypes).ToList();
            var tissueClosed = tissueClose.Any(s => performedList.Contains(s));
            var idx = tissueOpen.IndexOf(stepId);
            if (idx >= 0)
            {
                if (!IsSkinOpen(layerComp, stepsConfig))
                    return false;
                // When tissue layer is closed, allow open steps again (cyclical)
                if (tissueClosed)
                {
                    for (var i = 0; i < idx; i++)
                        if (!performedList.Contains(tissueOpen[i]))
                            return false;
                    return true;
                }
                if (performedList.Contains(stepId))
                    return false;
                if (tissueClose.Any(s => performedList.Contains(s)))
                    return false;
                for (var i = 0; i < idx; i++)
                    if (!performedList.Contains(tissueOpen[i]))
                        return false;
                return true;
            }
            idx = tissueClose.IndexOf(stepId);
            if (idx >= 0)
            {
                if (!IsTissueOpen(layerComp, stepsConfig))
                    return false;
                for (var i = 0; i < idx; i++)
                    if (!performedList.Contains(tissueClose[i]))
                        return false;
                return true;
            }
        }

        if (layer == SurgeryLayer.Organ)
        {
            if (!stepsConfig.OrganSteps.Any(s => s.ToString() == stepId))
                return false;
            return IsOrganLayerOpen(layerComp, stepsConfig);
        }

        return false;
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
