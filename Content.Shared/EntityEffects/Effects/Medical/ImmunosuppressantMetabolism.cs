using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Medical;

/// <summary>
/// Effect data for immunosuppressant metabolism.
/// </summary>
/// <inheritdoc cref="EntityEffectBase{T}"/>
public sealed partial class ImmunosuppressantMetabolism : EntityEffectBase<ImmunosuppressantMetabolism>
{
    /// <summary>
    /// Integrity bonus per unit of reagent (e.g., 0.5 for basic, 1.0 for advanced).
    /// </summary>
    [DataField]
    public float IntegrityPerUnit = 0.5f;

    /// <summary>
    /// The reagent being tracked.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> ReagentId;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString(
            "entity-effect-guidebook-immunosuppressant",
            ("bonus", IntegrityPerUnit));
    }

}
