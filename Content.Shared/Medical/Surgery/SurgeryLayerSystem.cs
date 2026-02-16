using Content.Shared.Medical.Surgery.Components;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Surgery layer state is initialized by BodySystem.OnBodyPartInit.
/// This system exists for future layer-related logic if needed.
/// </summary>
public sealed class SurgeryLayerSystem : EntitySystem
{
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
}
